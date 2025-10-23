#nullable enable
using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Core.Constants;
using Altinn.Platform.Authentication.Core.Helpers;
using Altinn.Platform.Authentication.Core.Models.Oidc;
using Altinn.Platform.Authentication.Core.RepositoryInterfaces;
using Altinn.Platform.Authentication.Core.Services.Interfaces;
using Altinn.Platform.Authentication.Helpers;
using Altinn.Platform.Authentication.Model;
using Altinn.Platform.Authentication.Services.Interfaces;
using Altinn.Platform.Profile.Models;
using AltinnCore.Authentication.Constants;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Altinn.Platform.Authentication.Services
{
    /// <summary>
    /// Service that implements the OIDC <c>/authorize</c> front-channel flow for Altinn Authentication as an OP.
    /// </summary>
    public class OidcServerService(ILogger<OidcServerService> logger, 
        IOidcServerClientRepository oidcServerClientRepository, 
        ILoginTransactionRepository loginTransactionRepository,
        IUpstreamLoginTransactionRepository upstreamLoginTransactionRepository,
        IAuthorizeRequestValidator authorizeRequestValidator, 
        IAuthorizeClientPolicyValidator authorizeClientPolicyValidator,
        IOptions<OidcProviderSettings> oidcProviderSettings,
        TimeProvider timeProvider,
        IOidcProvider oidcProvider,
        IUpstreamTokenValidator upstreamTokenValidator,
        IUserProfileService userProfileService,
        IProfile profile,
        IOidcSessionRepository oidcSessionRepository,
        IAuthorizationCodeRepository authorizationCodeRepository,
        IOptions<GeneralSettings> generalSettings,
        ITokenService tokenService,
        IRefreshTokenRepository refreshTokenRepository,
        IUnregisteredClientRepository unregisteredClientRequestRepository,
        ISblCookieDecryptionService sblCookieDecryptionService) : IOidcServerService
    {
        private readonly ILogger<OidcServerService> _logger = logger;
        private readonly IOidcServerClientRepository _oidcServerClientRepository = oidcServerClientRepository;
        private readonly ILoginTransactionRepository _loginTxRepo = loginTransactionRepository;
        private readonly IUpstreamLoginTransactionRepository _upstreamLoginTxRepo = upstreamLoginTransactionRepository;
        private readonly IAuthorizeRequestValidator _basicValidator = authorizeRequestValidator;
        private readonly IAuthorizeClientPolicyValidator _clientValidator = authorizeClientPolicyValidator;
        private readonly OidcProviderSettings _oidcProviderSettings = oidcProviderSettings.Value;
        private readonly TimeProvider _timeProvider = timeProvider;
        private readonly IOidcProvider _oidcProvider = oidcProvider;
        private readonly IUpstreamTokenValidator _upstreamTokenValidator = upstreamTokenValidator;
        private readonly IUserProfileService _userProfileService = userProfileService;
        private readonly IProfile _profileService = profile;
        private readonly IOidcSessionRepository _oidcSessionRepo = oidcSessionRepository;
        private readonly IAuthorizationCodeRepository _authorizationCodeRepo = authorizationCodeRepository;
        private readonly GeneralSettings _generalSettings = generalSettings.Value;
        private readonly ITokenService _tokenService = tokenService;
        private readonly IRefreshTokenRepository _refreshTokenRepo = refreshTokenRepository;
        private readonly IUnregisteredClientRepository _unregisteredClientRequestRepository = unregisteredClientRequestRepository;
        private readonly ISblCookieDecryptionService _cookieDecryptionService = sblCookieDecryptionService;
        private static readonly string DefaultProviderKey = "idporten";

        /// <summary>
        /// Handles an incoming OIDC <c>/authorize</c> request from a Downstream client in the Altinn Platform.
        /// This can be Arbeidsflate or other application.
        /// Identifes the correct Upstream ID Provider like ID-porten, UIDP, Testlogin or other configured provider
        /// Stores downstream login transaction and upstream transaction before redirecting to the correct upstream ID-provider
        /// </summary>
        public async Task<AuthorizeResult> Authorize(AuthorizeRequest request, ClaimsPrincipal principal, string? sessionHandle, CancellationToken cancellationToken)
        {
            // Local helper to choose error redirect or local error based on redirect_uri validity
            // 1) Client lookup
            OidcClient? client = await _oidcServerClientRepository.GetClientAsync(request.ClientId, cancellationToken);
            if (client is null)
            {
                return Fail(request, new AuthorizeValidationError { Error = "unauthorized_client", Description = $"Unknown client_id '{request.ClientId}'." }, client);
            }

            // ========= 2) Basic validation =========
            AuthorizeValidationError? basicError = _basicValidator.ValidateBasics(request);
            if (basicError is not null)
            {
                return Fail(request, basicError, client);
            }

            // 3) Client-binding validation
            AuthorizeValidationError? bindError = _clientValidator.ValidateClientBinding(request, client);
            if (bindError is not null)
            {
                return Fail(request, bindError, client);
            }

            // ========= 3) Handle PAR / JAR if present =========
            // TODO: if request.RequestUri != null -> load par_request, verify TTL and client_id match, override parameters
            // TODO: if request.RequestObject != null -> validate JWS/JWE, extract claims/params (optional phase)
            // NOTE Currently no need to support PAR/JAR for our downstream clients since Arbeidsflate does not support it

            // ========= 4) Persist login_transaction(downstream) =========
            LoginTransaction tx = await PersistLoginTransaction(request, client, cancellationToken);

            // ========= 5) Existing IdP session reuse =========
            // Is request contains a valid AltinnStudioRuntime cookie there will be a autenticated principal on the request
            OidcSession? existingSession = null;
            if (principal.Identity != null && principal.Identity.IsAuthenticated)
            {
                Claim? sidClaim = principal.Claims.FirstOrDefault(c => c.Type == "sid");
                if (sidClaim != null)
                {
                    existingSession = await _oidcSessionRepo.GetBySidAsync(sidClaim.Value, cancellationToken);
                }
            }
            else if (!string.IsNullOrEmpty(sessionHandle))
            {
                byte[] sessionHandleByte = FromBase64Url(sessionHandle);
                existingSession = await _oidcSessionRepo.GetBySessionHandleAsync(sessionHandleByte, cancellationToken);
            }

            // Verify that found session 
            if (existingSession is not null
                && existingSession.ExpiresAt.HasValue
                && _timeProvider.GetUtcNow() < existingSession.ExpiresAt.Value)
            {
                // Check if existing session meets ACR requirements from request
                if (!AuthenticationHelper.NeedAcrUpgrade(existingSession.Acr, request.AcrValues))
                {
                    // There is a valid session with high enough ACR. We just create a code an return straight away. Also slide session expiry.
                    await _oidcSessionRepo.SlideExpiryToAsync(existingSession.Sid, _timeProvider.GetUtcNow().AddMinutes(_generalSettings.JwtValidityMinutes), cancellationToken);
                    string code = await CreateDownstreamAuthorizationCode(null, tx, existingSession, cancellationToken);
                    return AuthorizeResult.RedirectToDownstreamBasedOnReusedSession(
                        request.RedirectUri, // safe because validated
                        code,
                        request.State!);
                }
            }

            // TODO: try locate valid oidc_session for (client_id, subject) meeting acr/max_age
            // TODO: if reusable and no prompt=login: proceed to issue downstream authorization_code (future extension)

            // ========= 6) Choose upstream and derive upstream params =========
            (OidcProvider provider, string upstreamState, string upstreamNonce, string upstreamPkceChallenge) = await CreateUpstreamLoginTransaction(request, tx, cancellationToken);

            // ========= 8) Build upstream authorize URL =========
            Uri authorizeUrl = BuildUpstreamAuthorizeUrl(
                provider,
                upstreamState,
                upstreamNonce,
                upstreamPkceChallenge,
                request,
                provider.IssuerKey);

            // ========= 9) Return redirect upstream =========
            return AuthorizeResult.RedirectUpstream(authorizeUrl, upstreamState, tx.RequestId);
        }

        /// <summary>
        /// Authorize unregistered client is used for flows where no client_id is sent in the authorize request and the result will only be a JWT token inside a cookie.
        /// </summary>
        public async Task<AuthorizeResult> AuthorizeUnregisteredClient(AuthorizeUnregisteredClientRequest request, CancellationToken cancellationToken)
        {
            OidcProvider provider = ChooseProvider(request);

            UnregisteredClientRequestCreate unregisteredClientRequestCreate = new()
            {
                RequestId = Guid.NewGuid(),
                ExpiresAt = _timeProvider.GetUtcNow().AddMinutes(10),
                Issuer = provider.Issuer,
                GotoUrl = request.GoTo,
                CreatedByIp = request.ClientIp,
                UserAgentHash = request.UserAgentHash,
                CorrelationId = request.CorrelationId
            };

            await _unregisteredClientRequestRepository.InsertAsync(unregisteredClientRequestCreate, cancellationToken);

            (string upstreamState, string upstreamNonce, string upstreamPkceChallenge) = await CreateUpstreamLoginTransaction(unregisteredClientRequestCreate, provider, cancellationToken);

            Uri authorizeUrl = BuildUpstreamAuthorizeUrl(
            provider,
            upstreamState,
            upstreamNonce,
            upstreamPkceChallenge,
            request,
            provider.IssuerKey);

            // ========= 9) Return redirect upstream =========
            return AuthorizeResult.RedirectUpstream(authorizeUrl, upstreamState, unregisteredClientRequestCreate.RequestId);
        }

        /// <inheritdoc/>
        public async Task<UpstreamCallbackResult> HandleUpstreamCallback(UpstreamCallbackInput input, string? existingSessionHandle, CancellationToken cancellationToken)
        {
            // ===== 1) Validate input + load upstream transaction =====
            (UpstreamCallbackResult? callbackResultUpstreamValidation, UpstreamLoginTransaction? upstreamTx) = await ValidateUpstreamCallbackState(input, cancellationToken);
            if (callbackResultUpstreamValidation != null)
            {
                return callbackResultUpstreamValidation;
            }

            // Check if there is an existing session to be removed
            if (existingSessionHandle != null)
            {
                byte[] handleHash = HashHandle(FromBase64Url(existingSessionHandle));

                OidcSession? currentSession = await _oidcSessionRepo.GetBySessionHandleAsync(handleHash, cancellationToken);
                if (currentSession != null)
                {
                    await _oidcSessionRepo.DeleteBySidAsync(currentSession.Sid, cancellationToken);
                }
            }

            Debug.Assert(upstreamTx != null);

            // ===== 2) Exchange upstream code for upstream tokens =====
            OidcProvider provider = ChooseProviderByKey(upstreamTx.Provider);
            UserAuthenticationModel userIdenity = await ExtractUserIdentityFromUpstream(input, upstreamTx, provider, cancellationToken);
            userIdenity = await IdentifyOrCreateAltinnUser(userIdenity, provider);
            AddLocalScopes(userIdenity);

            // 3. Create or refresh Altinn session session
            (OidcSession session, string sessionHandle) = await CreateOrUpdateOidcSession(upstreamTx, userIdenity, cancellationToken);

            string cookieToken = await _tokenService.CreateCookieToken(session, cancellationToken);

            CookieInstruction altinnStudioRuntime = new()
            {
                Name = _generalSettings.JwtCookieName,
                Value = cookieToken,
                HttpOnly = true,
                Secure = true,
                Path = "/",
                SameSite = SameSiteMode.Lax
            };

            CookieInstruction altinnSessionCookie = new()
            {
                Name = _generalSettings.AltinnSessionCookieName,
                Value = sessionHandle,
                HttpOnly = true,
                Secure = true,
                Path = "/",
                SameSite = SameSiteMode.Lax,
            };

            UpstreamCallbackResult? upstreamCallbackResult = null;

            // TODO How to handle first time login with epost bruker from ID porten if we gonna ask them to connect to existing self identified user.
            if (upstreamTx.RequestId != null)
            {
                // 4) Load downstream (original) transaction to get validated redirect_uri & original state =====
                (UpstreamCallbackResult? downStreamValidationResult, LoginTransaction? loginTx) = await ValidateDownstreamCallbackState(input, upstreamTx, cancellationToken);
                if (downStreamValidationResult != null)
                {
                    return downStreamValidationResult;
                }

                Debug.Assert(loginTx != null);

                // 5) Issue downstream authorization code
                string authCode = await CreateDownstreamAuthorizationCode(upstreamTx, loginTx, session, cancellationToken);
                upstreamCallbackResult = new UpstreamCallbackResult
                {
                    Kind = UpstreamCallbackResultKind.RedirectToClient,
                    ClientRedirectUri = loginTx!.RedirectUri,
                    DownstreamCode = authCode,
                    ClientState = loginTx.State,
                    Cookies = [altinnStudioRuntime, altinnSessionCookie]
                };
            }
            else if (upstreamTx.UnregisteredClientRequestId != null)
            {
                UnregisteredClientRequest? unregisteredClientRequest = await _unregisteredClientRequestRepository.GetByRequestIdAsync(upstreamTx.UnregisteredClientRequestId.Value, cancellationToken);
                Debug.Assert(unregisteredClientRequest != null);
                upstreamCallbackResult = new UpstreamCallbackResult
                {
                    Kind = UpstreamCallbackResultKind.RedirectToGoTo,
                    ClientRedirectUri = new Uri(unregisteredClientRequest.GotoUrl),
                    DownstreamCode = null,
                    ClientState = null,
                    Cookies = [altinnStudioRuntime, altinnSessionCookie]
                };
            }
            
            Debug.Assert(upstreamCallbackResult != null);

            // 6) Mark upstream transaction as completed
            await MarkUpstreamTokenExchanged(upstreamTx, userIdenity, cancellationToken);

            // 7 Create Session cookie for the user and AltinnStudio runtime cookie with JWT

            // 8) Redirect back to the client with code + original state
            return upstreamCallbackResult;
        }

        /// <summary>
        /// Handles refresh of session basded on claims principal
        /// -
        /// </summary>
        public async Task<OidcSession?> HandleSessionRefresh(ClaimsPrincipal principal, CancellationToken ct)
        {
            Claim? sidClaim = principal.Claims.FirstOrDefault(c => c.Type == "sid");
            if (sidClaim == null && _generalSettings.ForceOidc)
            {
                throw new InvalidOperationException("No sid claim present in principal");
            }

            if (sidClaim == null)
            {
                return null;
            }
            
            await _oidcSessionRepo.SlideExpiryToAsync(sidClaim.Value, _timeProvider.GetUtcNow().AddMinutes(_generalSettings.JwtValidityMinutes), ct);
            var session = await _oidcSessionRepo.GetBySidAsync(sidClaim.Value, ct);
            if (session is null && _generalSettings.ForceOidc)
            {
                throw new InvalidOperationException("No valid session found for sid");
            }
            
            return session;
        }

        /// <summary>
        /// Ends an OIDC session based on the provided input.
        /// </summary>
        public async Task<EndSessionResult> EndSessionAsync(EndSessionInput input, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(input);

            // 1) Resolve sid and (optionally) client_id from id_token_hint
            string? cookieSid = input.User?.Claims?.FirstOrDefault(c => c.Type == "sid")?.Value;
            string? hintClientId = null;
            string? hintSid = null;

            if (!string.IsNullOrWhiteSpace(input.IdTokenHint))
            {
                JwtSecurityTokenHandler handler = new();
                if (handler.CanReadToken(input.IdTokenHint))
                {
                    JwtSecurityToken raw = handler.ReadJwtToken(input.IdTokenHint);
                    try
                    {
                        OidcProvider provider = ChooseProviderByIssuer(raw.Issuer);
                        JwtSecurityToken validated = await _upstreamTokenValidator.ValidateTokenAsync(input.IdTokenHint, provider, null, ct);
                        hintClientId = validated.Audiences?.FirstOrDefault();
                        hintSid = validated.Claims.FirstOrDefault(c => c.Type == "sid")?.Value;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Invalid id_token_hint presented to end_session endpoint.");
                    }
                }
            }

            string? sid = cookieSid ?? hintSid;

            if (string.IsNullOrWhiteSpace(sid))
            {
                _logger.LogDebug("EndSession: no sid from cookie or id_token_hint; returning cookie delete only.");
                return new EndSessionResult
                {
                    RedirectUri = new Uri(_generalSettings.SBLLogoutEndpoint), // Redirect to SBL logout as a safe default
                    State = input.State,
                    Cookies = new[]
                    {
                        new CookieInstruction
                        {
                            Name = _generalSettings.JwtCookieName,
                            Value = string.Empty,
                            HttpOnly = true,
                            Secure = true,
                            Path = "/",
                            SameSite = SameSiteMode.Lax,
                            Expires = DateTimeOffset.UnixEpoch
                        },
                        new CookieInstruction
                        {
                            Name = _generalSettings.AltinnSessionCookieName,
                            Value = string.Empty,
                            HttpOnly = true,
                            Secure = true,
                            Path = "/",
                            SameSite = SameSiteMode.Lax,
                            Expires = DateTimeOffset.UnixEpoch
                        }
                    }
                };
            }

            // 2) Validate post_logout_redirect_uri (if provided and if we know the client from the hint)
            // TODO: We need to discuss if we need to support this. What about apps running outside Altinn. 
            Uri? redirect = null;
            if (input.PostLogoutRedirectUri is not null && !string.IsNullOrWhiteSpace(hintClientId))
            {
                OidcClient? client = await _oidcServerClientRepository.GetClientAsync(hintClientId!, ct);
                if (client?.RedirectUris?.Any() == true &&
                    client.RedirectUris.Contains(input.PostLogoutRedirectUri))
                {
                    // Append state if any
                    UriBuilder ub = new UriBuilder(input.PostLogoutRedirectUri);
                    if (!string.IsNullOrWhiteSpace(input.State))
                    {
                        System.Collections.Specialized.NameValueCollection q = System.Web.HttpUtility.ParseQueryString(ub.Query);
                        q["state"] = input.State;
                        ub.Query = q.ToString()!;
                    }

                    redirect = ub.Uri;
                }

                // else: ignore invalid redirect; return OP page
            }

            OidcSession? oidcSession = await _oidcSessionRepo.GetBySidAsync(sid!, ct);
            if (oidcSession != null)
            {
                string issuer = oidcSession.UpstreamIssuer;
                if (issuer.Equals(AuthzConstants.ISSUER_ALTINN_PORTAL, StringComparison.OrdinalIgnoreCase))
                {
                    // Session was created based on Altinn 2 ticket. Redirect back to Altinn 2 for logout
                    redirect = new Uri(_generalSettings.SBLLogoutEndpoint);
                }
                else
                {
                    OidcProvider provider = ChooseProviderByIssuer(issuer);
                    redirect = new Uri(provider.LogoutEndpoint!);
                }
            }

            // 3) Server-side invalidation for this session id
            if (!string.IsNullOrWhiteSpace(sid))
            {
                // Delete session row
                await _oidcSessionRepo.DeleteBySidAsync(sid!, ct);

                // Revoke all refresh tokens for this sid.
                IReadOnlyList<Guid> families = await _refreshTokenRepo.GetFamiliesByOpSidAsync(sid!, ct);
                if (families?.Any() == true)
                {
                    foreach (Guid family in families)
                    {
                        await _refreshTokenRepo.RevokeFamilyAsync(family, "logout", ct);
                    }
                }

                // OPTIONAL: purge any unredeemed authorization codes for this sid
                // await _authorizationCodeRepo.InvalidateBySidAsync(sid!, _timeProvider.GetUtcNow(), ct);
            }

            // 4) Instruct caller to delete the runtime cookie (attributes must match how it was set)
            CookieInstruction deleteRuntime = new()
            {
                Name = _generalSettings.JwtCookieName,
                Value = string.Empty,
                HttpOnly = true,
                Secure = true,
                Path = "/",
                SameSite = SameSiteMode.Lax,

                // Use UnixEpoch or MaxAge=0 consistently
                Expires = DateTimeOffset.UnixEpoch
            };

            CookieInstruction deleteSession = new CookieInstruction
            {
                Name = _generalSettings.AltinnSessionCookieName,
                Value = string.Empty,
                HttpOnly = true,
                Secure = true,
                Path = "/",
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UnixEpoch
            };
            
            return new EndSessionResult
            {
                RedirectUri = redirect,
                State = input.State,
                Cookies = [deleteRuntime, deleteSession]
            };
        }

        /// <summary>
        /// Handles upstream front-channel logout requests.
        /// </summary>
        public async Task<UpstreamFrontChannelLogoutResult> HandleUpstreamFrontChannelLogoutAsync(
    UpstreamFrontChannelLogoutInput input,
    CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(input);

            // 1) Validate issuer against configured upstreams
            // Accept either exact Issuer URL or configured key, depending on what the IdP sends.
            var provider = _oidcProviderSettings.Values.FirstOrDefault(p =>
                string.Equals(p.Issuer, input.Issuer, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p.IssuerKey, input.Issuer, StringComparison.OrdinalIgnoreCase));

            if (provider is null)
            {
                _logger.LogWarning("Upstream front-channel logout ignored: unknown issuer {Issuer}", input.Issuer);
                return new UpstreamFrontChannelLogoutResult { TerminatedSessions = 0 };
            }

            // 2) Find all local sessions tied to this upstream (issuer + upstream sid)
            // We fetch SIDs first (for revocation + cookie comparison), then delete.
            string[] localSids = await _oidcSessionRepo.GetSidsByUpstreamAsync(provider.Issuer, input.UpstreamSid, ct);

            if (localSids.Length == 0)
            {
                _logger.LogInformation("Upstream front-channel logout: no local sessions for issuer={Issuer}, sid={Sid}", provider.Issuer, input.UpstreamSid);
                return new UpstreamFrontChannelLogoutResult { TerminatedSessions = 0 };
            }

            // 3) Delete sessions (idempotent)
            int deleted = await _oidcSessionRepo.DeleteByUpstreamAsync(provider.Issuer, input.UpstreamSid, ct);

            // 4) Revoke refresh tokens / invalidate codes for those SIDs (if you have repos)
            foreach (var sid in localSids)
            {
                IReadOnlyList<Guid> families = await _refreshTokenRepo.GetFamiliesByOpSidAsync(sid, ct);
                
                foreach (Guid familyGuid in families)
                {
                    await _refreshTokenRepo.RevokeFamilyAsync(familyGuid, "frontlogou", ct);
                }

                // await _authorizationCodeRepo.InvalidateBySidAsync(sid, _timeProvider.GetUtcNow(), ct);
            }

            // 5) If this very browser has a cookie principal matching any of these SIDs, clear cookie (best-effort)
            string? principalSid = input.User?.Claims?.FirstOrDefault(c => c.Type == "sid")?.Value;
            List<CookieInstruction> cookies = [];
            if (!string.IsNullOrEmpty(principalSid) && localSids.Contains(principalSid, StringComparer.Ordinal))
            {
                cookies.Add(new CookieInstruction
                {
                    Name = _generalSettings.JwtCookieName,
                    Value = string.Empty,
                    HttpOnly = true,
                    Secure = true,
                    Path = "/",
                    SameSite = SameSiteMode.Lax,
                    Expires = DateTimeOffset.UnixEpoch
                });
            }

            return new UpstreamFrontChannelLogoutResult
            {
                TerminatedSessions = deleted,
                Cookies = cookies
            };
        }

        /// <summary>
        /// Handles the authentication process based on the provided session input.
        /// </summary>
        public async Task<AuthenticateFromSessionResult> HandleAuthenticateFromSessionResult(AuthenticateFromSessionInput sessionInput, CancellationToken ct)
        {
            byte[] handleHash = HashHandle(FromBase64Url(sessionInput.SessionHandle));

            // Try to load session by handle
            OidcSession? oidcSession = await _oidcSessionRepo.GetBySessionHandleAsync(handleHash, ct);
            if (oidcSession is not null
                  && oidcSession.ExpiresAt.HasValue
                  && oidcSession.ExpiresAt.Value > _timeProvider.GetUtcNow())
            { 
                string token = await _tokenService.CreateCookieToken(oidcSession, ct);
                CookieInstruction cookieInstruction
                    = new()
                {
                    Name = _generalSettings.JwtCookieName,
                    Value = token,
                    HttpOnly = true,
                    Secure = true,
                    Path = "/",
                    SameSite = SameSiteMode.Lax
                };

                await _oidcSessionRepo.SlideExpiryToAsync(oidcSession.Sid, _timeProvider.GetUtcNow().AddMinutes(_generalSettings.JwtValidityMinutes), ct);
                return new AuthenticateFromSessionResult
                {
                    Kind = AuthenticateFromSessionResultKind.Success,
                    Cookies = [cookieInstruction]
                };
            }

            return new AuthenticateFromSessionResult()
            {
                Kind = AuthenticateFromSessionResultKind.NoValidSession
            };
        }

        /// <summary>
        /// Handles the authentication process based on the provided Altinn2 ticket input.
        /// </summary>
        public async Task<AuthenticateFromAltinn2TicketResult> HandleAuthenticateFromTicket(AuthenticateFromAltinn2TicketInput sessionInfo, CancellationToken ct)
        {
            UserAuthenticationModel userAuthenticationModel = await _cookieDecryptionService.DecryptTicket(sessionInfo.EncryptedTicket);
            userAuthenticationModel = await IdentifyOrCreateAltinnUser(userAuthenticationModel, null);
            EnrichIdentityFromLegacyValues(userAuthenticationModel);
            AddLocalScopes(userAuthenticationModel);
            (OidcSession session, string sessionHandle) = await CreateOrUpdateOidcSessionFromAltinn2Ticket(sessionInfo, userAuthenticationModel, ct);
            if (session is not null && session.ExpiresAt.HasValue && session.ExpiresAt.Value > _timeProvider.GetUtcNow())
            {
                string token = await _tokenService.CreateCookieToken(session, ct);
                CookieInstruction cookieInstruction
                    = new()
                    {
                        Name = _generalSettings.JwtCookieName,
                        Value = token,
                        HttpOnly = true,
                        Secure = true,
                        Path = "/",
                        SameSite = SameSiteMode.Lax
                    };

                CookieInstruction altinnSessionCookie = new()
                {
                    Name = _generalSettings.AltinnSessionCookieName,
                    Value = sessionHandle,
                    HttpOnly = true,
                    Secure = true,
                    Path = "/",
                    SameSite = SameSiteMode.Lax,
                };

                return new AuthenticateFromAltinn2TicketResult
                {
                    Kind = AuthenticateFromAltinn2TicketResultKind.Success,
                    Cookies = [cookieInstruction, altinnSessionCookie]
                };
            }

            return new AuthenticateFromAltinn2TicketResult()
            {
                Kind = AuthenticateFromAltinn2TicketResultKind.NoValidSession
            };
        }

        private void AddLocalScopes(UserAuthenticationModel userAuthenticationModel)
        {
            string[] localScopes = _generalSettings.DefaultPortalScopes.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (userAuthenticationModel.Scope is null)
            {
                userAuthenticationModel.Scope = string.Join(' ', localScopes);
            }
            else
            {
                HashSet<string> scopes = userAuthenticationModel.Scope.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
                foreach (string s in localScopes)
                {
                    scopes.Add(s);
                }

                userAuthenticationModel.Scope = string.Join(' ', scopes);
            }
        }

        private void EnrichIdentityFromLegacyValues(UserAuthenticationModel model)
        {
            model.Iss = AuthzConstants.ISSUER_ALTINN_PORTAL;
            model.Amr = [AuthenticationHelper.GetAmrFromAuthenticationMethod(model.AuthenticationMethod)];
            model.Acr = AuthenticationHelper.GetAcrForAuthenticationLevel(model.AuthenticationLevel);
            model.TokenIssuer = model.Iss;
            model.TokenSubject = model.PartyUuid.ToString();
        }
 
        private async Task MarkUpstreamTokenExchanged(UpstreamLoginTransaction upstreamTx, UserAuthenticationModel userIdenity, CancellationToken cancellationToken)
        {
            await _upstreamLoginTxRepo.MarkTokenExchangedAsync(
                upstreamTx.UpstreamRequestId,
                issuer: userIdenity.TokenIssuer!,
                sub: userIdenity.TokenSubject!,
                acr: userIdenity.Acr,
                authTime: userIdenity.AuthTime,
                idTokenJti: userIdenity.ExternalSessionId,
                upstreamSid: userIdenity.Sid,
                cancellationToken: cancellationToken);
        }

        private async Task<UserAuthenticationModel> ExtractUserIdentityFromUpstream(UpstreamCallbackInput input, UpstreamLoginTransaction upstreamTx, OidcProvider provider, CancellationToken cancellationToken)
        {
            OidcCodeResponse codeReponse = await _oidcProvider.GetTokens(input.Code!, provider, upstreamTx.UpstreamRedirectUri.ToString(), upstreamTx.CodeVerifier, cancellationToken);
            JwtSecurityToken idToken = await _upstreamTokenValidator.ValidateTokenAsync(codeReponse.IdToken, provider, upstreamTx.Nonce, cancellationToken);
            JwtSecurityToken accesstoken = await _upstreamTokenValidator.ValidateTokenAsync(codeReponse.AccessToken, provider, null, cancellationToken);
            UserAuthenticationModel userIdenity = AuthenticationHelper.GetUserFromToken(idToken, provider, accesstoken);
            return userIdenity;
        }

        /// <summary>
        /// Validates the downstream callback state by loading the original login transaction.
        /// </summary>
        private async Task<(UpstreamCallbackResult? CallbackResultdownstreamValidation, LoginTransaction? LoginTx)> ValidateDownstreamCallbackState(UpstreamCallbackInput input, UpstreamLoginTransaction upstreamTx, CancellationToken cancellationToken)
        {
            LoginTransaction? loginTx = await _loginTxRepo.GetByRequestIdAsync(upstreamTx!.RequestId.Value, cancellationToken);
            if (loginTx is null)
            {
                return (CallbackResultdownstreamValidation: new UpstreamCallbackResult
                {
                    Kind = UpstreamCallbackResultKind.LocalError,
                    StatusCode = 500,
                    LocalErrorMessage = "Downstream transaction not found."
                }, null);
            }

            // Safety: We only ever redirect to the redirect_uri we validated on /authorize
            if (loginTx.RedirectUri is null || !loginTx.RedirectUri.IsAbsoluteUri)
            {
                return (CallbackResultdownstreamValidation: new UpstreamCallbackResult
                {
                    Kind = UpstreamCallbackResultKind.LocalError,
                    StatusCode = 500,
                    LocalErrorMessage = "Stored redirect_uri is invalid."
                }, loginTx);
            }

            // ===== 3) If upstream returned an error, map it back to client immediately =====
            if (!string.IsNullOrWhiteSpace(input.Error))
            {
                // Mark upstream as failed (optional, but recommended)
                //await _upstreamLoginTxRepo.MarkErrorAsync(
                //    upstreamTx.UpstreamRequestId,
                //    error: input.Error!,
                //    errorDescription: input.ErrorDescription,
                //    ct: ct);

                return (CallbackResultdownstreamValidation: new UpstreamCallbackResult
                {
                    Kind = UpstreamCallbackResultKind.ErrorRedirectToClient,
                    ClientRedirectUri = loginTx.RedirectUri,
                    ClientState = loginTx.State,
                    Error = input.Error,
                    ErrorDescription = input.ErrorDescription
                }, loginTx);
            }

            // ===== 4) Require 'code' on success path =====
            if (string.IsNullOrWhiteSpace(input.Code))
            {
                // Missing code → client-facing error
                return (CallbackResultdownstreamValidation: new UpstreamCallbackResult
                {
                    Kind = UpstreamCallbackResultKind.ErrorRedirectToClient,
                    ClientRedirectUri = loginTx.RedirectUri,
                    ClientState = loginTx.State,
                    Error = "access_denied",
                    ErrorDescription = "Missing authorization code in upstream callback."
                }, loginTx);
            }

            return (CallbackResultdownstreamValidation: null, LoginTx: loginTx);
        }

        /// <summary>
        /// Validates the upstream callback state by loading the upstream login transaction.
        /// </summary>
        private async Task<(UpstreamCallbackResult? CallbackResult, UpstreamLoginTransaction? UpstreamTranscation)> ValidateUpstreamCallbackState(UpstreamCallbackInput input, CancellationToken cancellationToken)
        {
            // ===== 0) Guard / basic semantics =====
            ArgumentNullException.ThrowIfNull(input);

            // We expect either success (code+state) or error(+state). State is required in both.
            if (string.IsNullOrWhiteSpace(input.State))
            {
                return (CallbackResult: new UpstreamCallbackResult
                {
                    Kind = UpstreamCallbackResultKind.LocalError,
                    StatusCode = 400,
                    LocalErrorMessage = "Missing 'state' in upstream callback."
                }, UpstreamTranscation: null);
            }

            // ===== 1) Load upstream login transaction by state =====
            UpstreamLoginTransaction? upstreamTx = await _upstreamLoginTxRepo.GetForCallbackByStateAsync(input.State, cancellationToken);
            if (upstreamTx is null)
            {
                // We don't know where to redirect safely; local error.
                return (CallbackResult: new UpstreamCallbackResult
                {
                    Kind = UpstreamCallbackResultKind.LocalError,
                    StatusCode = 400,
                    LocalErrorMessage = "Unknown or expired upstream state."
                }, UpstreamTranscation: upstreamTx);
            }

            // Quickly reject non-pending or expired
            if (!string.Equals(upstreamTx.Status, "pending", StringComparison.OrdinalIgnoreCase))
            {
                return (CallbackResult: new UpstreamCallbackResult
                {
                    Kind = UpstreamCallbackResultKind.LocalError,
                    StatusCode = 400,
                    LocalErrorMessage = "Upstream transaction is not pending."
                }, upstreamTx);
            }

            if (upstreamTx.ExpiresAt <= _timeProvider.GetUtcNow())
            {
                return (CallbackResult: new UpstreamCallbackResult
                {
                    Kind = UpstreamCallbackResultKind.LocalError,
                    StatusCode = 400,
                    LocalErrorMessage = "Upstream transaction has expired."
                }, upstreamTx);
            }

            return (CallbackResult: null, UpstreamTranscation: upstreamTx);
        }

        private async Task<(OidcProvider Provider, string UpstreamState, string UpstreamNonce, string UpstreamPkceChallenge)> CreateUpstreamLoginTransaction(AuthorizeRequest request, LoginTransaction tx, CancellationToken cancellationToken)
        {
            OidcProvider provider = ChooseProvider(request);

            string upstreamState = CryptoHelpers.RandomBase64Url(32);
            string upstreamNonce = CryptoHelpers.RandomBase64Url(32);
            string upstreamPkceVerifier = Pkce.RandomPkceVerifier();
            string upstreamPkceChallenge = Hashing.Sha256Base64Url(upstreamPkceVerifier);

            // ========= 7) Persist login_transaction_upstream =========
            // NOTE: Store everything you need for callback + token exchange.
            UpstreamLoginTransactionCreate upstreamCreate = new()
            {
                RequestId = tx.RequestId,
                ExpiresAt = _timeProvider.GetUtcNow().AddMinutes(10),
               
                Provider = provider.IssuerKey ?? provider.Issuer, // stable key for routing/ops
                UpstreamClientId = provider.ClientId,
                AuthorizationEndpoint = new Uri(provider.AuthorizationEndpoint),
                TokenEndpoint = new Uri(provider.TokenEndpoint),
                JwksUri = string.IsNullOrWhiteSpace(provider.WellKnownConfigEndpoint) ? null : new Uri(provider.WellKnownConfigEndpoint), // keep null unless you decide to pin

                UpstreamRedirectUri = BuildUpstreamRedirectUri(provider, provider.IssuerKey),

                State = upstreamState,
                Nonce = upstreamNonce,
                Scopes = request.Scopes,
                AcrValues = request.AcrValues?.Length > 0 ? request.AcrValues : null,
                Prompts = request.Prompts?.Length > 0 ? request.Prompts : null,
                UiLocales = request.UiLocales?.Length > 0 ? request.UiLocales : null,
                MaxAge = request.MaxAge,

                CodeVerifier = upstreamPkceVerifier,
                CodeChallenge = upstreamPkceChallenge,
                CodeChallengeMethod = "S256",

                CorrelationId = request.CorrelationId,
                CreatedByIp = request.ClientIp,
                UserAgentHash = request.UserAgentHash
            };

            _ = await _upstreamLoginTxRepo.InsertAsync(upstreamCreate, cancellationToken);
            return (provider, upstreamState, upstreamNonce, upstreamPkceChallenge);
        }

        private async Task<(string UpstreamState, string UpstreamNonce, string UpstreamPkceChallenge)> CreateUpstreamLoginTransaction(UnregisteredClientRequestCreate request, OidcProvider provider, CancellationToken cancellationToken)
        {
            string upstreamState = CryptoHelpers.RandomBase64Url(32);
            string upstreamNonce = CryptoHelpers.RandomBase64Url(32);
            string upstreamPkceVerifier = Pkce.RandomPkceVerifier();
            string upstreamPkceChallenge = Hashing.Sha256Base64Url(upstreamPkceVerifier);

            // ========= 7) Persist login_transaction_upstream =========
            // NOTE: Store everything you need for callback + token exchange.
            UpstreamLoginTransactionCreate upstreamCreate = new()
            {
                UnregisteredClientRequestId = request.RequestId, 
                ExpiresAt = _timeProvider.GetUtcNow().AddMinutes(10),

                Provider = provider.IssuerKey ?? provider.Issuer, // stable key for routing/ops
                UpstreamClientId = provider.ClientId,
                AuthorizationEndpoint = new Uri(provider.AuthorizationEndpoint),
                TokenEndpoint = new Uri(provider.TokenEndpoint),
                JwksUri = string.IsNullOrWhiteSpace(provider.WellKnownConfigEndpoint) ? null : new Uri(provider.WellKnownConfigEndpoint), // keep null unless you decide to pin

                UpstreamRedirectUri = BuildUpstreamRedirectUri(provider, provider.IssuerKey),

                State = upstreamState,
                Nonce = upstreamNonce,
                Scopes = provider.Scope.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),

                CodeVerifier = upstreamPkceVerifier,
                CodeChallenge = upstreamPkceChallenge,
                CodeChallengeMethod = "S256",

                CorrelationId = request.CorrelationId,
                CreatedByIp = request.CreatedByIp,
                UserAgentHash = request.UserAgentHash
            };

            _ = await _upstreamLoginTxRepo.InsertAsync(upstreamCreate, cancellationToken);
            return (upstreamState, upstreamNonce, upstreamPkceChallenge);
        }

        private async Task<LoginTransaction> PersistLoginTransaction(AuthorizeRequest request, OidcClient client, CancellationToken cancellationToken)
        {
            LoginTransactionCreate transaction = new()
            {
                ClientId = client.ClientId,
                RedirectUri = request.RedirectUri!,
                Scopes = request.Scopes!,
                State = request.State!,
                Nonce = request.Nonce!,
                AcrValues = request.AcrValues,
                Prompts = request.Prompts,
                UiLocales = request.UiLocales,
                MaxAge = request.MaxAge,
                CodeChallenge = request.CodeChallenge!,
                CodeChallengeMethod = request.CodeChallengeMethod ?? "S256",
                RequestUri = request.RequestUri,
                RequestObjectJwt = request.RequestObject, // if you keep it
                AuthorizationDetailsJson = null,
                CreatedByIp = request.ClientIp,                           // captured in controller
                UserAgentHash = request.UserAgentHash,
                CorrelationId = request.CorrelationId,
                ExpiresAt = TimeProvider.System.GetUtcNow().AddMinutes(10)
            };

            LoginTransaction tx = await _loginTxRepo.InsertAsync(transaction, cancellationToken);
            return tx;
        }

        private async Task<string> CreateDownstreamAuthorizationCode(UpstreamLoginTransaction? upstreamTx, LoginTransaction loginTx, OidcSession session, CancellationToken cancellationToken)
        {
            string authCode = CryptoHelpers.RandomBase64Url(32);
            DateTimeOffset codeTime = _timeProvider.GetUtcNow();
            DateTimeOffset codeExpires = codeTime.AddSeconds(120);

            await _authorizationCodeRepo.InsertAsync(
                new AuthorizationCodeCreate
                {
                    Code = authCode,
                    ClientId = loginTx.ClientId,
                    SubjectId = session.SubjectId, // fallback
                    ExternalId = session.ExternalId,    
                    SubjectPartyUuid = session.SubjectPartyUuid,
                    SubjectPartyId = session.SubjectPartyId,
                    SubjectUserId = session.SubjectUserId,
                    SubjectUserName = session.SubjectUserName,
                    SessionId = session.Sid,
                    RedirectUri = loginTx.RedirectUri,
                    Scopes = session.Scopes,
                    Nonce = loginTx.Nonce,
                    Acr = session.Acr,
                    Amr = session.Amr,
                    AuthTime = session.AuthTime,
                    CodeChallenge = loginTx.CodeChallenge,
                    CodeChallengeMethod = loginTx.CodeChallengeMethod ?? "S256",
                    IssuedAt = codeTime,
                    ExpiresAt = codeExpires,
                    CreatedByIp = upstreamTx?.CreatedByIp,
                    CorrelationId = upstreamTx?.CorrelationId,
                    ProviderClaims = session.ProviderClaims
                },
                cancellationToken);
            return authCode;
        }

        /// <summary>
        /// Creates or updates an OIDC session based on the upstream identity.
        /// </summary>
        private async Task<(OidcSession OidcSession, string SessionHandle)> CreateOrUpdateOidcSession(UpstreamLoginTransaction upstreamTx, UserAuthenticationModel userIdenity, CancellationToken cancellationToken)
        {
            string[]? scopes = [];
            if (!string.IsNullOrWhiteSpace(userIdenity.Scope))
            {
                scopes = userIdenity.Scope.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            }

            // 1) Generate 256-bit random handle for the cookie
            byte[] handleBytes = RandomNumberGenerator.GetBytes(32);
            string sessionHandle = ToBase64Url(handleBytes); // this is what you'll set as the cookie value

            // 2) Hash it for storage (HMAC with server-side pepper)
            byte[] handleHash = HashHandle(handleBytes);

            string? externalId = null;
            if (!string.IsNullOrEmpty(userIdenity.SSN))
            {
                externalId = $"{AltinnCoreClaimTypes.PersonIdentifier}:{userIdenity.SSN}";
            }
            else if (!string.IsNullOrEmpty(userIdenity.ExternalIdentity))
            {
                externalId = userIdenity.ExternalIdentity;
            }

            OidcSession session = await _oidcSessionRepo.UpsertByUpstreamSubAsync(
                    new OidcSessionCreate
                    {
                        Sid = CryptoHelpers.RandomBase64Url(32),
                        SessionHandleHash = handleHash, // store hash only
                        Provider = upstreamTx.Provider,
                        UpstreamIssuer = userIdenity.TokenIssuer!,
                        UpstreamSub = userIdenity.TokenSubject!,
                        SubjectId = $"{AltinnCoreClaimTypes.PartyUUID}:{userIdenity.PartyUuid}",
                        SubjectPartyUuid = userIdenity.PartyUuid,            // <- Altinn GUID
                        SubjectPartyId = userIdenity.PartyID,              // <- legacy
                        SubjectUserId = userIdenity.UserID,
                        SubjectUserName = userIdenity.Username,  // <- legacy
                        ExternalId = externalId,
                        Acr = userIdenity.Acr,
                        AuthTime = userIdenity.AuthTime,
                        Amr = userIdenity.Amr,
                        Scopes = scopes,
                        ExpiresAt = _timeProvider.GetUtcNow().AddMinutes(_generalSettings.JwtValidityMinutes),
                        UpstreamSessionSid = userIdenity.Sid,
                        Now = _timeProvider.GetUtcNow(),
                        CreatedByIp = upstreamTx.CreatedByIp,
                        UserAgentHash = upstreamTx.UserAgentHash,
                        ProviderClaims = userIdenity.ProviderClaims,
                    },
                    cancellationToken);
            return (session, sessionHandle);
        }

        /// <summary>
        /// Method that creates or updates an OIDC session based on the Altinn2 ticket identity.
        /// This can be deleted in the future when Altinn2 is decommissioned.
        /// </summary>
        private async Task<(OidcSession OidcSession, string SessionHandle)> CreateOrUpdateOidcSessionFromAltinn2Ticket(AuthenticateFromAltinn2TicketInput authInput,  UserAuthenticationModel userIdenity, CancellationToken cancellationToken)
        {
            string[]? scopes = [];
            if (!string.IsNullOrWhiteSpace(userIdenity.Scope))
            {
                scopes = userIdenity.Scope.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            }

            // 1) Generate 256-bit random handle for the cookie
            byte[] handleBytes = RandomNumberGenerator.GetBytes(32);
            string sessionHandle = ToBase64Url(handleBytes); // this is what you'll set as the cookie value

            // 2) Hash it for storage (HMAC with server-side pepper)
            byte[] handleHash = HashHandle(handleBytes);

            string? externalId = null;

            if (!string.IsNullOrEmpty(userIdenity.SSN))
            {
                externalId = $"{AltinnCoreClaimTypes.PersonIdentifier}:{userIdenity.SSN}";
            }

            OidcSession session = await _oidcSessionRepo.UpsertByUpstreamSubAsync(
                new OidcSessionCreate
                {
                    Sid = CryptoHelpers.RandomBase64Url(32),
                    SessionHandleHash = handleHash, // store hash only
                    Provider = "altinn2",
                    UpstreamIssuer = userIdenity.TokenIssuer!,
                    UpstreamSub = userIdenity.TokenSubject!,
                    SubjectId = $"{AltinnCoreClaimTypes.PartyUUID}:{userIdenity.PartyUuid}",
                    ExternalId = externalId,
                    SubjectPartyUuid = userIdenity.PartyUuid,            // <- Altinn GUID
                    SubjectPartyId = userIdenity.PartyID,              // <- legacy
                    SubjectUserId = userIdenity.UserID,    
                    SubjectUserName = userIdenity.Username,  // <- legacy
                    Acr = userIdenity.Acr,
                    AuthTime = userIdenity.AuthTime,
                    Amr = userIdenity.Amr,
                    Scopes = scopes,
                    ExpiresAt = _timeProvider.GetUtcNow().AddMinutes(_generalSettings.JwtValidityMinutes),
                    UpstreamSessionSid = userIdenity.Sid,
                    Now = _timeProvider.GetUtcNow(),
                    CreatedByIp = authInput.CreatedByIp,
                    UserAgentHash = authInput.UserAgentHash
                },
                cancellationToken);
            return (session, sessionHandle);
        }

        private static AuthorizeResult Fail(AuthorizeRequest req, AuthorizeValidationError e, OidcClient? oidcClient)
        {
            // If we can safely redirect back, do an OIDC error redirect; else local error.
            if (oidcClient != null && req.RedirectUri is not null && req.RedirectUri.IsAbsoluteUri)
            {
                return AuthorizeResult.ErrorRedirect(req.RedirectUri, e.Error, e.Description, req.State);
            }

            return AuthorizeResult.LocalError(400, e.Error, e.Description);
        }

        private static readonly Regex ProviderKeyRegex = new(@"^[a-z0-9._-]+$", RegexOptions.Compiled);

        private static string ToBase64Url(byte[] bytes) =>
            WebEncoders.Base64UrlEncode(bytes);

        private static byte[] FromBase64Url(string s) =>
            WebEncoders.Base64UrlDecode(s);

        private byte[] HashHandle(byte[] handleBytes)
        {
            using HMACSHA256 hmac = new(DecodePepper(_generalSettings.OidcRefreshTokenPepper));
            return hmac.ComputeHash(handleBytes); // 32 bytes
        }

        private OidcProvider ChooseProvider(AuthorizeRequest req)
        {
            // 1) Try map ACR → provider key
            string? key = GetIdProviderFromAcr(req.AcrValues ?? Array.Empty<string>());

            // 3) Try explicit key first
            if (!string.IsNullOrEmpty(key) && _oidcProviderSettings.TryGetValue(key, out var selected))
            {
                _logger.LogDebug("OIDC upstream provider selected via acr mapping: {ProviderKey}", key);
                return selected;
            }

            // 4) Fallback to configured default ('idporten')
            if (_oidcProviderSettings.TryGetValue(DefaultProviderKey, out var defaultIdp))
            {
                _logger.LogDebug("OIDC upstream provider defaulted to 'idporten'.");
                return defaultIdp;
            }

            // 5) No match → OIDC-style failure (surface as 'server_error' from /authorize)
            _logger.LogError(
                "No default OIDC provider configured. Known providers: {Keys}",
                string.Join(",", _oidcProviderSettings.Keys));
            throw new ApplicationException("server_error No default OIDC provider configured.");
        }

        private OidcProvider ChooseProvider(AuthorizeUnregisteredClientRequest req)
        {
            if (req.RequestedIss is not null)
            {
                foreach (var p in _oidcProviderSettings.Values)
                {
                    if (string.Equals(p.Issuer, req.RequestedIss, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(p.IssuerKey, req.RequestedIss, StringComparison.OrdinalIgnoreCase))
                    {
                        return p;
                    }
                }
            }

            // 1) Try map ACR → provider key
            string? key = GetIdProviderFromAcr(req.AcrValues ?? Array.Empty<string>());

            // 3) Try explicit key first
            if (!string.IsNullOrEmpty(key) && _oidcProviderSettings.TryGetValue(key, out var selected))
            {
                _logger.LogDebug("OIDC upstream provider selected via acr mapping: {ProviderKey}", key);
                return selected;
            }

            // 4) Fallback to configured default ('idporten')
            if (_oidcProviderSettings.TryGetValue(DefaultProviderKey, out var defaultIdp))
            {
                _logger.LogDebug("OIDC upstream provider defaulted to 'idporten'.");
                return defaultIdp;
            }

            // 5) No match → OIDC-style failure (surface as 'server_error' from /authorize)
            _logger.LogError(
                "No default OIDC provider configured. Known providers: {Keys}",
                string.Join(",", _oidcProviderSettings.Keys));
            throw new ApplicationException("server_error No default OIDC provider configured.");
        }

        private OidcProvider ChooseProviderByKey(string? key)
        {
            if (!string.IsNullOrWhiteSpace(key) && _oidcProviderSettings.TryGetValue(key, out var selected))
            {
                 return selected;
            }

            throw new ArgumentException("Invalid or unknown provider key.", nameof(key));
        }

        private OidcProvider ChooseProviderByIssuer(string issuer)
        {
            foreach (var p in _oidcProviderSettings.Values)
            {
                if (string.Equals(p.Issuer, issuer, StringComparison.OrdinalIgnoreCase))
                {
                    return p;
                }
            }

            throw new ArgumentException("Invalid or unknown provider issuer.", nameof(issuer));
        }

        private static string GetIdProviderFromAcr(string[]? acrValues)
        {
            if (acrValues is null || acrValues.Length == 0)
            {
                return DefaultProviderKey;
            }

            var set = new HashSet<string>(acrValues, StringComparer.OrdinalIgnoreCase);

            // IdPorten (high/substantial still go to the same upstream)
            if (set.Contains("idporten-loa-high") || set.Contains("idporten-loa-substantial"))
            {
                return "idporten";
            }

            if (set.Contains("selfregistered-email"))
            {
                return "idporten";
            }

            // UDIR/UIDP
            if (set.Contains("uidp"))
            {
                return "uidp";
            }

            return DefaultProviderKey;
        }

        private static Uri BuildUpstreamAuthorizeUrl(
                OidcProvider p,
                string upstreamState,
                string upstreamNonce,
                string upstreamCodeChallenge,
                AuthorizeRequest incoming,
                string? issKeyForCallback)
        {
            var q = System.Web.HttpUtility.ParseQueryString(string.Empty);
            q["response_type"] = string.IsNullOrWhiteSpace(p.ResponseType) ? "code" : p.ResponseType;
            q["client_id"] = p.ClientId;
            q["redirect_uri"] = BuildUpstreamRedirectUri(p, issKeyForCallback).ToString();
            q["scope"] = string.IsNullOrWhiteSpace(p.Scope) ? "openid" : p.Scope;

            q["state"] = upstreamState;
            q["nonce"] = upstreamNonce;
            q["code_challenge"] = upstreamCodeChallenge;
            q["code_challenge_method"] = "S256";

            if (incoming.AcrValues is { Length: > 0 })
            {
                q["acr_values"] = string.Join(' ', incoming.AcrValues);
            }

            if (incoming.Prompts is { Length: > 0 })
            {
                q["prompt"] = string.Join(' ', incoming.Prompts);
            }

            if (incoming.UiLocales is { Length: > 0 })
            {
                q["ui_locales"] = string.Join(' ', incoming.UiLocales);
            }

            if (incoming.MaxAge is not null)
            {
                q["max_age"] = incoming.MaxAge.Value.ToString();
            }

            var ub = new UriBuilder(p.AuthorizationEndpoint) { Query = q.ToString()! };
            return ub.Uri;
        }

        private static Uri BuildUpstreamAuthorizeUrl(
               OidcProvider p,
               string upstreamState,
               string upstreamNonce,
               string upstreamCodeChallenge,
               AuthorizeUnregisteredClientRequest incoming,
               string? issKeyForCallback)
        {
            var q = System.Web.HttpUtility.ParseQueryString(string.Empty);
            q["response_type"] = string.IsNullOrWhiteSpace(p.ResponseType) ? "code" : p.ResponseType;
            q["client_id"] = p.ClientId;
            q["redirect_uri"] = BuildUpstreamRedirectUri(p, issKeyForCallback).ToString();
            q["scope"] = string.IsNullOrWhiteSpace(p.Scope) ? "openid" : p.Scope;

            q["state"] = upstreamState;
            q["nonce"] = upstreamNonce;
            q["code_challenge"] = upstreamCodeChallenge;
            q["code_challenge_method"] = "S256";

            if (incoming.AcrValues is { Length: > 0 })
            {
                q["acr_values"] = string.Join(' ', incoming.AcrValues);
            }

            var ub = new UriBuilder(p.AuthorizationEndpoint) { Query = q.ToString()! };
            return ub.Uri;
        }

        private static Uri BuildUpstreamRedirectUri(OidcProvider p, string? issKey)
        {
            // Your fixed upstream callback base:
            var baseCallback = new Uri("https://platform.altinn.no/authentication/api/v1/upstream/callback");

            // If you want per-provider routing, append ?iss=key when configured
            if (p.IncludeIssInRedirectUri && !string.IsNullOrWhiteSpace(issKey))
            {
                var q = System.Web.HttpUtility.ParseQueryString(string.Empty);
                q["iss"] = issKey;
                var ub = new UriBuilder(baseCallback) { Query = q.ToString()! };
                return ub.Uri;
            }

            return baseCallback;
        }

        private async Task<UserAuthenticationModel> IdentifyOrCreateAltinnUser(UserAuthenticationModel userAuthenticationModel, OidcProvider provider)
        {
            ArgumentNullException.ThrowIfNull(userAuthenticationModel);

            if (userAuthenticationModel != null && userAuthenticationModel.UserID != null && userAuthenticationModel.PartyID != null && userAuthenticationModel.PartyUuid != null)
            {
                return userAuthenticationModel;
            }

            UserProfile profile;

            if (!string.IsNullOrEmpty(userAuthenticationModel!.SSN))
            {
                profile = await _profileService.GetUserProfile(new UserProfileLookup { Ssn = userAuthenticationModel.SSN });
                userAuthenticationModel.PartyUuid = profile.UserUuid;
                if (profile.PartyId != 0)
                {
                    userAuthenticationModel.PartyID = profile.PartyId;
                }

                if (profile.UserId != 0)
                {
                    userAuthenticationModel.UserID = profile.UserId;
                }

                if (profile.Party.PartyUuid != null)
                {
                    userAuthenticationModel.PartyUuid = profile.Party.PartyUuid;
                }
            }
            else if (!string.IsNullOrEmpty(userAuthenticationModel.ExternalIdentity))
            {
                string issExternalIdentity = userAuthenticationModel.Iss + ":" + userAuthenticationModel.ExternalIdentity;
                profile = await _userProfileService.GetUser(issExternalIdentity);

                if (profile != null)
                {
                    userAuthenticationModel.UserID = profile.UserId;
                    userAuthenticationModel.PartyID = profile.PartyId;
                    userAuthenticationModel.PartyUuid = profile.Party.PartyUuid;
                    userAuthenticationModel.Username = profile.UserName;
                    userAuthenticationModel.Amr = ["SelfIdentified"];
                    userAuthenticationModel.Acr = "Selfidentified";
                    return userAuthenticationModel;
                }

                UserProfile userToCreate = new()
                {
                    ExternalIdentity = issExternalIdentity,
                    UserName = CreateUserName(userAuthenticationModel, provider),
                    UserType = Profile.Enums.UserType.SelfIdentified
                };

                UserProfile userCreated = await _userProfileService.CreateUser(userToCreate);
                userAuthenticationModel.UserID = userCreated.UserId;
                userAuthenticationModel.PartyID = userCreated.PartyId;
                userAuthenticationModel.PartyUuid = userCreated.Party.PartyUuid;
                userAuthenticationModel.Amr = ["SelfIdentified"];
                userAuthenticationModel.Acr = "Selfidentified";
            }
            else if (userAuthenticationModel.UserID.HasValue && userAuthenticationModel.UserID.Value > 0)
            {
                profile = await _profileService.GetUserProfile(new UserProfileLookup { UserId = userAuthenticationModel.UserID.Value });
                userAuthenticationModel.PartyUuid = profile.UserUuid;
                if (profile.PartyId != 0)
                {
                    userAuthenticationModel.PartyID = profile.PartyId;
                    userAuthenticationModel.PartyUuid = profile.Party.PartyUuid;
                    userAuthenticationModel.SSN = profile.Party.SSN;
                    return userAuthenticationModel;
                }
            }
            
            return userAuthenticationModel;
        }

        private static string CreateUserName(UserAuthenticationModel userAuthenticationModel, OidcProvider provider)
        {
            string hashedIdentity = HashNonce(userAuthenticationModel.ExternalIdentity).Substring(5, 10);
            Regex rgx = new Regex("[^a-zA-Z0-9 -]");
            hashedIdentity = rgx.Replace(hashedIdentity, string.Empty);

            return provider.UserNamePrefix + hashedIdentity.ToLower() + DateTime.Now.Millisecond;
        }

        private static string HashNonce(string nonce)
        {
            using (SHA256 nonceHash = SHA256.Create())
            {
                byte[] byteArrayResultOfRawData = Encoding.UTF8.GetBytes(nonce);
                byte[] byteArrayResult = nonceHash.ComputeHash(byteArrayResultOfRawData);
                return Convert.ToBase64String(byteArrayResult);
            }
        }

        private static byte[] DecodePepper(string value)
        {
            try
            {
                return Convert.FromBase64String(value);
            }
            catch (FormatException)
            {
                // Try Base64Url normalization
                string normalized = value.Replace('-', '+').Replace('_', '/');
                int pad = (4 - (normalized.Length % 4)) % 4;
                normalized = normalized + new string('=', pad);
                try { return Convert.FromBase64String(normalized); }
                catch (Exception ex) { throw new ApplicationException("Invalid OidcRefreshTokenPepper; must be Base64/Base64Url.", ex); }
            }
        }
    }
}
