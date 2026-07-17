#nullable enable
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Authentication.Core.Clients.Interfaces;
using Altinn.Common.AccessToken.Services;
using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Core.Helpers;
using Altinn.Platform.Authentication.Core.Models.Oidc;
using Altinn.Platform.Authentication.Core.Services.Interfaces;
using Altinn.Platform.Authentication.Enum;
using Altinn.Platform.Authentication.Helpers;
using Altinn.Platform.Authentication.Services.Interfaces;
using Altinn.Register.Contracts.V1;
using AltinnCore.Authentication.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.FeatureManagement;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json.Linq;
using RegisterContracts = Altinn.Register.Contracts;
using SameSiteMode = Microsoft.AspNetCore.Http.SameSiteMode;

namespace Altinn.Platform.Authentication.Controllers
{
    /// <summary>
    /// Handles authentication of requests to the Altinn platform. Exposes:
    /// <list type="bullet">
    /// <item><description><c>GET authentication</c> — browser sign-in for anonymous clients; redirects to the upstream ID-provider.</description></item>
    /// <item><description><c>GET refresh</c> — refreshes the JWT for an already-authenticated user.</description></item>
    /// <item><description><c>GET exchange/{tokenProvider}</c> — exchanges a trusted external token (ID-porten/Maskinporten/Altinn Studio) for an Altinn JWT.</description></item>
    /// </list>
    /// </summary>
    [Route("authentication/api/v1")]
    [ApiController]
    public class AuthenticationController : ControllerBase
    {
        private const string OrganisationIdentity = "OrganisationLogin";
        private const string EndUserSystemIdentity = "EndUserSystemLogin";
        private const string AltinnStudioIdentity = "AltinnStudioDesignerLogin";
        private const string PidClaimName = "pid";
        private const string AuthLevelClaimName = "acr";
        private const string AuthMethodClaimName = "amr";
        private const string ExternalSessionIdClaimName = "sid";
        private const string IssClaimName = "iss";
        private const string IdportenLevel0 = "idporten-loa-low";
        private const string IdportenLevel3 = "idporten-loa-substantial";
        private const string IdportenLevel4 = "idporten-loa-high";
        private const string ScopeClaim = "scope";
        private readonly GeneralSettings _generalSettings;
        private readonly ILogger _logger;
        private readonly IOrganisationsService _organisationService;
        private readonly IJwtSigningCertificateProvider _certificateProvider;
        private readonly ISigningKeysRetriever _signingKeysRetriever;
        private readonly JwtSecurityTokenHandler _validator;
        private readonly IPublicSigningKeyProvider _designerSigningKeysResolver;
        private readonly IPartiesClient _partiesClient;
        private readonly IOidcServerService _oidcServerService;
        private readonly TimeProvider _timeProvider;

        private readonly IEventLog _eventLog;
        private readonly IFeatureManager _featureManager;
        private readonly IGuidService _guidService;

        private readonly List<string>? _partnerScopes;

        /// <summary>
        /// Initialises a new instance of the <see cref="AuthenticationController"/> class with the given dependencies.
        /// </summary>
        public AuthenticationController(
            ILogger<AuthenticationController> logger,
            IOptions<GeneralSettings> generalSettings,
            ISigningKeysRetriever signingKeysRetriever,
            IJwtSigningCertificateProvider certificateProvider,
            IOrganisationsService organisationRepository,
            IPublicSigningKeyProvider signingKeysResolver,
            IEventLog eventLog,
            IFeatureManager featureManager,
            IGuidService guidService,
            IOidcServerService oidcServerService,
            TimeProvider timeProvider,
            IPartiesClient partiesClient)
        {
            _logger = logger;
            _generalSettings = generalSettings.Value;
            _signingKeysRetriever = signingKeysRetriever;
            _certificateProvider = certificateProvider;
            _organisationService = organisationRepository;
            _designerSigningKeysResolver = signingKeysResolver;
            _validator = new JwtSecurityTokenHandler();
            _eventLog = eventLog;
            _featureManager = featureManager;
            _guidService = guidService;
            _oidcServerService = oidcServerService;
            _timeProvider = timeProvider;
            _partiesClient = partiesClient;
            if (_generalSettings.PartnerScopes != null)
            {
                _partnerScopes = _generalSettings.PartnerScopes.Split(";").ToList();
            }
        }

