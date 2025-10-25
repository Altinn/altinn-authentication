using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Altinn.Common.Authentication.Configuration;
using Altinn.Common.Authentication.Models;
using Altinn.Common.Authentication.Utils;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace AltinnCore.Authentication.JwtCookie
{
    /// <summary>
    /// This handles a asp.net core application running with JWT tokens in cookie.
    /// Code is inspired by ASP.Net Core CookieAuthentication
    /// </summary>
    public class JwtCookieHandler : AuthenticationHandler<JwtCookieOptions>
    {
        private readonly IOptionsMonitor<OidcProviderSettings> _oidcProviderSettings;
        private readonly TimeProvider _timeProvider;

        /// <summary>
        /// The default constructor
        /// </summary>
        /// <param name="options">The options</param>
        /// <param name="oidcProviderSettings">The settings related to oidc providers</param>
        /// <param name="logger">The logger</param>
        /// <param name="encoder">The Url encoder</param>
        /// <param name="timeProvider">The timeprovider</param>
        public JwtCookieHandler(
            IOptionsMonitor<JwtCookieOptions> options,
            IOptionsMonitor<OidcProviderSettings> oidcProviderSettings,
            ILoggerFactory logger,
            UrlEncoder encoder,
            TimeProvider timeProvider)
            : base(options, logger, encoder)
        {
            _oidcProviderSettings = oidcProviderSettings;
            _timeProvider = timeProvider;
        }

        /// <summary>
        /// The handler calls methods on the events which give the application control at certain points where processing is occurring.
        /// If it is not provided a default instance is supplied which does nothing when the methods are called.
        /// </summary>
        protected new JwtCookieEvents Events
        {
            get => (JwtCookieEvents)base.Events;
            set => base.Events = value;
        }

        /// <summary>
        /// Creates a new instance of the events instance.
        /// </summary>
        /// <returns>A new instance of the events instance.</returns>
        protected override Task<object> CreateEventsAsync() => Task.FromResult<object>(new JwtCookieEvents());

        /// <summary>
        ///  Handles the authentication of the request
        /// </summary>
        /// <returns></returns>
        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var settings = _oidcProviderSettings.CurrentValue;

            try
            {
                string token = string.Empty;

                // First get the token from authorization header
                string authorization = Request.Headers["Authorization"];

                // If no authorization header found, get the token
                if (!string.IsNullOrEmpty(authorization))
                {
                    if (authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    {
                        token = authorization.Substring("Bearer ".Length).Trim();
                    }
                }

                // If the token is not found in authorization header, get the token from cookie
                if (string.IsNullOrEmpty(token))
                {
                    // Try to get the cookie from the default cookie value
                    token = Options.CookieManager.GetRequestCookie(Context, JwtCookieDefaults.AltinnTokenCookie);

                    if (string.IsNullOrEmpty(token))
                    {
                        // Get the cookie from the configured cookie value
                        token = Options.CookieManager.GetRequestCookie(Context, Options.JwtCookieName);
                    }
                }

                // If no token found, return no result
                if (string.IsNullOrEmpty(token))
                {
                    return AuthenticateResult.NoResult();
                }

                JwtSecurityTokenHandler validator = new JwtSecurityTokenHandler();
                if (!validator.CanReadToken(token))
                {
                    return AuthenticateResult.Fail("No SecurityTokenValidator available for token: " + token);
                }

                TokenValidationParameters validationParameters = null;
                string issuer = validator.ReadJwtToken(token).Issuer;

                if (issuer != null && settings.Count > 0)
                {
                    foreach (KeyValuePair<string, OidcProvider> provider in settings)
                    {
                        if (provider.Value.Issuer == issuer)
                        {
                            // Match found for configured OIDC Provider. Set up validator and get signing keys
                            validationParameters = new TokenValidationParameters()
                            {
                                ValidateIssuerSigningKey = true,
                                ValidateAudience = false,
                                RequireExpirationTime = true,
                                ValidateLifetime = true,
                                ClockSkew = TimeSpan.FromSeconds(10)
                            };

                            validationParameters.LifetimeValidator = (nbf, exp, _, tvp) =>
                            {
                                var now = _timeProvider.GetUtcNow(); // DateTimeOffset
                                if (nbf.HasValue && nbf.Value > now + tvp.ClockSkew)
                                {
                                    return false;
                                }

                                if (exp.HasValue && exp.Value < now - tvp.ClockSkew)
                                {
                                    return false;
                                }

                                return true;
                            };

                            OpenIdConnectConfiguration configuration = await GetOidcConfiguration(provider.Value.WellKnownConfigEndpoint);
                            if (configuration != null)
                            {
                                var issuers = new[] { configuration.Issuer };
                                validationParameters.ValidIssuers = validationParameters.ValidIssuers?.Concat(issuers) ?? issuers;
                                validationParameters.IssuerSigningKeys = validationParameters.IssuerSigningKeys?.Concat(configuration.SigningKeys) ?? configuration.SigningKeys;
                            }

                            break;
                        }
                    }
                }

                if (validationParameters == null && Options.ConfigurationManager != null)
                {
                    // Use standard configured OIDC config for JTWCookie provider from startup.
                    validationParameters = Options.TokenValidationParameters.Clone();
                    validationParameters.ClockSkew = TimeSpan.FromSeconds(10);
                    validationParameters.LifetimeValidator = (nbf, exp, _, tvp) =>
                    {
                        var now = _timeProvider.GetUtcNow(); // DateTimeOffset
                        if (nbf.HasValue && nbf.Value > now + tvp.ClockSkew)
                        {
                            return false;
                        }

                        if (exp.HasValue && exp.Value < now - tvp.ClockSkew)
                        {
                            return false;
                        }

                        return true;
                    };

                    OpenIdConnectConfiguration configuration = await Options.ConfigurationManager.GetConfigurationAsync(Context.RequestAborted);

                    if (configuration != null)
                    {
                        var issuers = new[] { configuration.Issuer };
                        validationParameters.ValidIssuers = validationParameters.ValidIssuers?.Concat(issuers) ?? issuers;
                        validationParameters.IssuerSigningKeys = validationParameters.IssuerSigningKeys?.Concat(configuration.SigningKeys) ?? configuration.SigningKeys;
                    }
                }

                ClaimsPrincipal principal;
                SecurityToken validatedToken;
                try
                {
                    principal = validator.ValidateToken(token, validationParameters, out validatedToken);
                }
                catch (Exception ex)
                {
                    Logger.LogInformation("Failed to validate token.");

                    // Refresh the configuration for exceptions that may be caused by key rollovers. The user can also request a refresh in the event.
                    if (Options.RefreshOnIssuerKeyNotFound &&
                        Options.ConfigurationManager != null &&
                        ex is SecurityTokenSignatureKeyNotFoundException)
                    {
                        Options.ConfigurationManager.RequestRefresh();
                    }

                    JwtCookieFailedContext jwtCookieFailedContext = new JwtCookieFailedContext(Context, Scheme, Options)
                    {
                        Exception = ex
                    };

                    await Events.AuthenticationFailed(jwtCookieFailedContext);
                    if (jwtCookieFailedContext.Result != null)
                    {
                        return jwtCookieFailedContext.Result;
                    }

                    return AuthenticateResult.Fail(jwtCookieFailedContext.Exception);
                }

                Logger.LogInformation("Successfully validated the token.");
                JwtCookieValidatedContext jwtCookieValidatedContext = new JwtCookieValidatedContext(Context, Scheme, Options)
                {
                    Principal = principal,
                    SecurityToken = validatedToken
                };

                await Events.TokenValidated(jwtCookieValidatedContext);

                if (jwtCookieValidatedContext.Result != null)
                {
                    return jwtCookieValidatedContext.Result;
                }

                jwtCookieValidatedContext.Success();
                return jwtCookieValidatedContext.Result;
            }
            catch (Exception ex)
            {
                Logger.LogInformation("Exception occurred while processing message.");
                JwtCookieFailedContext jwtCookieFailedContext = new JwtCookieFailedContext(Context, Scheme, Options)
                {
                    Exception = ex
                };

                await Events.AuthenticationFailed(jwtCookieFailedContext);
                if (jwtCookieFailedContext.Result != null)
                {
                    return jwtCookieFailedContext.Result;
                }

                throw;
            }
        }

        /// <summary>
        /// Get the signing keys published by the given endpoint.
        /// </summary>
        /// <param name="wellKnownEndpoint">The url of the endpoint</param>
        /// <returns>The signing keys published by the endpoint</returns>
        public async Task<OpenIdConnectConfiguration> GetOidcConfiguration(string wellKnownEndpoint)
        {
            string configKey = wellKnownEndpoint.ToLower();
            ConfigurationManager<OpenIdConnectConfiguration> configurationManager;
            if (ConfigurationMangerUtil.Instance.ConfigManagers.ContainsKey(configKey))
            {
                configurationManager = ConfigurationMangerUtil.Instance.ConfigManagers[configKey];
            }
            else
            {
                configurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                   wellKnownEndpoint,
                   new OpenIdConnectConfigurationRetriever(),
                   new HttpDocumentRetriever());
                ConfigurationMangerUtil.Instance.ConfigManagers[configKey] = configurationManager;
            }

            OpenIdConnectConfiguration discoveryDocument = await configurationManager.GetConfigurationAsync();
            return discoveryDocument;
        }
    }
}
