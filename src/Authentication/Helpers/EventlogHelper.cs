﻿using System;
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
using Microsoft.FeatureManagement;

namespace Altinn.Platform.Authentication.Helpers
{
    /// <summary>
    /// Helper class for event logging
    /// </summary>
    public static class EventlogHelper
    {
        /// <summary>
        /// Creates an authentication event
        /// </summary>
        /// <param name="featureManager">handler for feature manager service</param>
        /// <param name="eventLog">handler for eventlog service</param>
        /// <param name="jwtToken">token in the authentication request</param>
        /// <param name="eventType">authentication event type</param>
        public async static Task CreateAuthenticationEvent(IFeatureManager featureManager, IEventLog eventLog, string jwtToken, AuthenticationEventType eventType)
        {            
            if (await featureManager.IsEnabledAsync(FeatureFlags.AuditLog))
            {
                AuthenticationEvent authenticationEvent = MapAuthenticationEvent(jwtToken, eventType);              
                eventLog.CreateAuthenticationEvent(authenticationEvent);
            }
        }

        /// <summary>
        /// Creates an authentication event
        /// </summary>
        /// <param name="featureManager">handler for feature manager service</param>
        /// <param name="eventLog">handler for eventlog service</param>
        /// <param name="authenticatedUser">authenticat</param>
        /// <param name="eventType">authentication event type</param>
        public async static Task CreateAuthenticationEvent(IFeatureManager featureManager, IEventLog eventLog, UserAuthenticationModel authenticatedUser, AuthenticationEventType eventType)
        {
            if (await featureManager.IsEnabledAsync(FeatureFlags.AuditLog))
            {
                AuthenticationEvent authenticationEvent = MapAuthenticationEvent(authenticatedUser, eventType);
                eventLog.CreateAuthenticationEvent(authenticationEvent);
            }
        }

        /// <summary>
        /// Maps claims to the authentication event model
        /// </summary>
        /// <param name="jwtToken">authenticated token</param>
        /// <param name="eventType">authentication event type</param>
        /// <returns>authentication event</returns>
        public static AuthenticationEvent MapAuthenticationEvent(string jwtToken, AuthenticationEventType eventType)
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
                                authenticationEvent.UserId = claim.Value;
                                break;

                            case AltinnCoreClaimTypes.OrgNumber:
                                authenticationEvent.OrgNumber = claim.Value;
                                break;

                            case AltinnCoreClaimTypes.AuthenticateMethod:
                                AuthenticationMethod authenticationMethod;
                                authenticationEvent.AuthenticationMethod = System.Enum.TryParse<AuthenticationMethod>(claim.Value, true, out authenticationMethod) ? authenticationMethod.ToString() : AuthenticationMethod.NotDefined.ToString();
                                                                
                                break;

                            case AltinnCoreClaimTypes.AuthenticationLevel:
                                authenticationEvent.AuthenticationLevel = ((SecurityLevel)System.Enum.Parse(typeof(SecurityLevel), claim.Value)).ToString();
                                break;

                            case "amr":
                                authenticationEvent.AuthenticationMethod = AuthenticationHelper.GetAuthenticationMethod(claim.Value).ToString();
                                break;

                            case "acr":
                                authenticationEvent.AuthenticationLevel = AuthenticationHelper.GetAuthenticationLevel(claim.Value).ToString();
                                break;
                        }
                    }

                    authenticationEvent.EventType = eventType.ToString();
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
        public static AuthenticationEvent MapAuthenticationEvent(UserAuthenticationModel authenticatedUser, AuthenticationEventType eventType)
        {
            AuthenticationEvent authenticationEvent = null;
            if (authenticatedUser != null)
            {
                authenticationEvent = new AuthenticationEvent();
                authenticationEvent.AuthenticationMethod = authenticatedUser.AuthenticationMethod.ToString();
                authenticationEvent.AuthenticationLevel = authenticatedUser.AuthenticationLevel.ToString();
                authenticationEvent.UserId = authenticatedUser.UserID.ToString();
                authenticationEvent.EventType = eventType.ToString();
            }

            return authenticationEvent;
        }
    }
}