        /// <summary>
        /// Endpoint to authenticate a user requested by anonymous clients like Altinn Apps or Access Management UI.
        /// - Does not require a client registration in the OIDC server, but will redirect to the upstream ID-provider (ID-porten/FEIDE/UIDP) for authentication. Defaults to ID-porten if no iss query parameter is provided.
        /// - Supports optional requested authentication level (acr_values) to trigger a step-up if the current session does not meet the requested level.
        /// - Supports optional goTo parameter to redirect to a specific URL after successful authentication.
        /// </summary>
        /// <param name="goTo">The url to redirect to if everything validates ok. Only valid to redirect to URLs within the same domain.</param>
        /// <param name="acrValues">Optional requested authentication level as space-separated acr_values. The current values are
        /// <c>idporten-loa-substantial</c>, <c>idporten-loa-high</c> and <c>selfregistered-email</c>; the legacy values
        /// <c>level0</c>, <c>level1</c> and <c>level2</c> are still accepted but deprecated. Any other value yields
        /// <c>400 Bad Request</c> (validated by <see cref="AuthenticationHelper.TryParseAcrValues"/>). When the current
        /// session does not meet the requested level, the user is re-authenticated upstream (step-up).</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>A 302 redirect: to <paramref name="goTo"/> when an existing session already satisfies the request, otherwise to the upstream ID-provider login.</returns>
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status302Found)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
        [HttpGet("authentication")]
        public async Task<ActionResult> AuthenticateUser([FromQuery] string? goTo, [FromQuery(Name = "acr_values")] string? acrValues = null, CancellationToken cancellationToken = default)
        {
            System.Net.IPAddress? ip = HttpContext.Connection.RemoteIpAddress;

            // Optional requested authentication level (acr_values). Lets unregistered clients (e.g. Altinn Apps)
            // ask for a higher level than the user's current session — i.e. trigger a step-up (level 3 -> 4).
            if (!AuthenticationHelper.TryParseAcrValues(acrValues, out string[] requestedAcrValues))
            {
                return BadRequest("Invalid acr_values.");
            }

            if (string.IsNullOrEmpty(goTo) && HttpContext.Request.Cookies[_generalSettings.AuthnGoToCookieName] != null)
            {
                goTo = HttpContext.Request.Cookies[_generalSettings.AuthnGoToCookieName];
            }

            // Validate goTo url. It has to be a valid uri and on the same host or subdomain as this authentication service. Example skd.apps.altinn.no/skattemelding/ is allowed when altinn.no is the host domain
            if (!Uri.TryCreate(goTo, UriKind.Absolute, out var validatedGoToUri) || !IsSafeSameOrSubdomainHttps(validatedGoToUri, _generalSettings.HostName))
            {
                return Redirect(_generalSettings.BaseUrl); // known-safe constant
            }

            string? oidcissuer = Request.Query["iss"];

            // Authentication responses (including the early session-reuse redirects below) must never
            // be cached.
            Response.Headers.CacheControl = "no-store";
            Response.Headers.Pragma = "no-cache";

            // Verify if the user is already authenticated. Then just go directly to the target URL.
            if (User?.Identity != null && User.Identity.IsAuthenticated)
            {
                try
                {
                    OidcSession? refreshedSession = await _oidcServerService.HandleSessionRefresh(User, cancellationToken);

                    // Only reuse the existing session if it already satisfies the requested level.
                    // Otherwise fall through and re-authenticate upstream at the higher level (step-up).
                    if (!AuthenticationHelper.NeedAcrUpgrade(refreshedSession?.Acr, requestedAcrValues))
                    {
                        return Redirect(validatedGoToUri.AbsoluteUri);
                    }
                }
                catch
                {
                    // Session was not able to be refreshed. Delete the cookies and continue to re-authenticate.
                    Response.Cookies.Append(_generalSettings.JwtCookieName, string.Empty, new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = true,
                        Path = "/",
                        Domain = _generalSettings.HostName,
                        Expires = DateTimeOffset.UnixEpoch,
                        SameSite = SameSiteMode.Lax
                    });
                }
            }

            // Check to see if we have a valid Session cookie and recreate JWT based on that. This can happen when user did authenticate for Arbeidsflate, but the JWT has expired. In that case we can reuse the session and create a new JWT for the user.
            if (Request.Cookies.TryGetValue(_generalSettings.AltinnSessionCookieName, out string? sessionCookieValue))
            {
                AuthenticateFromSessionInput sessionCookieInput = new() { SessionHandle = sessionCookieValue };
                AuthenticateFromSessionResult authenticateFromSessionResult = await _oidcServerService.HandleAuthenticateFromSessionResult(sessionCookieInput, cancellationToken);

                // Reuse the session only when it already meets the requested level; otherwise step up.
                if (authenticateFromSessionResult.Kind.Equals(AuthenticateFromSessionResultKind.Success)
                    && !AuthenticationHelper.NeedAcrUpgrade(authenticateFromSessionResult.Acr, requestedAcrValues))
                {
                    foreach (var c in authenticateFromSessionResult.Cookies)
                    {
                        Response.Cookies.Append(c.Name, c.Value, new CookieOptions
                        {
                            HttpOnly = c.HttpOnly,
                            Secure = c.Secure,
                            Path = c.Path ?? "/",
                            Domain = c.Domain,
                            Expires = c.Expires,
                            SameSite = c.SameSite
                        });
                    }

                    return Redirect(validatedGoToUri.AbsoluteUri);
                }
            }

