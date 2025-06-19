using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Altinn.Authorization.ProblemDetails;
using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Enum;
using Altinn.Platform.Authentication.Extensions;
using Altinn.Platform.Authentication.Helpers;
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
    [Route("authentication/api/v1")]
    [ApiController]
    public class LogoutController : ControllerBase
    {
        private const string OriginalIssClaimName = "originaliss";

        private readonly GeneralSettings _generalSettings;
   
        private readonly OidcProviderSettings _oidcProviderSettings;
        private readonly JwtSecurityTokenHandler _validator;

        private readonly IEventLog _eventLog;
        private readonly IFeatureManager _featureManager;
        private readonly IRequestSystemUser _requestSystemUser;
        private readonly IChangeRequestSystemUser _changeRequestSystemUser;
        private readonly IConsentService _consentService;

        /// <summary>
        /// Defay
        /// </summary>
        public LogoutController(
            ILogger<LogoutController> logger,
            IOptions<GeneralSettings> generalSettings,
            IOptions<OidcProviderSettings> oidcProviderSettings,
            IEventLog eventLog,
            IFeatureManager featureManager,
            IRequestSystemUser requestSystemUser,
            IChangeRequestSystemUser changeRequestSystemUser,
            IConsentService consentService)
        {
            _generalSettings = generalSettings.Value;
            _oidcProviderSettings = oidcProviderSettings.Value;
            _validator = new JwtSecurityTokenHandler();
            _eventLog = eventLog;
            _featureManager = featureManager;
            _requestSystemUser = requestSystemUser;
            _changeRequestSystemUser = changeRequestSystemUser;
            _consentService = consentService;
        }

        /// <summary>
        /// Logs out user
        /// </summary>
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status302Found)]
        [HttpGet("logout")]
        public ActionResult Logout()
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
            Response.Cookies.Delete(_generalSettings.AltinnLogoutInfoCookieName, opt);

            Dictionary<string, string> cookieValues = logoutInfoCookie?.Split('?')
                .Select(x => x.Split('='))
                .ToDictionary(x => x[0], x => x[1]);

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

            if (cookieValues != null && cookieValues.TryGetValue("ConsentRequestId", out string consentRequestId) && Guid.TryParse(consentRequestId, out Guid consentRequestGuid))
            {
                Result<string> redirectUrl = await _consentService.GetConsentRequestRedirectUrl(consentRequestGuid);
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
    }
}
