#nullable enable
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Altinn.Common.AccessToken.Services;
using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Core.Constants;
using Altinn.Platform.Authentication.Core.Helpers;
using Altinn.Platform.Authentication.Core.Models.Oidc;
using Altinn.Platform.Authentication.Core.Models.Profile;
using Altinn.Platform.Authentication.Core.Services.Interfaces;
using Altinn.Platform.Authentication.Enum;
using Altinn.Platform.Authentication.Helpers;
using Altinn.Platform.Authentication.Model;
using Altinn.Platform.Authentication.Services;
using Altinn.Platform.Authentication.Services.Interfaces;
using AltinnCore.Authentication.Constants;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.FeatureManagement;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Net.Http.Headers;
using Newtonsoft.Json.Linq;
using SameSiteMode = Microsoft.AspNetCore.Http.SameSiteMode;

namespace Altinn.Platform.Authentication.Controllers
{
    /// <summary>
    /// Handles the authentication of requests to platform
    /// </summary>
    [Route("authentication/api/v1")]
    [ApiController]
    public class AuthenticationController : ControllerBase
    {
        private const string HeaderValueNoCache = "no-cache";
        private const string HeaderValueEpocDate = "Thu, 01 Jan 1970 00:00:00 GMT";
        private const string OrganisationIdentity = "OrganisationLogin";
        private const string EndUserSystemIdentity = "EndUserSystemLogin";
        private const string AltinnStudioIdentity = "AltinnStudioDesignerLogin";
        private const string PidClaimName = "pid";
        private const string AuthLevelClaimName = "acr";
        private const string AuthMethodClaimName = "amr";
        private const string ExternalSessionIdClaimName = "jti";
        private const string IssClaimName = "iss";
        private const string OriginalIssClaimName = "originaliss";
        private const string IdportenLevel0 = "idporten-loa-low";
        private const string IdportenLevel3 = "idporten-loa-substantial";
        private const string IdportenLevel4 = "idporten-loa-high";
        private const string ScopeClaim = "scope";
        private readonly GeneralSettings _generalSettings;
        private readonly ILogger _logger;
        private readonly IOrganisationsService _organisationService;
        private readonly IJwtSigningCertificateProvider _certificateProvider;
        private readonly ISblCookieDecryptionService _cookieDecryptionService;
        private readonly ISigningKeysRetriever _signingKeysRetriever;
        private readonly IUserProfileService _userProfileService;
        private readonly IEnterpriseUserAuthenticationService _enterpriseUserAuthenticationService;
        private readonly JwtSecurityTokenHandler _validator;
        private readonly IPublicSigningKeyProvider _designerSigningKeysResolver;
        private readonly IOidcProvider _oidcProvider;
        private readonly IProfile _profileService;
        private readonly IOidcServerService _oidcServerService;
        private readonly TimeProvider _timeProvider;

        private readonly OidcProviderSettings _oidcProviderSettings;
        private readonly IAntiforgery _antiforgery;

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
            IOptions<OidcProviderSettings> oidcProviderSettings,
            ISigningKeysRetriever signingKeysRetriever,
            IJwtSigningCertificateProvider certificateProvider,
            ISblCookieDecryptionService cookieDecryptionService,
            IUserProfileService userProfileService,
            IEnterpriseUserAuthenticationService enterpriseUserAuthenticationService,
            IOrganisationsService organisationRepository,
            IPublicSigningKeyProvider signingKeysResolver,
            IOidcProvider oidcProvider,
            IAntiforgery antiforgery,
            IEventLog eventLog,
            IFeatureManager featureManager,
            IGuidService guidService,
            IProfile profileService,
            IOidcServerService oidcServerService,
            TimeProvider timeProvider)
        {
            _logger = logger;
            _generalSettings = generalSettings.Value;
            _oidcProviderSettings = oidcProviderSettings.Value;
            _signingKeysRetriever = signingKeysRetriever;
            _certificateProvider = certificateProvider;
            _cookieDecryptionService = cookieDecryptionService;
            _organisationService = organisationRepository;
            _userProfileService = userProfileService;
            _enterpriseUserAuthenticationService = enterpriseUserAuthenticationService;
            _designerSigningKeysResolver = signingKeysResolver;
            _validator = new JwtSecurityTokenHandler();
            _oidcProvider = oidcProvider;
            _antiforgery = antiforgery;
            _eventLog = eventLog;
            _featureManager = featureManager;
            _guidService = guidService;
            _profileService = profileService;
            _oidcServerService = oidcServerService;
            _timeProvider = timeProvider;
            if (_generalSettings.PartnerScopes != null)
            {
                _partnerScopes = _generalSettings.PartnerScopes.Split(";").ToList();
            }
        }