            string ua = Request.Headers.UserAgent.ToString();
            string? userAgentHash = string.IsNullOrEmpty(ua) ? null : Hashing.Sha256Base64Url(ua);
            Guid corr = HttpContext.TraceIdentifier is { Length: > 0 } id && Guid.TryParse(id, out var g) ? g : Guid.CreateVersion7();

            // User was not authenticated, so start a new authorization request for unregistered clients
            // and redirect to the upstream ID-provider (ID-porten/FEIDE/UIDP).
            AuthorizeUnregisteredClientRequest authorizeUnregisteredClientRequest = new()
            {
                GoTo = goTo,
                RequestedIss = oidcissuer,
                ClientIp = ip,
                UserAgentHash = userAgentHash,
                CorrelationId = corr,
                AcrValues = requestedAcrValues
            };

            AuthorizeResult result = await _oidcServerService.AuthorizeUnregisteredClient(authorizeUnregisteredClientRequest, cancellationToken);
            return result.Kind switch
            {
                AuthorizeResultKind.RedirectUpstream
                    => Redirect(result.UpstreamAuthorizeUrl!.ToString()),
                AuthorizeResultKind.LocalError
                    => StatusCode(result.StatusCode ?? 400, result.LocalErrorMessage),
                _ => StatusCode(500)
            };
        }

        /// <summary>
        /// Refreshes JwtToken.
        /// </summary>
        /// <returns>Ok response with the refreshed token appended.</returns>
        [Authorize]
        [HttpGet("refresh")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult> RefreshJwtCookie(bool enrichPid = false, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Starting to refresh token...");

            ClaimsPrincipal principal = HttpContext.User;

            _logger.LogInformation("Refreshing token....");

            if (enrichPid && !principal.Claims.Any(c => c.Type == "pid"))
            {
                Guid partyUuid = AuthenticationHelper.GetPartyUuId(HttpContext);
                if (partyUuid != Guid.Empty)
                {
                    Party? party = await _partiesClient.GetPartyByUuId(partyUuid, cancellationToken);
                    if (party != null && !string.IsNullOrWhiteSpace(party.SSN))
                    {
                        ClaimsIdentity? identity = principal.Identity as ClaimsIdentity;
                        if (identity != null)
                        {
                            identity.AddClaim(new Claim("pid", party.SSN, ClaimValueTypes.String, _generalSettings.AltinnOidcIssuerUrl));
                        }
                    }
                }
            }

            string serializedToken = await GenerateToken(principal);

            OidcSession? session = await _oidcServerService.HandleSessionRefresh(principal, cancellationToken);

            _eventLog.CreateAuthenticationEventAsync(_featureManager, serializedToken, AuthenticationEventType.Refresh, HttpContext.Connection.RemoteIpAddress);
            _logger.LogInformation("End of refreshing token");

            // For test we return cookie also as a cookie
            if (_generalSettings.PlatformEndpoint.Equals("http://localhost/") && HttpContext.Request.Host.Host.Equals("localhost"))
            {
                HttpContext.Response.Cookies.Append(
                    _generalSettings.JwtCookieName,
                    serializedToken,
                    new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = true,
                        SameSite = SameSiteMode.Lax,
                        Domain = _generalSettings.HostName,
                    });
            }

            return Ok(serializedToken);
        }

        /// <summary>
        /// Exchanges a JWT issued by a trusted external token provider (supplied as a <c>Bearer</c> token in the
        /// Authorization header) for a new Altinn JWT used to authenticate against the rest of Altinn.
        /// Returns <c>401 Unauthorized</c> when the token is missing/unreadable/invalid, <c>400 Bad Request</c>
        /// for an unknown provider, and <c>429 Too Many Requests</c> when a self-identified account is locked out.
        /// </summary>
        /// <param name="tokenProvider">The trusted provider that issued the incoming token. One of <c>id-porten</c>, <c>maskinporten</c> or <c>altinnstudio</c> (case-insensitive).</param>
        /// <param name="test">Only relevant for the Maskinporten path: when <c>true</c> and the consumer org is <c>digdir</c>, the token is treated as a test token (see <see cref="OrgIsDigDirAndTestIsTrue"/>). Ignored for the other providers.</param>
        /// <returns>The result of the action. Contains the new token if the old token was valid and could be exchanged.</returns>
        [AllowAnonymous]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(void), StatusCodes.Status429TooManyRequests)]
        [HttpGet("exchange/{tokenProvider}")]
        public async Task<ActionResult> ExchangeExternalSystemToken(string tokenProvider, [FromQuery] bool test)
        {
            string originalToken = string.Empty;

            string? authorization = Request.Headers.Authorization;

            if (!string.IsNullOrEmpty(authorization))
            {
                _logger.LogInformation("Getting the token from Authorization header");
                if (authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("Bearer found");
                    originalToken = authorization.Substring("Bearer ".Length).Trim();
                }
            }

            if (string.IsNullOrEmpty(originalToken))
            {
                _logger.LogInformation("No token found");
                return Unauthorized();
            }

            if (!_validator.CanReadToken(originalToken))
            {
                _logger.LogInformation("Unable to read token");
                return Unauthorized();
            }

            switch (tokenProvider.ToLower())
            {
                case "id-porten":
                    return await AuthenticateIdPortenToken(originalToken);
                case "maskinporten":
                    return await AuthenticateMaskinportenToken(originalToken, test);
                case "altinnstudio":
                    return await AuthenticateAltinnStudioToken(originalToken);
                default:
                    string msg = $"Invalid token provider: {tokenProvider}. Trusted token providers are 'Maskinporten', 'Id-porten' and 'AltinnStudio'.";
                    return BadRequest(msg);
            }
        }

        /// <summary>
        /// Validates a JWT issued by Altinn Studio Designer (issuer <c>studio</c>/<c>dev-studio</c>/<c>staging-studio</c>,
        /// verified against the designer signing keys) and exchanges it for a new Altinn JWT carrying the same claims.
        /// </summary>
        /// <returns>The new Altinn token on success, otherwise <c>401 Unauthorized</c>.</returns>
        private async Task<ActionResult> AuthenticateAltinnStudioToken(string originalToken)
        {
            try
            {
                if (!_validator.CanReadToken(originalToken))
                {
                    return Unauthorized();
                }

                JwtSecurityToken jwt = _validator.ReadJwtToken(originalToken);

                if (!jwt.Issuer.Equals("studio") && !jwt.Issuer.Equals("dev-studio") && !jwt.Issuer.Equals("staging-studio"))
                {
                    return Unauthorized();
                }

                IEnumerable<SecurityKey> signingKeys = await _designerSigningKeysResolver.GetSigningKeys(jwt.Issuer);

                TokenValidationParameters validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKeys = signingKeys,
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    RequireExpirationTime = true,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(10)
                };

                ClaimsPrincipal originalPrincipal = _validator.ValidateToken(originalToken, validationParameters, out _);

                List<Claim> claims = new List<Claim>();
                foreach (Claim claim in originalPrincipal.Claims)
                {
                    claims.Add(claim);
                }

                ClaimsIdentity identity = new ClaimsIdentity(AltinnStudioIdentity);
                identity.AddClaims(claims);
                ClaimsPrincipal principal = new ClaimsPrincipal(identity);

                string serializedToken = await GenerateToken(principal);

                return Ok(serializedToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Altinn Studio authentication failed.");
                return Unauthorized();
            }
        }

        /// <summary>
        /// Action for exchanging a JWT generated by <c>Maskinporten</c> with a new JWT for further use as authentication against rest of Altinn.
        /// </summary>
        /// <returns>The result of the action. Contains the new token if the old token was valid and could be exchanged.</returns>
        private async Task<ActionResult> AuthenticateMaskinportenToken(string originalToken, bool test)
        {
            try
            {
                ICollection<SecurityKey> signingKeys = await GetSigningKeys(_generalSettings.MaskinportenWellKnownConfigEndpoint);
                ICollection<SecurityKey> alternativeSigningKeys = await GetSigningKeys(_generalSettings.MaskinportenWellKnownAlternativeConfigEndpoint);

                TokenValidationParameters validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKeys = signingKeys,
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    RequireExpirationTime = true,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(10)
                };

                ClaimsPrincipal originalPrincipal = GetClaimsPrincipalAndValidateMaskinportenToken(originalToken, validationParameters, alternativeSigningKeys);
                _logger.LogInformation("Token is valid");

                string? issOriginal = originalPrincipal.Claims.Where(c => c.Type.Equals(IssClaimName)).Select(c => c.Value).FirstOrDefault();
                string? externalSessionId = originalPrincipal.Claims.Where(c => c.Type.Equals(ExternalSessionIdClaimName)).Select(c => c.Value).FirstOrDefault();
                if (IsValidIssuer(issOriginal, _generalSettings.MaskinportenWellKnownConfigEndpoint, _generalSettings.MaskinportenWellKnownAlternativeConfigEndpoint))
                {
                    _logger.LogInformation("Invalid issuer {issOriginal}", issOriginal);
                    return Unauthorized();
                }

                string orgNumber = GetOrganisationNumberFromConsumerClaim(originalPrincipal);

                if (string.IsNullOrEmpty(orgNumber))
                {
                    _logger.LogInformation("Invalid consumer claim");
                    return Unauthorized();
                }

                List<Claim> claims = new List<Claim>();
                foreach (Claim claim in originalPrincipal.Claims)
                {
                    claims.Add(claim);
                }

                string issuer = _generalSettings.AltinnOidcIssuerUrl;

                string? org = null;

                if (HasServiceOwnerScope(originalPrincipal))
                {
                    org = await _organisationService.LookupOrg(orgNumber);
                    if (OrgIsDigDirAndTestIsTrue(org, test))
                    {
                        org = "ttd";
                    }

                    if (!string.IsNullOrEmpty(org))
                    {
                        claims.Add(new Claim(AltinnCoreClaimTypes.Org, org, ClaimValueTypes.String, issuer));
                    }
                }

                string authenticatemethod = "maskinporten";

                if (!string.IsNullOrEmpty(Request.Headers["X-Altinn-EnterpriseUser-Authentication"]))
                {
                    // Enterprise-user (virksomhetsbruker) authentication via SBL Bridge was discontinued
                    // with the Altinn 2 shutdown (#1979 / #2030). Always reject with 410 Gone, pointing
                    // callers to Systembruker (system user) or ID-porten.
                    ProblemDetails problem = new ProblemDetails
                    {
                        Status = StatusCodes.Status410Gone,
                        Title = "Virksomhetsbruker is no longer available",
                        Detail = "Virksomhetsbruker (enterprise user) is no longer available. It has been replaced by Systembruker (system user) or ID-porten, depending on the use case. See https://docs.altinn.studio for migration guidance.",
                        Type = "https://docs.altinn.studio"
                    };
                    return new ObjectResult(problem) { StatusCode = StatusCodes.Status410Gone };
                }

                claims.Add(new Claim(AltinnCoreClaimTypes.OrgNumber, orgNumber, ClaimValueTypes.String, issuer));
                claims.Add(new Claim(AltinnCoreClaimTypes.AuthenticateMethod, authenticatemethod, ClaimValueTypes.String, issuer));
                claims.Add(new Claim(AltinnCoreClaimTypes.AuthenticationLevel, "3", ClaimValueTypes.Integer32, issuer));

                string[] claimTypesToRemove = { "aud", IssClaimName, "client_amr", "sid" };
                foreach (string claimType in claimTypesToRemove)
                {
                    Claim? audClaim = claims.Find(c => c.Type == claimType);
                    if (audClaim != null)
                    {
                        claims.Remove(audClaim);
                    }
                }

                claims.Add(new Claim(IssClaimName, issuer, ClaimValueTypes.String, issuer));
                claims.Add(new Claim("sid", _guidService.NewGuid(), ClaimValueTypes.String, issuer));

                ClaimsIdentity identity = new ClaimsIdentity(OrganisationIdentity);

                identity.AddClaims(claims);
                ClaimsPrincipal principal = new ClaimsPrincipal(identity);

                string serializedToken = await GenerateToken(principal);
                await _eventLog.CreateAuthenticationEventAsync(_featureManager, serializedToken, AuthenticationEventType.TokenExchange, HttpContext.Connection.RemoteIpAddress, externalSessionId);
                return Ok(serializedToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Organisation authentication failed.");
                return Unauthorized();
            }
        }

        /// <summary>
        /// Action for exchanging a JWT generated by <c>ID-porten</c> with a new JWT for further use as authentication against rest of Altinn.
        /// </summary>
        /// <returns>The result of the action. Contains the new token if the old token was valid and could be exchanged.</returns>
        private async Task<ActionResult> AuthenticateIdPortenToken(string originalToken)
        {
            try
            {
                JwtSecurityToken token = await ValidateAndExtractOidcToken(originalToken, _generalSettings.IdPortenWellKnownConfigEndpoint, _generalSettings.IdPortenAlternativeWellKnownConfigEndpoint);

                string? pid = token.Claims.Where(c => c.Type.Equals(PidClaimName)).Select(c => c.Value).FirstOrDefault();
                string? authLevel = token.Claims.Where(c => c.Type.Equals(AuthLevelClaimName)).Select(c => c.Value).FirstOrDefault();
                string? authMethod = token.Claims.Where(c => c.Type.Equals(AuthMethodClaimName)).Select(c => c.Value).FirstOrDefault();
                string? externalSessionId = token.Claims.Where(c => c.Type.Equals(ExternalSessionIdClaimName)).Select(c => c.Value).FirstOrDefault();
                string? scope = token.Claims.Where(c => c.Type.Equals(ScopeClaim)).Select(c => c.Value).FirstOrDefault();
                
                if (!HasAltinnScope(scope) && !HasPartnerScope(scope))
                {
                     _logger.LogInformation("Missing scope");
                     return Forbid();
                }

                if (string.IsNullOrWhiteSpace(pid) || string.IsNullOrWhiteSpace(authLevel))
                {
                    _logger.LogInformation("Token contained invalid or missing claims.");
                    return Unauthorized();
                }

                if (string.IsNullOrEmpty(authMethod))
                {
                    authMethod = AuthenticationMethod.NotDefined.ToString();
                }

                // SBL Bridge user lookup is decommissioned. The user fields
                // (UserId/UserName/PartyId/PartyUuid) are resolved from Register:
                // POST /register/api/v2/internal/parties/query (fields=uuid,id,user).
                RegisterContracts.Party? party = await _partiesClient.GetPartyIdentifiersAndUsernameByPersonIdentifier(pid);

                if (party is null || !party.User.HasValue || !party.User.Value.UserId.HasValue)
                {
                    _logger.LogInformation("ID-porten exchange: person not found in Register, or has no associated Altinn user.");
                    return Unauthorized();
                }

                int userId = (int)party.User.Value.UserId.Value;
                string userName = party.User.Value.Username.HasValue ? party.User.Value.Username.Value : string.Empty;
                int partyId = (int)party.PartyId.Value;
                Guid? partyUuid = party.Uuid;

                if (!partyUuid.HasValue)
                {
                    _logger.LogInformation("ID-porten exchange: party UUID missing for user.");
                    return Unauthorized();
                }

                string issuer = _generalSettings.AltinnOidcIssuerUrl;

                string authLevelValue = "0";

                if (authLevel.Equals(IdportenLevel0))
                {
                    authLevelValue = "0";
                }
                else if (authLevel.Equals(IdportenLevel3))
                {
                    authLevelValue = "3";
                }
                else if (authLevel.Equals(IdportenLevel4))
                {
                    authLevelValue = "4";
                }
                else
                {
                    authLevelValue = authLevel.Substring(authLevel.Length - 1, 1);
                }

                List<Claim> claims = new List<Claim>();
                claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.ToString(), ClaimValueTypes.String, issuer));
                claims.Add(new Claim(AltinnCoreClaimTypes.UserId, userId.ToString(), ClaimValueTypes.String, issuer));
                claims.Add(new Claim(AltinnCoreClaimTypes.UserName, userName, ClaimValueTypes.String, issuer));
                claims.Add(new Claim(AltinnCoreClaimTypes.PartyID, partyId.ToString(), ClaimValueTypes.Integer32, issuer));
                claims.Add(new Claim(AltinnCoreClaimTypes.PartyUUID, partyUuid.Value.ToString(), ClaimValueTypes.String, issuer));
                claims.Add(new Claim(AltinnCoreClaimTypes.AuthenticateMethod, authMethod, ClaimValueTypes.String, issuer));
                claims.Add(new Claim(AltinnCoreClaimTypes.AuthenticationLevel, authLevelValue, ClaimValueTypes.Integer32, issuer));
                claims.AddRange(token.Claims);

                string[] claimTypesToRemove = { "aud", IssClaimName, "at_hash", "sid", "sub" };
                foreach (string claimType in claimTypesToRemove)
                {
                    Claim claim = claims.Find(c => c.Type == claimType);
                    claims.Remove(claim);
                }

                claims.Add(new Claim(IssClaimName, issuer, ClaimValueTypes.String, issuer));
                claims.Add(new Claim("sid", _guidService.NewGuid(), ClaimValueTypes.String, issuer));

                ClaimsIdentity identity = new ClaimsIdentity(EndUserSystemIdentity);
                identity.AddClaims(claims);
                ClaimsPrincipal principal = new ClaimsPrincipal(identity);

                string serializedToken = await GenerateToken(principal, token.ValidTo);
                await _eventLog.CreateAuthenticationEventAsync(_featureManager, serializedToken, AuthenticationEventType.TokenExchange, HttpContext.Connection.RemoteIpAddress, externalSessionId);
                return Ok(serializedToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "End user system authentication failed.");
                return Unauthorized();
            }
        }

        /// <summary>
        /// Assumes that the consumer claim follows the ISO 6523. {"Identifier": {"Authority": "iso6523-actorid-upis","ID": "9908:910075918"}}
        /// </summary>
        /// <returns>organisation number found in the ID property of the ISO 6523 record</returns>
        private static string? GetOrganisationNumberFromConsumerClaim(ClaimsPrincipal originalPrincipal)
        {
            string? consumerJson = originalPrincipal.FindFirstValue("consumer");
            
            if (consumerJson == null)
            {
                return null;
            }

            JObject consumer = JObject.Parse(consumerJson);
            JToken? consumerAuthorityToken = consumer["authority"];
            
            if (consumerAuthorityToken == null)
            {
                return null;
            }   

            string consumerAuthority = consumerAuthorityToken.ToString();
            if (!"iso6523-actorid-upis".Equals(consumerAuthority))
            {
                return null;
            }
           
            JToken? consumerValue = consumer["ID"];

            if (consumerValue == null)
            {
                return null;
            }

            string? consumerId = consumerValue.ToString();

            string organisationNumber = consumerId.Split(":")[1];
            return organisationNumber;
        }

        private static bool HasServiceOwnerScope(ClaimsPrincipal originalPrincipal)
        {
            string? scope = originalPrincipal.FindFirstValue("scope");

            if (scope != null && scope.Contains("altinn:serviceowner"))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Returns <c>true</c> when the space-separated <paramref name="scope"/> string contains at least one
        /// Altinn scope (any token prefixed <c>altinn:</c>). Used to gate which exchanged tokens are accepted.
        /// </summary>
        private static bool HasAltinnScope(string scope)
        {
            return scope?.Split(" ").Any(s => s.StartsWith("altinn:")) ?? false;
        }

        /// <summary>
        /// Returns <c>true</c> when the space-separated <paramref name="scope"/> string contains one of the
        /// configured partner scopes (<see cref="GeneralSettings.PartnerScopes"/>). Used to gate which exchanged
        /// tokens are accepted for partner integrations.
        /// </summary>
        private bool HasPartnerScope(string scope)
        {
            string[]? scopes = scope?.Split(" ");

            foreach (string partnerScope in _partnerScopes)
            {
                if (scopes?.Contains(partnerScope) == true)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IssuerMatchesWellknownEndpoint(string issOriginal, string wellknownEndpoint, string alternativeWellknownEndpoint)
        {
            if (!wellknownEndpoint.Contains(issOriginal) && !alternativeWellknownEndpoint.Contains(issOriginal))
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        private static bool IsValidIssuer(string issOriginal, string maskinportenWellKnownConfigEndpoint, string maskinportenWellKnownAlternativeConfigEndpoint)
        {
            if (issOriginal == null || !IssuerMatchesWellknownEndpoint(issOriginal, maskinportenWellKnownConfigEndpoint, maskinportenWellKnownAlternativeConfigEndpoint))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private static bool OrgIsDigDirAndTestIsTrue(string org, bool test)
        {
            if (org == "digdir" && test)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Generates a token and serialize it to a compact format
        /// </summary>
        /// <param name="principal">The claims principal for the token</param>
        /// <param name="expires">The Expiry time of the token</param>
        /// <returns>A serialized version of the generated JSON Web Token.</returns>
        private async Task<string> GenerateToken(ClaimsPrincipal principal, DateTimeOffset? expires = null)
        {
            List<X509Certificate2> certificates = await _certificateProvider.GetCertificates();

            DateTimeOffset now = _timeProvider.GetUtcNow();

            // If GetLatestCertificateWithRolloverDelay uses "now", pass it in so it also honors TimeProvider.
            var certificate = GetLatestCertificateWithRolloverDelay(
                certificates,
                _generalSettings.JwtSigningCertificateRolloverDelayHours,
                now);

            var lifetime = TimeSpan.FromMinutes(_generalSettings.JwtValidityMinutes);
            var exp = (expires ?? now.Add(lifetime)).UtcDateTime;

            var tokenHandler = new JwtSecurityTokenHandler();

            var descriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(principal.Claims),
                IssuedAt = now.UtcDateTime,     // iat
                NotBefore = now.UtcDateTime,    // nbf
                Expires = exp,                  // exp
                SigningCredentials = new X509SigningCredentials(certificate),
            };

            var token = tokenHandler.CreateToken(descriptor);
            return tokenHandler.WriteToken(token);
        }

        /// <summary>
        /// Selects the newest signing certificate that has been valid for at least <paramref name="rolloverDelayHours"/>
        /// hours (by <c>NotBefore</c>). The delay gives a freshly-published certificate time to propagate to token
        /// consumers before it is used to sign. If no certificate is old enough, it falls back to the newest currently
        /// valid certificate.
        /// </summary>
        /// <param name="certificates">The available signing certificates.</param>
        /// <param name="rolloverDelayHours">How long a certificate must have existed before it is used for signing.</param>
        /// <param name="now">The current time (injected for testability).</param>
        /// <returns>The chosen certificate, or <c>null</c> if none are usable.</returns>
        private X509Certificate2 GetLatestCertificateWithRolloverDelay(
            List<X509Certificate2> certificates, int rolloverDelayHours, DateTimeOffset now)
        {
            // First limit the search to just those certificates that have existed longer than the rollover delay.
            var rolloverCutoff = now.AddHours(-rolloverDelayHours);
            var potentialCerts =
                certificates.Where(c => c.NotBefore < rolloverCutoff).ToList();

            // If no certs could be found, then widen the search to any usable certificate.
            if (!potentialCerts.Any())
            {
                potentialCerts = certificates.Where(c => c.NotBefore < now).ToList();
            }

            // Of the potential certs, return the newest one.
            return potentialCerts
                .OrderByDescending(c => c.NotBefore)
                .FirstOrDefault();
        }
        
        private async Task<JwtSecurityToken> ValidateAndExtractOidcToken(string originalToken, string wellKnownConfigEndpoint, string alternativeWellKnownConfigEndpoint = null)
        {
            try
            {
                ICollection<SecurityKey> signingKeys = await _signingKeysRetriever.GetSigningKeys(wellKnownConfigEndpoint);
                return ValidateToken(originalToken, signingKeys);
            }
            catch (Exception)
            {
                if (!string.IsNullOrEmpty(alternativeWellKnownConfigEndpoint))
                {
                    ICollection<SecurityKey> alternativeSigningKeys =
                   await _signingKeysRetriever.GetSigningKeys(alternativeWellKnownConfigEndpoint);

                    return ValidateToken(originalToken, alternativeSigningKeys);
                }

                throw;
            }
        }

        private ClaimsPrincipal GetClaimsPrincipalAndValidateMaskinportenToken(string originalToken, TokenValidationParameters validationParameters, ICollection<SecurityKey> alternativeSigningKeys)
        {
            try
            {
                ClaimsPrincipal originalPrincipal = _validator.ValidateToken(originalToken, validationParameters, out _);
                return originalPrincipal;
            }           
            catch (Exception)
            {
                if (alternativeSigningKeys is null || alternativeSigningKeys.Count == 0)
                {
                    throw;
                }

                validationParameters.IssuerSigningKeys = alternativeSigningKeys;
                return _validator.ValidateToken(originalToken, validationParameters, out _);
            }
        }

        private async Task<ICollection<SecurityKey>> GetSigningKeys(string endpoint)
        {
            try
            {
                return await _signingKeysRetriever.GetSigningKeys(endpoint);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private JwtSecurityToken ValidateToken(string originalToken, ICollection<SecurityKey> signingKeys)
        {
            TokenValidationParameters validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = signingKeys,
                ValidateIssuer = false,
                ValidateAudience = false,
                RequireExpirationTime = true,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(10),
            };

            _validator.ValidateToken(originalToken, validationParameters, out _);
            _logger.LogInformation("Token is valid");

            JwtSecurityToken token = _validator.ReadJwtToken(originalToken);
            return token;
        }

        /// <summary>
        /// Open-redirect guard for the <c>goTo</c> parameter. Returns <c>true</c> only when <paramref name="target"/>
        /// is safe to redirect an authenticated user to: an absolute <c>https</c> URL, with no embedded credentials,
        /// whose host equals <paramref name="baseHost"/> or is a subdomain of it (e.g. <c>skd.apps.altinn.no</c> is
        /// allowed when the service host is <c>altinn.no</c>). This prevents leaking the session/token to an
        /// attacker-controlled domain via a crafted <c>goTo</c>.
        /// </summary>
        /// <param name="target">The requested redirect target.</param>
        /// <param name="baseHost">The authentication service host that the target must match or be a subdomain of.</param>
        /// <returns><c>true</c> if the target is safe to redirect to; otherwise <c>false</c>.</returns>
        private static bool IsSafeSameOrSubdomainHttps(Uri target, string baseHost)
        {
            if (target is null || !target.IsAbsoluteUri)
            {
                return false;
            }

            // 1) Must be HTTPS
            if (!string.Equals(target.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // 2) No embedded credentials
            if (!string.IsNullOrEmpty(target.UserInfo))
            {
                return false;
            }

            // 3) Normalize hosts
            static string Norm(string h) => h.Trim().TrimEnd('.').ToLowerInvariant();

            string th = Norm(target.Host);
            string bh = Norm(baseHost);
            if (string.IsNullOrEmpty(th) || string.IsNullOrEmpty(bh))
            {
                return false;
            }

            // 4) Exact or dot-bounded subdomain
            if (th == bh)
            {
                return true;
            }

            return th.Length > bh.Length && th.EndsWith("." + bh, StringComparison.Ordinal);
        }
    }
}
