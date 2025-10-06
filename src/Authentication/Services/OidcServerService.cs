#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Core.Helpers;
using Altinn.Platform.Authentication.Core.Models.Oidc;
using Altinn.Platform.Authentication.Core.RepositoryInterfaces;
using Altinn.Platform.Authentication.Core.Services.Interfaces;
using Altinn.Platform.Authentication.Helpers;
using Altinn.Platform.Authentication.Model;
using Altinn.Platform.Authentication.Services.Interfaces;
using Altinn.Platform.Profile.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
        ITokenService tokenService) : IOidcServerService
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
        private static readonly string DefaultProviderKey = "idporten";

        /// <summary>
        /// Handles an incoming OIDC <c>/authorize</c> request from a Downstream client in the Altinn Platform.
        /// This can be Arbeidsflate or other application.
        /// Identifes the correct Upstream ID Provider like ID-porten, UIDP, Testlogin or other configured provider
        /// Stores downstream login transaction and upstream transaction before redirecting to the correct upstream ID-provider
        /// </summary>
        public async Task<AuthorizeResult> Authorize(AuthorizeRequest request, CancellationToken cancellationToken)
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

            // ========= 4) Existing IdP session reuse (optional optimization) =========
            // TODO: try locate valid oidc_session for (client_id, subject) meeting acr/max_age
            // TODO: if reusable and no prompt=login: proceed to issue downstream authorization_code (future extension)

            // ========= 5) Persist login_transaction(downstream) =========
            LoginTransaction tx = await PersistLoginTransaction(request, client, cancellationToken);

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

        /// <inheritdoc/>
        public async Task<UpstreamCallbackResult> HandleUpstreamCallback(UpstreamCallbackInput input, CancellationToken cancellationToken)
        {
            // ===== 1) Validate input + load upstream transaction =====
            (UpstreamCallbackResult? callbackResultUpstreamValidation, UpstreamLoginTransaction? upstreamTx) = await ValidateUpstreamCallbackState(input, cancellationToken);
            if (callbackResultUpstreamValidation != null)
            {
                return callbackResultUpstreamValidation;
            }

            Debug.Assert(upstreamTx != null);

            // ===== 2) Load downstream (original) transaction to get validated redirect_uri & original state =====
            (UpstreamCallbackResult? downStreamValidationResult, LoginTransaction? loginTx) = await ValidateDownstreamCallbackState(input, upstreamTx, cancellationToken);
            if (downStreamValidationResult != null)
            {
                return downStreamValidationResult;
            }

            Debug.Assert(loginTx != null);

            // ===== 3) Exchange upstream code for upstream tokens =====
            OidcProvider provider = ChooseProviderByKey(upstreamTx.Provider);
            UserAuthenticationModel userIdenity = await ExtractUserIdentityFromUpstream(input, upstreamTx, provider, cancellationToken);
            await IdentifyOrCreateAltinnUser(userIdenity, provider);

            // 4. Create or refresh Altinn session session
            OidcSession session = await CreateOrUpdateOidcSession(upstreamTx, userIdenity, cancellationToken);

            // TODO How to handle first time login with epost bruker from ID porten if we gonna ask them to connect to existing self identified user.

            // 5) Issue downstream authorization code
            string authCode = await CreateDownstreamAuthorizationCode(upstreamTx, loginTx, userIdenity, session, cancellationToken);

            // 6) Mark upstream transaction as completed
            await MarkUpstreamTokenExchanged(upstreamTx, userIdenity, cancellationToken);

            // 7 Create Session cookie for the user and AltinnStudio runtime cookie with JWT
            string cookieToken = await _tokenService.CreateCookieToken(session, cancellationToken);

            CookieInstruction altinnStudioRuntime = new()
            { 
                Name = "AltinnStudioRuntime", 
                Value = cookieToken, 
                HttpOnly = true, 
                Secure = true, 
                Path = "/", 
                SameSite = SameSiteMode.Lax
            };

            // 8) Redirect back to the client with code + original state
            return new UpstreamCallbackResult
            {
                Kind = UpstreamCallbackResultKind.RedirectToClient,
                ClientRedirectUri = loginTx!.RedirectUri,
                DownstreamCode = authCode,
                ClientState = loginTx.State,
                Cookies = [altinnStudioRuntime]
            };
        }

        /// <summary>
        /// Handles refresh of session basded on claims principal
        /// -
        /// </summary>
        public Task<OidcSession?> HandleSessionRefresh(ClaimsPrincipal principal, CancellationToken ct)
        {
            Claim? sidClaim = principal.Claims.FirstOrDefault(c => c.Type == "sid");
            if (sidClaim == null && _generalSettings.ForceOidc)
            {
                throw new InvalidOperationException("No sid claim present in principal");
            }
            else if (sidClaim == null)
            {
                // TODO: Need to consider this scenario. 
                return Task.FromResult<OidcSession?>(null);
            }

            _oidcSessionRepo.SlideExpiryToAsync(sidClaim!.Value, _timeProvider.GetUtcNow().AddMinutes(_generalSettings.JwtValidityMinutes), ct);
            return _oidcSessionRepo.GetBySidAsync(sidClaim.Value, ct) ?? throw new InvalidOperationException("No valid session found for sid");
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
            LoginTransaction? loginTx = await _loginTxRepo.GetByRequestIdAsync(upstreamTx!.RequestId, cancellationToken);
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
            var upstreamCreate = new UpstreamLoginTransactionCreate
            {
                RequestId = tx.RequestId,
                ExpiresAt = _timeProvider.GetUtcNow().AddMinutes(10),

                Provider = provider.IssuerKey ?? provider.Issuer, // stable key for routing/ops
                UpstreamClientId = provider.ClientId,
                AuthorizationEndpoint = new Uri(provider.AuthorizationEndpoint),
                TokenEndpoint = new Uri(provider.TokenEndpoint),
                JwksUri = string.IsNullOrWhiteSpace(provider.WellKnownConfigEndpoint) ? null : null, // keep null unless you decide to pin

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

        private async Task<string> CreateDownstreamAuthorizationCode(UpstreamLoginTransaction upstreamTx, LoginTransaction loginTx, UserAuthenticationModel userIdenity, OidcSession session, CancellationToken cancellationToken)
        {
            string authCode = CryptoHelpers.RandomBase64Url(32);
            var codeTime = _timeProvider.GetUtcNow();
            var codeExpires = codeTime.AddSeconds(120);

            await _authorizationCodeRepo.InsertAsync(
                new AuthorizationCodeCreate
                {
                    Code = authCode,
                    ClientId = loginTx.ClientId,
                    SubjectId = session.SubjectId ?? userIdenity.ExternalIdentity, // fallback
                    SubjectPartyUuid = session.SubjectPartyUuid,
                    SubjectPartyId = session.SubjectPartyId,
                    SubjectUserId = session.SubjectUserId,
                    SessionId = session.Sid,
                    RedirectUri = loginTx.RedirectUri,
                    Scopes = loginTx.Scopes,
                    Nonce = loginTx.Nonce,
                    Acr = userIdenity.Acr,
                    Amr = userIdenity.Amr,
                    AuthTime = userIdenity.AuthTime,
                    CodeChallenge = loginTx.CodeChallenge,
                    CodeChallengeMethod = loginTx.CodeChallengeMethod ?? "S256",
                    IssuedAt = codeTime,
                    ExpiresAt = codeExpires,
                    CreatedByIp = upstreamTx.CreatedByIp,
                    CorrelationId = upstreamTx.CorrelationId
                },
                cancellationToken);
            return authCode;
        }

        /// <summary>
        /// Creates or updates an OIDC session based on the upstream identity.
        /// </summary>
        private async Task<OidcSession> CreateOrUpdateOidcSession(UpstreamLoginTransaction upstreamTx, UserAuthenticationModel userIdenity, CancellationToken cancellationToken)
        {
            string[]? scopes = [];
            if (!string.IsNullOrWhiteSpace(userIdenity.Scope))
            {
                scopes = userIdenity.Scope.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            }

            OidcSession session = await _oidcSessionRepo.UpsertByUpstreamSubAsync(
                new OidcSessionCreate
                {
                    Sid = CryptoHelpers.RandomBase64Url(32),
                    Provider = upstreamTx.Provider,
                    UpstreamIssuer = userIdenity.TokenIssuer!,
                    UpstreamSub = userIdenity.TokenSubject!,
                    SubjectId = userIdenity.SSN ?? userIdenity.ExternalIdentity,   // <- string PID/email/etc
                    SubjectPartyUuid = userIdenity.PartyUuid,            // <- Altinn GUID
                    SubjectPartyId = userIdenity.PartyID,              // <- legacy
                    SubjectUserId = userIdenity.UserID,               // <- legacy
                    Acr = userIdenity.Acr,
                    AuthTime = userIdenity.AuthTime,
                    Amr = userIdenity.Amr,
                    Scopes = scopes,
                    ExpiresAt = _timeProvider.GetUtcNow().AddMinutes(_generalSettings.JwtValidityMinutes),
                    UpstreamSessionSid = userIdenity.Sid,
                    Now = _timeProvider.GetUtcNow(),
                    CreatedByIp = upstreamTx.CreatedByIp,
                    UserAgentHash = upstreamTx.UserAgentHash
                },
                cancellationToken);
            return session;
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

        private OidcProvider ChooseProviderByKey(string? key)
        {
            if (!string.IsNullOrWhiteSpace(key) && _oidcProviderSettings.TryGetValue(key, out var selected))
            {
                 return selected;
            }

            throw new ArgumentException("Invalid or unknown provider key.", nameof(key));
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

            // Test provider (adjust if you actually want this to go to idporten)
            if (set.Contains("selfregistered-email"))
            {
                return "testidp"; // <-- change to "idporten" if that’s intentional
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

        private async Task IdentifyOrCreateAltinnUser(UserAuthenticationModel userAuthenticationModel, OidcProvider provider)
        {
            ArgumentNullException.ThrowIfNull(userAuthenticationModel);

            if (userAuthenticationModel != null && userAuthenticationModel.UserID != null && userAuthenticationModel.PartyID != null && userAuthenticationModel.PartyUuid != null)
            {
                return;
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
                    return;
                }
            }
        }
    }
}
