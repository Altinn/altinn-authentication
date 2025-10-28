#nullable enable
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Authorization.ProblemDetails;
using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Core.Models.Oidc;
using Altinn.Platform.Authentication.Core.Services.Interfaces;
using Altinn.Platform.Authentication.Enum;
using Altinn.Platform.Authentication.Model;
using Altinn.Platform.Authentication.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.FeatureManagement;

namespace Altinn.Platform.Authentication.Controllers
{
    /// <summary>
    /// Controller responsible for loging out
    /// </summary>
    /// <remarks>
    /// Defay
    /// </remarks>
    [Route("authentication/api/v1")]
    [ApiController]
    public class LogoutController(
        ILogger<LogoutController> logger,
        IOptions<GeneralSettings> generalSettings,
        IOptions<OidcProviderSettings> oidcProviderSettings,
        IEventLog eventLog,
        IFeatureManager featureManager,
        IRequestSystemUser requestSystemUser,
        IChangeRequestSystemUser changeRequestSystemUser,
        IOidcServerService oidcServerService) : ControllerBase
    {
        private const string OriginalIssClaimName = "originaliss";

        private readonly GeneralSettings _generalSettings = generalSettings.Value;
   
        private readonly OidcProviderSettings _oidcProviderSettings = oidcProviderSettings.Value;
        private readonly JwtSecurityTokenHandler _validator = new JwtSecurityTokenHandler();

        private readonly IEventLog _eventLog = eventLog;
        private readonly IFeatureManager _featureManager = featureManager;
        private readonly IRequestSystemUser _requestSystemUser = requestSystemUser;
        private readonly IChangeRequestSystemUser _changeRequestSystemUser = changeRequestSystemUser;
        private readonly IOidcServerService _oidcServerService = oidcServerService;

        /// <summary>
        /// Logs out user. This uses OIDC end session endpoint if enabled.
        /// This is the legacy endpoint used by Altinn Studio and Altinn Apps.
        /// See also OIDCForntChannel controller end_session endpoint.
        /// </summary>
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status302Found)]
        [HttpGet("logout")]
        public async Task<ActionResult> Logout(CancellationToken cancellationToken = default)
        {
            if (_generalSettings.AuthorizationServerEnabled)
            {
                System.Net.IPAddress? ip = HttpContext.Connection.RemoteIpAddress;
                string ua = Request.Headers.UserAgent.ToString();
                string? userAgentHash = string.IsNullOrWhiteSpace(ua) ? null : ua; // hash if you want, not required here

                EndSessionInput input = new()
                {
                    IdTokenHint = null,
                    PostLogoutRedirectUri = null,
                    State = null,
                    User = HttpContext.User,
                    ClientIp = ip,
                    UserAgentHash = userAgentHash
                };

                EndSessionResult result = await _oidcServerService.EndSessionAsync(input, cancellationToken);

                SetCacheHeaders();

                // Apply cookie instructions produced by the service
                SetCookies(result.Cookies);

                if (result.RedirectUri is not null)
                {
                    return Redirect(result.RedirectUri.ToString());
                }

                return Redirect(_generalSettings.BaseUrl);
            }
            else
            {
                JwtSecurityToken jwt = null;
                string orgIss = null;
                string tokenCookie = Request.Cookies[_generalSettings.JwtCookieName];
                if (_validator.CanReadToken(tokenCookie))
                {
                    jwt = _validator.ReadJwtToken(tokenCookie);
                    orgIss = jwt.Claims.Where(c => c.Type.Equals(OriginalIssClaimName)).Select(c => c.Value).FirstOrDefault();
                }

                OidcProvider provider = GetOidcProvider(orgIss);
                if (provider == null)
                {
                    _eventLog.CreateAuthenticationEventAsync(_featureManager, tokenCookie, AuthenticationEventType.Logout, HttpContext);
                    return Redirect(_generalSettings.SBLLogoutEndpoint);
                }

                CookieOptions opt = new CookieOptions() { Domain = _generalSettings.HostName, Secure = true, HttpOnly = true };
                Response.Cookies.Delete(_generalSettings.SblAuthCookieName, opt);
                Response.Cookies.Delete(_generalSettings.JwtCookieName, opt);

                _eventLog.CreateAuthenticationEventAsync(_featureManager, tokenCookie, AuthenticationEventType.Logout, HttpContext);
                return Redirect(provider.LogoutEndpoint);
            }
        }

        /// <summary>
        /// Redirects user to specific url if AltinnLogoutInfo is set
        /// </summary>
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status302Found)]
        [HttpGet("logout/handleloggedout")]
        public async Task<ActionResult> HandleLoggedOut()
        {
            string logoutInfoCookie = Request.Cookies[_generalSettings.AltinnLogoutInfoCookieName];
            CookieOptions opt = new CookieOptions() { Domain = _generalSettings.HostName, Secure = true, HttpOnly = true };
            
            Dictionary<string, string> cookieValues = logoutInfoCookie?.Split('?')
                .Select(x => x.Split(['='], 2))
                .ToDictionary(x => x[0], x => x[1]);

            // if amSafeRedirectUrl is set in cookie, the am bff handles the redirect and deletes cookie
            if (cookieValues != null && cookieValues.ContainsKey("amSafeRedirectUrl"))
            {
                string bffUrl = $"https://am.ui.{_generalSettings.HostName}/accessmanagement/api/v1/logoutredirect";
                return Redirect(bffUrl);
            }

            Response.Cookies.Delete(_generalSettings.AltinnLogoutInfoCookieName, opt);

            if (cookieValues != null && cookieValues.TryGetValue("SystemuserRequestId", out string requestId) && Guid.TryParse(requestId, out Guid requestGuid))
            {
                Result<string> redirectUrl = await _requestSystemUser.GetRedirectByRequestId(requestGuid);
                return Redirect(redirectUrl.Value);
            }

            if (cookieValues != null && cookieValues.TryGetValue("SystemuserChangeRequestId", out string changeRequestId) && Guid.TryParse(changeRequestId, out Guid changeRequestGuid))
            {
                Result<string> redirectUrl = await _changeRequestSystemUser.GetRedirectByChangeRequestId(changeRequestGuid);
                return Redirect(redirectUrl.Value);
            }

            if (cookieValues != null && cookieValues.TryGetValue("SystemuserAgentRequestId", out string agentRequestId) && Guid.TryParse(agentRequestId, out Guid agentRequestGuid))
            {
                Result<string> redirectUrl = await _requestSystemUser.GetRedirectByAgentRequestId(agentRequestGuid);
                return Redirect(redirectUrl.Value);
            }

            return Redirect(_generalSettings.SBLLogoutEndpoint);
        }

        /// <summary>
        /// Frontchannel logout for OIDC
        /// </summary>
        /// <returns></returns>
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [HttpGet("frontchannel_logout")]
        public ActionResult FrontchannelLogout()
        {
            CookieOptions opt = new CookieOptions() { Domain = _generalSettings.HostName, Secure = true, HttpOnly = true };
            Response.Cookies.Delete(_generalSettings.SblAuthCookieName, opt);
            Response.Cookies.Delete(_generalSettings.JwtCookieName, opt);
            string tokenCookie = Request.Cookies[_generalSettings.JwtCookieName];
            _eventLog.CreateAuthenticationEventAsync(_featureManager, tokenCookie, AuthenticationEventType.Logout, HttpContext);
            return Ok();
        }

        private OidcProvider GetOidcProvider(string iss)
        {
            if (!string.IsNullOrEmpty(iss) && _oidcProviderSettings.ContainsKey(iss))
            {
                return _oidcProviderSettings[iss];
            }

            return null;
        }

        private void SetCacheHeaders()
        {
            Response.Headers.CacheControl = "no-store";
            Response.Headers.Pragma = "no-cache";
        }

        private void SetCookies(IReadOnlyList<CookieInstruction> Cookies)
        {
            foreach (CookieInstruction c in Cookies)
            {
                Response.Cookies.Append(c.Name, c.Value ?? string.Empty, new CookieOptions
                {
                    HttpOnly = c.HttpOnly,
                    Secure = c.Secure,
                    Path = c.Path ?? "/",
                    Domain = c.Domain,
                    Expires = c.Expires,
                    SameSite = c.SameSite
                });
            }
        }
    }
}
