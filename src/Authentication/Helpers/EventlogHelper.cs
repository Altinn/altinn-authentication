using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Enum;
using Altinn.Platform.Authentication.Model;
using Altinn.Platform.Authentication.Services.Interfaces;
using AltinnCore.Authentication.Constants;
using Microsoft.AspNetCore.Http;
using Microsoft.FeatureManagement;

namespace Altinn.Platform.Authentication.Helpers
{
    /// <summary>
    /// Helper class for event logging
    /// </summary>
    public static class EventlogHelper
    {
        /// <summary>
        /// Maps claims to the authentication event model
        /// </summary>
        /// <param name="jwtToken">authenticated token</param>
        /// <param name="eventType">authentication event type</param>
        /// <returns>authentication event</returns>
        public static AuthenticationEvent MapAuthenticationEvent(string jwtToken, AuthenticationEventType eventType, HttpContext context, DateTime currentDateTime, bool isAuthenticated = true)
        {
            JwtSecurityTokenHandler tokenHandler = new JwtSecurityTokenHandler();
            AuthenticationEvent authenticationEvent = null;
            if (!string.IsNullOrEmpty(jwtToken))
            {
                JwtSecurityToken token = tokenHandler.ReadJwtToken(jwtToken);

                if (token != null)
                {
                    authenticationEvent = new AuthenticationEvent();
                    foreach (Claim claim in token.Claims)
                    {
                        // Handle various claim types
                        switch (claim.Type)
                        {
                            case AltinnCoreClaimTypes.UserId:
                                authenticationEvent.UserId = Convert.ToInt32(claim.Value);
                                break;

                            case AltinnCoreClaimTypes.OrgNumber:
                                authenticationEvent.OrgNumber = Convert.ToInt32(claim.Value);
                                break;

                            case AltinnCoreClaimTypes.AuthenticateMethod:
                                AuthenticationMethod authenticationMethod;
                                authenticationEvent.AuthenticationMethod = System.Enum.TryParse<AuthenticationMethod>(claim.Value, true, out authenticationMethod) ? authenticationMethod : AuthenticationMethod.NotDefined;
                                                                
                                break;

                            case AltinnCoreClaimTypes.AuthenticationLevel:
                                authenticationEvent.AuthenticationLevel = (SecurityLevel)System.Enum.Parse(typeof(SecurityLevel), claim.Value);
                                break;

                            case "amr":
                                authenticationEvent.AuthenticationMethod = AuthenticationHelper.GetAuthenticationMethod(claim.Value);
                                break;

                            case "acr":
                                authenticationEvent.AuthenticationLevel = AuthenticationHelper.GetAuthenticationLevelForIdPorten(claim.Value);
                                break;
                        }
                    }

                    authenticationEvent.Created = currentDateTime;
                    authenticationEvent.EventType = eventType;
                    authenticationEvent.IpAddress = GetClientIpAddress(context);
                    authenticationEvent.IsAuthenticated = isAuthenticated;                    
                }

                return authenticationEvent;
            }

            return authenticationEvent;
        }

        /// <summary>
        /// Maps authenticated user information to authentication event
        /// </summary>
        /// <param name="authenticatedUser">authenticated user</param>
        /// <param name="eventType">type of authentication event</param>
        /// <returns>authentication event</returns>
        public static AuthenticationEvent MapAuthenticationEvent(UserAuthenticationModel authenticatedUser, AuthenticationEventType eventType, HttpContext context, DateTime currentDateTime)
        {
            AuthenticationEvent authenticationEvent = null;
            if (authenticatedUser != null)
            {
                authenticationEvent = new AuthenticationEvent();
                authenticationEvent.Created = currentDateTime;
                authenticationEvent.AuthenticationMethod = authenticatedUser.AuthenticationMethod;
                authenticationEvent.AuthenticationLevel = authenticatedUser.AuthenticationLevel;
                authenticationEvent.UserId = authenticatedUser.UserID;
                authenticationEvent.EventType = eventType;
                authenticationEvent.IpAddress = GetClientIpAddress(context);
                authenticationEvent.IsAuthenticated = authenticatedUser.IsAuthenticated;                
            }

            return authenticationEvent;
        }

        /// <summary>
        /// Get the client ip address
        /// </summary>
        /// <param name="context">the http request context</param>
        /// <returns></returns>
        public static string GetClientIpAddress(HttpContext context)
        {
            // Try to get the client IP address from the X-Real-IP header
            var clientIp = context?.Request?.Headers["X-Real-IP"].FirstOrDefault();

            // If the X-Real-IP header is not present, fall back to the RemoteIpAddress property
            if (string.IsNullOrEmpty(clientIp))
            {
                clientIp = context?.Request?.Headers["X-Forwarded-For"].FirstOrDefault();
            }

            // If the X-Forwarded-For header is not present, fall back to the RemoteIpAddress property
            if (string.IsNullOrEmpty(clientIp))
            {
                clientIp = context?.Connection?.RemoteIpAddress?.ToString();
            }

            return clientIp;
        }
    }
}