        /// <summary>
        /// Request that handles the form authentication cookie from SBL
        /// </summary>
        /// <param name="goTo">The url to redirect to if everything validates ok</param>
        /// <param name="dontChooseReportee">Parameter to indicate disabling of reportee selection in Altinn Portal.</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>redirect to correct url based on the validation of the form authentication sbl cookie</returns>
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status302Found)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
        [HttpGet("authentication")]
        public async Task<ActionResult> AuthenticateUser([FromQuery] string? goTo, [FromQuery] bool dontChooseReportee, CancellationToken cancellationToken = default)
        {
            System.Net.IPAddress? ip = HttpContext.Connection.RemoteIpAddress;

            if (string.IsNullOrEmpty(goTo) && HttpContext.Request.Cookies[_generalSettings.AuthnGoToCookieName] != null)
            {
                goTo = HttpContext.Request.Cookies[_generalSettings.AuthnGoToCookieName];
            }

            // Validate goTo url. It has to be a valid uri and on the same host or subdomain as this authentication service. Example skd.apps.altinn.no/skattemelding/ is allowed when altinn.no is the host domain
            if (!Uri.TryCreate(goTo, UriKind.Absolute, out var validatedGoToUri) || !IsSafeSameOrSubdomainHttps(validatedGoToUri, _generalSettings.HostName))
            {
                return Redirect(_generalSettings.BaseUrl); // known-safe constant
            }

            string platformReturnUrl = $"{_generalSettings.PlatformEndpoint}authentication/api/v1/authentication?goto={goTo}";

            if (dontChooseReportee) 
            {
                platformReturnUrl += "&DontChooseReportee=true";
            }

            string encodedGoToUrl = HttpUtility.UrlEncode(platformReturnUrl);
            string sblRedirectUrl = $"{_generalSettings.SBLRedirectEndpoint}?goTo={encodedGoToUrl}";

            string? oidcissuer = Request.Query["iss"];
            UserAuthenticationModel userAuthentication;
            if (_generalSettings.EnableOidc && (!string.IsNullOrEmpty(oidcissuer) || _generalSettings.ForceOidc))
            {
                OidcProvider provider = GetOidcProvider(oidcissuer);

                string? code = Request.Query["code"];
                string? state = Request.Query["state"];

                if (!string.IsNullOrEmpty(code))
                {
                    if (string.IsNullOrEmpty(state))
                    {
                        return BadRequest("Missing state param");
                    }

                    HttpContext.Request.Headers.Add("X-XSRF-TOKEN", state);

                    try
                    {
                        await _antiforgery.ValidateRequestAsync(HttpContext);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogInformation("Validateion of state failed", ex.ToString());
                        return BadRequest("Invalid state param");
                    }

                    OidcCodeResponse oidcCodeResponse = await _oidcProvider.GetTokens(code, provider, GetRedirectUri(provider), null, cancellationToken);
                    string? originalToken = oidcCodeResponse.IdToken;
                    JwtSecurityToken jwtSecurityToken = await ValidateAndExtractOidcToken(oidcCodeResponse.IdToken, provider.WellKnownConfigEndpoint);
                    userAuthentication = AuthenticationHelper.GetUserFromToken(jwtSecurityToken, provider);
                    if (!ValidateNonce(HttpContext, userAuthentication.Nonce))
                    {
                        return BadRequest("Invalid nonce");
                    }

                    if (!userAuthentication.UserID.HasValue)
                    {
                        await IdentifyOrCreateAltinnUser(userAuthentication, provider);
                    }

                    if (userAuthentication.UserID.HasValue && userAuthentication.PartyUuid == null)
                    {
                        UserProfile profile = await _profileService.GetUserProfile(new UserProfileLookup { UserId = userAuthentication.UserID.Value });
                        userAuthentication.PartyUuid = profile.UserUuid;
                    }

                    if (userAuthentication.IsAuthenticated)
                    {
                        await CreateTokenCookie(userAuthentication);

                        return Redirect(validatedGoToUri.AbsoluteUri);
                    }
                }
                else if (_generalSettings.AuthorizationServerEnabled)
                {
                    // Flow for Authorization Server Active
                    // Verify if user is already authenticated. The just go directly to the target URL
                    if (User?.Identity != null && User.Identity.IsAuthenticated)
                    {
                        try
                        {
                            await _oidcServerService.HandleSessionRefresh(User, cancellationToken);
                            return Redirect(validatedGoToUri.AbsoluteUri);
                        }
                        catch
                        {
                            // Sessions was not able to be refreshed. Deletes the cookies and continues to re-authenticate
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

                    // Check to see if we have a valid Session cookie and recreate JWT Based on that
                    if (Request.Cookies.TryGetValue(_generalSettings.AltinnSessionCookieName, out string? sessionCookieValue))
                    {
                        AuthenticateFromSessionInput sessionCookieInput = new() { SessionHandle = sessionCookieValue };
                        AuthenticateFromSessionResult authenticateFromSessionResult = await _oidcServerService.HandleAuthenticateFromSessionResult(sessionCookieInput, cancellationToken);
                        if (authenticateFromSessionResult.Kind.Equals(AuthenticateFromSessionResultKind.Success))
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

                    Response.Headers.CacheControl = "no-store";
                    Response.Headers.Pragma = "no-cache";
                    string ua = Request.Headers.UserAgent.ToString();
                    string? userAgentHash = string.IsNullOrEmpty(ua) ? null : Hashing.Sha256Base64Url(ua);
                    Guid corr = HttpContext.TraceIdentifier is { Length: > 0 } id && Guid.TryParse(id, out var g) ? g : Guid.CreateVersion7();

                    string cookieName = Request.Cookies[_generalSettings.SblAuthCookieEnvSpecificName] != null ? _generalSettings.SblAuthCookieEnvSpecificName : _generalSettings.SblAuthCookieName;
                    string? encryptedTicket = Request.Cookies[cookieName];

                    if (encryptedTicket != null)
                    {
                        AuthenticateFromAltinn2TicketInput ticketInput = new() 
                        { 
                            EncryptedTicket = encryptedTicket, 
                            CreatedByIp = ip ?? System.Net.IPAddress.Loopback,
                            UserAgentHash = userAgentHash, 
                            CorrelationId = corr 
                        };
                        
                        AuthenticateFromAltinn2TicketResult ticketResult = await _oidcServerService.HandleAuthenticateFromTicket(ticketInput, cancellationToken);
                        if (ticketResult.Kind.Equals(AuthenticateFromAltinn2TicketResultKind.Success))
                        {
                            foreach (var c in ticketResult.Cookies)
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

                            // When you finally redirect:
                            return Redirect(validatedGoToUri.AbsoluteUri); // not OriginalString
                        }
                    }

                    // User was not autenticated so start a new authorization request for unregistered clints and redirect to upstream ID- Provider like ID-porten/FEIDE/UIDP
                    AuthorizeUnregisteredClientRequest authorizeUnregisteredClientRequest = new()
                    {
                        GoTo = goTo,
                        RequestedIss = oidcissuer,
                        ClientIp = ip,
                        UserAgentHash = userAgentHash,
                        CorrelationId = corr,
                        AcrValues = []
                    };

                    AuthorizeResult result = await _oidcServerService.AuthorizeUnregisteredClient(authorizeUnregisteredClientRequest, cancellationToken);
                    return result.Kind switch
                    {
                        AuthorizeResultKind.RedirectUpstream
                            => Redirect(result.UpstreamAuthorizeUrl!.ToString()),
                        AuthorizeResultKind.LocalError
                            => StatusCode(result.StatusCode ?? 400, result.LocalErrorMessage), // or return View("Error", ...)
                        _ => StatusCode(500)
                    };
                }
                else
                {
                    // Generates state tokens. One is added to a cookie and another is sent as state parameter to OIDC provider
                    AntiforgeryTokenSet tokens = _antiforgery.GetAndStoreTokens(HttpContext);

                    // Create Nonce. One is added to a cookie and another is sent as nonce parameter to OIDC provider
                    string nonce = CreateNonce(HttpContext);
                    CreateGoToCookie(HttpContext, validatedGoToUri.AbsoluteUri);

                    // Redirect to OIDC Provider
                    return Redirect(CreateAuthenticationRequest(provider, tokens.RequestToken, nonce));
                }
            }
            else
            {
                // Verify if user is already authenticated. The just go directly to the target URL
                if (User?.Identity != null && User.Identity.IsAuthenticated)
                {
                    return Redirect(validatedGoToUri.AbsoluteUri);
                }

                // Check to see if we have a valid Session cookie and recreate JWT Based on that
                if (Request.Cookies.TryGetValue(_generalSettings.AltinnSessionCookieName, out string? sessionCookie))
                {
                    AuthenticateFromSessionInput sessionCookieInput = new() { SessionHandle = sessionCookie };
                    AuthenticateFromSessionResult authenticateFromSessionResult = await _oidcServerService.HandleAuthenticateFromSessionResult(sessionCookieInput, cancellationToken);
                    if (authenticateFromSessionResult.Kind.Equals(AuthenticateFromSessionResultKind.Success))
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

                if (Request.Cookies[_generalSettings.SblAuthCookieName] == null && Request.Cookies[_generalSettings.SblAuthCookieEnvSpecificName] == null)
                {
                    return Redirect(sblRedirectUrl);
                }

                string cookieName = Request.Cookies[_generalSettings.SblAuthCookieEnvSpecificName] != null ? _generalSettings.SblAuthCookieEnvSpecificName : _generalSettings.SblAuthCookieName;
                string? encryptedTicket = Request.Cookies[cookieName];
                if (_generalSettings.AuthorizationServerEnabled && encryptedTicket != null)
                {
                    // Server enabled, but still rely on Altinn 2 for Authentication. Temporary solution during migration period.
                    AuthenticateFromAltinn2TicketInput ticketInput = new() { EncryptedTicket = encryptedTicket, CreatedByIp = ip ?? System.Net.IPAddress.Loopback };
                    AuthenticateFromAltinn2TicketResult ticketResult = await _oidcServerService.HandleAuthenticateFromTicket(ticketInput, cancellationToken);

                    if (ticketResult.Kind.Equals(AuthenticateFromAltinn2TicketResultKind.Success))
                    {
                        foreach (var c in ticketResult.Cookies)
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

                        // When you finally redirect:
                        return Redirect(validatedGoToUri.AbsoluteUri); // not OriginalString
                    }
                }
                else
                {
                    // Legacy Mode. Decrypt the SBL cookie and create our own JWT cookie
                    try
                    {
                        userAuthentication = await _cookieDecryptionService.DecryptTicket(encryptedTicket);
                    }
                    catch (SblBridgeResponseException sblBridgeException)
                    {
                        _logger.LogWarning(sblBridgeException, "SBL Bridge replied with {StatusCode} - {ReasonPhrase}", sblBridgeException.Response.StatusCode, sblBridgeException.Response.ReasonPhrase);
                        return StatusCode(StatusCodes.Status503ServiceUnavailable);
                    }

                    if (userAuthentication.UserID.HasValue && userAuthentication.UserID.Value != 0 && userAuthentication.PartyUuid == null)
                    {
                        UserProfile profile = await _profileService.GetUserProfile(new UserProfileLookup { UserId = userAuthentication.UserID.Value });
                        userAuthentication.PartyUuid = profile.UserUuid;
                    }

                    if (userAuthentication != null && userAuthentication.IsAuthenticated)
                    {
                        await CreateTokenCookie(userAuthentication);
                        
                        return Redirect(validatedGoToUri.AbsoluteUri);
                    }
                }
            }

            return Redirect(sblRedirectUrl);
        }

        /// <summary>
        /// Refreshes JwtToken.
        /// </summary>
        /// <returns>Ok response with the refreshed token appended.</returns>
        [Authorize]
        [HttpGet("refresh")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult> RefreshJwtCookie(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting to refresh token...");

            ClaimsPrincipal principal = HttpContext.User;

            _logger.LogInformation("Refreshing token....");

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
        /// Action for exchanging a JWT generated by a trusted token provider with a new JWT for further use as authentication against rest of Altinn.
        /// </summary>
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
                    string? enterpriseUserHeader = Request.Headers["X-Altinn-EnterpriseUser-Authentication"];

                    (UserAuthenticationResult authenticatedEnterpriseUser, ActionResult error) = await HandleEnterpriseUserLogin(enterpriseUserHeader, orgNumber);

                    if (error != null)
                    {
                        return error;
                    }

                    if (authenticatedEnterpriseUser != null)
                    {
                        authenticatemethod = "virksomhetsbruker";

                        string userID = authenticatedEnterpriseUser.UserID.ToString();
                        string username = authenticatedEnterpriseUser.Username;
                        string partyId = authenticatedEnterpriseUser.PartyID.ToString();

                        claims.Add(new Claim(AltinnCoreClaimTypes.UserId, userID, ClaimValueTypes.Integer32, issuer));
                        claims.Add(new Claim(AltinnCoreClaimTypes.UserName, username, ClaimValueTypes.String, issuer));
                        claims.Add(new Claim(AltinnCoreClaimTypes.PartyID, partyId, ClaimValueTypes.Integer32, issuer));
                    }
                }

                claims.Add(new Claim(AltinnCoreClaimTypes.OrgNumber, orgNumber, ClaimValueTypes.String, issuer));
                claims.Add(new Claim(AltinnCoreClaimTypes.AuthenticateMethod, authenticatemethod, ClaimValueTypes.String, issuer));
                claims.Add(new Claim(AltinnCoreClaimTypes.AuthenticationLevel, "3", ClaimValueTypes.Integer32, issuer));

                string[] claimTypesToRemove = { "aud", IssClaimName, "client_amr", "jti" };
                foreach (string claimType in claimTypesToRemove)
                {
                    Claim? audClaim = claims.Find(c => c.Type == claimType);
                    if (audClaim != null)
                    {
                        claims.Remove(audClaim);
                    }
                }

                claims.Add(new Claim(IssClaimName, issuer, ClaimValueTypes.String, issuer));
                claims.Add(new Claim("jti", _guidService.NewGuid(), ClaimValueTypes.String, issuer));

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

        private async Task<(UserAuthenticationResult? AuthenticatedEnterpriseUser, ActionResult? Error)> HandleEnterpriseUserLogin(string enterpriseUserHeader, string orgNumber)
        {
            EnterpriseUserCredentials credentials;

            try
            {
                credentials = DecodeEnterpriseUserHeader(enterpriseUserHeader, orgNumber);
            }
            catch (Exception)
            {
                return (null, StatusCode(400));
            }

            HttpResponseMessage response = await _enterpriseUserAuthenticationService.AuthenticateEnterpriseUser(credentials);
            string content = await response.Content.ReadAsStringAsync();

            switch (response.StatusCode)
            {
                case System.Net.HttpStatusCode.BadRequest:
                    return (null, StatusCode(400));
                case System.Net.HttpStatusCode.NotFound:
                    ObjectResult result = StatusCode(401, "The user either does not exist or the password is incorrect.");
                    return (null, result);
                case System.Net.HttpStatusCode.TooManyRequests:
                    if (response.Headers.RetryAfter != null)
                    {
                        Response.Headers.Add("Retry-After", response.Headers.RetryAfter.ToString());
                    }

                    return (null, StatusCode(429));
                case System.Net.HttpStatusCode.OK:
                    UserAuthenticationResult userAuthenticationResult = JsonSerializer.Deserialize<UserAuthenticationResult>(content);

                    return (userAuthenticationResult, null);
                default:
                    _logger.LogWarning("Unexpected response from SBLBridge during enterprise user authentication. HttpStatusCode={statusCode} Content={content}", response.StatusCode, content);
                    return (null, StatusCode(502));
            }
        }

        private EnterpriseUserCredentials DecodeEnterpriseUserHeader(string encodedCredentials, string orgNumber)
        {
            byte[] decodedCredentials = Convert.FromBase64String(encodedCredentials);
            string decodedString = Encoding.UTF8.GetString(decodedCredentials);

            string[] decodedStringArray = decodedString.Split(":", 2);
            string usernameFromRequest = decodedStringArray[0];
            string password = decodedStringArray[1];

            EnterpriseUserCredentials credentials = new EnterpriseUserCredentials { UserName = usernameFromRequest, Password = password, OrganizationNumber = orgNumber };
            return credentials;
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

                UserProfile userProfile = await _userProfileService.GetUser(pid);
                UserProfile profile = await _profileService.GetUserProfile(new UserProfileLookup { Ssn = pid });

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
                claims.Add(new Claim(ClaimTypes.NameIdentifier, userProfile.UserId.ToString(), ClaimValueTypes.String, issuer));
                claims.Add(new Claim(AltinnCoreClaimTypes.UserId, userProfile.UserId.ToString(), ClaimValueTypes.String, issuer));
                claims.Add(new Claim(AltinnCoreClaimTypes.UserName, userProfile.UserName, ClaimValueTypes.String, issuer));
                claims.Add(new Claim(AltinnCoreClaimTypes.PartyID, userProfile.PartyId.ToString(), ClaimValueTypes.Integer32, issuer));
                claims.Add(new Claim(AltinnCoreClaimTypes.PartyUUID, profile.UserUuid.ToString(), ClaimValueTypes.String, issuer));
                claims.Add(new Claim(AltinnCoreClaimTypes.AuthenticateMethod, authMethod, ClaimValueTypes.String, issuer));
                claims.Add(new Claim(AltinnCoreClaimTypes.AuthenticationLevel, authLevelValue, ClaimValueTypes.Integer32, issuer));
                claims.AddRange(token.Claims);

                string[] claimTypesToRemove = { "aud", IssClaimName, "at_hash", "jti", "sub" };
                foreach (string claimType in claimTypesToRemove)
                {
                    Claim claim = claims.Find(c => c.Type == claimType);
                    claims.Remove(claim);
                }

                claims.Add(new Claim(IssClaimName, issuer, ClaimValueTypes.String, issuer));
                claims.Add(new Claim("jti", _guidService.NewGuid(), ClaimValueTypes.String, issuer));

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
        /// Creates a session cookie meant to be used to hold the generated JSON Web Token and appends it to the response.
        /// </summary>
        /// <param name="cookieValue">The cookie value.</param>
        private void CreateJwtCookieAndAppendToResponse(string cookieValue)
        {
            CookieBuilder cookieBuilder = new RequestPathBaseCookieBuilder
            {
                Name = _generalSettings.JwtCookieName,
                //// To support OAuth authentication, a lax mode is required, see https://github.com/aspnet/Security/issues/1231.
                SameSite = SameSiteMode.Lax,
                HttpOnly = true,
                SecurePolicy = CookieSecurePolicy.Always,
                IsEssential = true,
                Domain = _generalSettings.HostName
            };

            CookieOptions cookieOptions = cookieBuilder.Build(HttpContext);

            ICookieManager cookieManager = new ChunkingCookieManager();
            cookieManager.AppendResponseCookie(
                HttpContext,
                cookieBuilder.Name,
                cookieValue,
                cookieOptions);

            ApplyHeaders();
        }

        private void ApplyHeaders()
        {
            Response.Headers[HeaderNames.CacheControl] = HeaderValueNoCache;
            Response.Headers[HeaderNames.Pragma] = HeaderValueNoCache;
            Response.Headers[HeaderNames.Expires] = HeaderValueEpocDate;
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

        private static bool HasAltinnScope(string scope)
        {
            return scope?.Split(" ").Any(s => s.StartsWith("altinn:")) ?? false;
        }

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
        
        private async Task IdentifyOrCreateAltinnUser(UserAuthenticationModel userAuthenticationModel, OidcProvider provider)
        {
            UserProfile profile;

            if (!string.IsNullOrEmpty(userAuthenticationModel.ExternalIdentity))
            {
                string issExternalIdentity = userAuthenticationModel.Iss + ":" + userAuthenticationModel.ExternalIdentity;
                profile = await _userProfileService.GetUser(issExternalIdentity);

                if (profile != null)
                {
                    userAuthenticationModel.UserID = profile.UserId;
                    userAuthenticationModel.PartyID = profile.PartyId;
                    return;
                }

                UserProfile userToCreate = new()
                {
                    ExternalIdentity = issExternalIdentity,
                    UserName = CreateUserName(userAuthenticationModel, provider),
                    UserType = Altinn.Platform.Authentication.Core.Models.Profile.Enums.UserType.SelfIdentified
                };

                UserProfile userCreated = await _userProfileService.CreateUser(userToCreate);
                userAuthenticationModel.UserID = userCreated.UserId;
                userAuthenticationModel.PartyID = userCreated.PartyId;
            }
        }

        /// <summary>
        /// Creates a automatic username based on external identity and prefix.
        /// </summary>
        private static string CreateUserName(UserAuthenticationModel userAuthenticationModel, OidcProvider provider)
        {
            string hashedIdentity = HashNonce(userAuthenticationModel.ExternalIdentity).Substring(5, 10);
            Regex rgx = new Regex("[^a-zA-Z0-9 -]");
            hashedIdentity = rgx.Replace(hashedIdentity, string.Empty);

            return provider.UserNamePrefix + hashedIdentity.ToLower() + DateTime.Now.Millisecond;
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
                ClockSkew = TimeSpan.FromSeconds(10)
            };

            _validator.ValidateToken(originalToken, validationParameters, out _);
            _logger.LogInformation("Token is valid");

            JwtSecurityToken token = _validator.ReadJwtToken(originalToken);
            return token;
        }

        /// <summary>
        /// Find the OIDC provider based on given ISS or default oidc provider.
        /// </summary>
        private OidcProvider GetOidcProvider(string iss)
        {
            if (!string.IsNullOrEmpty(iss) && _oidcProviderSettings.ContainsKey(iss))
            {
                return _oidcProviderSettings[iss];
            }

            if (!string.IsNullOrEmpty(iss))
            {
                return _oidcProviderSettings.Where(kvp => kvp.Value.Issuer.Equals(iss)).Select(kvp => kvp.Value).FirstOrDefault();
            }

            if (!string.IsNullOrEmpty(_generalSettings.DefaultOidcProvider) && _oidcProviderSettings.ContainsKey(_generalSettings.DefaultOidcProvider))
            {
                return _oidcProviderSettings[_generalSettings.DefaultOidcProvider];
            }

            return _oidcProviderSettings.First().Value;
        }

        /// <summary>
        /// Builds URI to redirect for OIDC login for authentication
        /// Based on https://openid.net/specs/openid-connect-core-1_0.html#AuthRequest
        /// </summary>
        private string CreateAuthenticationRequest(OidcProvider provider, string state, string nonce)
        {
            string redirect_uri = GetRedirectUri(provider);
            string authorizationEndpoint = provider.AuthorizationEndpoint;
            Dictionary<string, string> oidcParams = new Dictionary<string, string>();

            // REQUIRED. Redirection URI to which the response will be sent. This URI MUST exactly match one of the Redirection URI
            // values for the Client pre-registered at the OpenID Provider, with the matching performed as described in Section 6.2.1 of
            // [RFC3986] (Simple String Comparison). When using this flow, the Redirection URI SHOULD use the https scheme; however,
            // it MAY use the http scheme, provided that the Client Type is confidential, as defined in Section 2.1 of OAuth 2.0, and
            // provided the OP allows the use of http Redirection URIs in this case. The Redirection URI MAY use an alternate scheme,
            // such as one that is intended to identify a callback into a native application.
            if (!authorizationEndpoint.Contains('?'))
            {
                authorizationEndpoint += "?redirect_uri=" + redirect_uri;
            }
            else
            {
                authorizationEndpoint += "&redirect_uri=" + redirect_uri;
            }

            // REQUIRED. OpenID Connect requests MUST contain the openid scope value. If the openid scope value is not present,
            // the behavior is entirely unspecified. Other scope values MAY be present.
            // Scope values used that are not understood by an implementation SHOULD be ignored.
            // See Sections 5.4 and 11 for additional scope values defined by this specification.
            oidcParams.Add("scope", provider.Scope);

            // REQUIRED. OAuth 2.0 Client Identifier valid at the Authorization Server.
            oidcParams.Add("client_id", provider.ClientId);

            // REQUIRED. OAuth 2.0 Response Type value that determines the authorization processing flow to be used, including what parameters
            // are returned from the endpoints used. When using the Authorization Code Flow, this value is code.
            oidcParams.Add("response_type", provider.ResponseType);

            // RECOMMENDED. Opaque value used to maintain state between the request and the callback.
            // Typically, Cross-Site Request Forgery (CSRF, XSRF)
            // mitigation is done by cryptographically binding the value of this parameter with a browser cookie.
            oidcParams.Add("state", state);

            // OPTIONAL. String value used to associate a Client session with an ID Token, and to mitigate replay attacks.
            // The value is passed through unmodified from the Authentication Request to the ID Token.
            // Sufficient entropy MUST be present in the nonce values used to prevent attackers
            // from guessing values. For implementation notes, see Section 15.5.2.
            oidcParams.Add("nonce", nonce);
            string uri = QueryHelpers.AddQueryString(authorizationEndpoint, oidcParams);

            return uri;
        }

        private string GetRedirectUri(OidcProvider provider)
        {
            string redirectUri = $"{_generalSettings.PlatformEndpoint}authentication/api/v1/authentication";

            if (provider.IncludeIssInRedirectUri)
            {
                redirectUri = redirectUri + "?iss=" + provider.IssuerKey;
            }

            return redirectUri;
        }

        private string CreateNonce(HttpContext httpContext)
        {
            string nonce = Guid.NewGuid().ToString();
            httpContext.Response.Cookies.Append(_generalSettings.OidcNonceCookieName, nonce);
            return HashNonce(nonce);
        }

        private void CreateGoToCookie(HttpContext httpContext, string goToUrl)
        {
            httpContext.Response.Cookies.Append(_generalSettings.AuthnGoToCookieName, goToUrl);
        }

        private async Task CreateTokenCookie(UserAuthenticationModel userAuthentication)
        {
            List<Claim> claims = new List<Claim>();
            string issuer = _generalSettings.AltinnOidcIssuerUrl;
            string sessionId = _guidService.NewGuid();
            userAuthentication.SessionId = sessionId;
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userAuthentication.UserID.ToString(), ClaimValueTypes.String, issuer));
            claims.Add(new Claim(AltinnCoreClaimTypes.UserId, userAuthentication.UserID.ToString(), ClaimValueTypes.String, issuer));

            if (!string.IsNullOrEmpty(userAuthentication.Username))
            {
                claims.Add(new Claim(AltinnCoreClaimTypes.UserName, userAuthentication.Username, ClaimValueTypes.String, issuer));
            }

            if (userAuthentication.PartyUuid != null)
            {
               claims.Add(new Claim(AltinnCoreClaimTypes.PartyUUID, userAuthentication.PartyUuid.ToString(), ClaimValueTypes.String, issuer));
            }

            if (!string.IsNullOrEmpty(userAuthentication.Iss))
            {
                claims.Add(new Claim(OriginalIssClaimName, userAuthentication.Iss, ClaimValueTypes.String, issuer));
            }

            claims.Add(new Claim(AltinnCoreClaimTypes.PartyID, userAuthentication.PartyID.ToString(), ClaimValueTypes.Integer32, issuer));
            claims.Add(new Claim(AltinnCoreClaimTypes.AuthenticateMethod, userAuthentication.AuthenticationMethod.ToString(), ClaimValueTypes.String, issuer));
            claims.Add(new Claim(AltinnCoreClaimTypes.AuthenticationLevel, ((int)userAuthentication.AuthenticationLevel).ToString(), ClaimValueTypes.Integer32, issuer));
            claims.Add(new Claim("jti", sessionId, ClaimValueTypes.String, issuer));

            if (userAuthentication.ProviderClaims != null && userAuthentication.ProviderClaims.Count > 0)
            {
                foreach (KeyValuePair<string, List<string>> kvp in userAuthentication.ProviderClaims)
                {
                    foreach (string claimvalue in kvp.Value)
                    {
                        claims.Add(new Claim(kvp.Key, claimvalue, ClaimValueTypes.String, issuer));
                    }
                }
            }

            if (!claims.Any(c => c.Type == AuthzConstants.CLAIM_SCOPE))
            {
                claims.Add(new Claim(AuthzConstants.CLAIM_SCOPE, AuthzConstants.SCOPE_PORTAL, ClaimValueTypes.String, issuer));
            }
            else
            {
                // Find the existing claim and modify its value
                Claim existingClaim = claims.FirstOrDefault(c => c.Type == AuthzConstants.CLAIM_SCOPE);
                if (existingClaim != null)
                {
                    claims.Remove(existingClaim);

                    // Adding portal scope to list of scopes
                    claims.Add(new Claim(AuthzConstants.CLAIM_SCOPE, existingClaim.Value + " " + AuthzConstants.SCOPE_PORTAL, ClaimValueTypes.String, issuer));
                }
            }

            ClaimsIdentity identity = new ClaimsIdentity(_generalSettings.ClaimsIdentity);
            identity.AddClaims(claims);
            ClaimsPrincipal principal = new ClaimsPrincipal(identity);
            string serializedToken = await GenerateToken(principal);
            _eventLog.CreateAuthenticationEventAsync(_featureManager, userAuthentication, AuthenticationEventType.Authenticate, HttpContext);
            CreateJwtCookieAndAppendToResponse(serializedToken);
            if (userAuthentication.TicketUpdated)
            {
                Response.Cookies.Append(_generalSettings.SblAuthCookieName, userAuthentication.EncryptedTicket);
            }
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

        private bool ValidateNonce(HttpContext context, string hashedNonce)
        {
            string nonceCookie = context.Request.Cookies[_generalSettings.OidcNonceCookieName];
            if (!string.IsNullOrEmpty(nonceCookie) && HashNonce(nonceCookie).Equals(hashedNonce))
            {
                return true;
            }

            return false;
        }

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
