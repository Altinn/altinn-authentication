using Mockporten.Configuration;
using Mockporten.Helpers;
using Mockporten.Models;
using Mockporten.Services;
using Mockporten.Services.Interface;
using Mockporten.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Threading.Tasks;

namespace Mockporten.Services.Implementation
{
    public class TokenService: IToken
    {
        private const string TokenUseClaim = "token_use";
        private const string TokenUseAuthzCode = "authz_code";
        private const string TokenUseRequestObject = "request_object";

        private readonly IJwtSigningCertificateProvider _certificateProvider;
        private readonly GeneralSettings _generalSettings;
        private readonly ILogger<TokenService> _logger;
        private readonly JwtSecurityTokenHandler _validator;

        public TokenService(
            IOptions<GeneralSettings> generalSettings,
            IJwtSigningCertificateProvider certificateProvider,
            ILogger<TokenService> logger)
        {
            _generalSettings = generalSettings.Value;
            _certificateProvider = certificateProvider;
            _logger = logger;
            _validator = new JwtSecurityTokenHandler();
        }

        public async Task<string> GetAuthorizationCode(OidcAuthorizationModel oidcAuthorizationModel)
        {
            string issuer = _generalSettings.IssCode;

            ClaimsPrincipal principal = GetClaimsPrincipal(Guid.NewGuid().ToString(),
                oidcAuthorizationModel.Pid,
                "nb",
                oidcAuthorizationModel.Nonce,
                Guid.NewGuid().ToString(),
                oidcAuthorizationModel.Client_id,
                oidcAuthorizationModel.Acr_values?.Split(" "),
                new[] { "bankid" },
                DateTimeOffset.Now);

            ClaimsIdentity identity = (ClaimsIdentity)principal.Identity;
            identity.AddClaim(new Claim(TokenUseClaim, TokenUseAuthzCode, ClaimValueTypes.String, issuer));

            // PKCE: bind the code_challenge into the (signed) authorization code
            // so the token endpoint can verify it statelessly. RFC 7636.
            if (!string.IsNullOrEmpty(oidcAuthorizationModel.Code_challenge))
            {
                identity.AddClaim(new Claim("code_challenge", oidcAuthorizationModel.Code_challenge, ClaimValueTypes.String, issuer));
                identity.AddClaim(new Claim("code_challenge_method",
                    string.IsNullOrEmpty(oidcAuthorizationModel.Code_challenge_method) ? Pkce.MethodS256 : oidcAuthorizationModel.Code_challenge_method,
                    ClaimValueTypes.String, issuer));
            }

            return await GenerateAccessToken(principal, DateTime.Now.AddMinutes(5));
        }

        public ClaimsPrincipal GetClaimsPrincipal(string sub, string pid, string locale, string nonce, string sid, string aud, string[] acr, string[] amr, DateTimeOffset auth_time)
        {
            List<Claim> claims = new List<Claim>();
            string issuer = _generalSettings.IssCode;
            claims.Add(new Claim("iss", issuer, ClaimValueTypes.String, issuer));
            claims.Add(new Claim("sub", sub, ClaimValueTypes.String, issuer));
            if (!string.IsNullOrEmpty(pid))
            {
                claims.Add(new Claim("pid", pid, ClaimValueTypes.String, issuer));
            }
            if (!string.IsNullOrEmpty(locale))
            {
                claims.Add(new Claim("locale", locale, ClaimValueTypes.String, issuer));
            }
            claims.Add(new Claim("nonce", nonce, ClaimValueTypes.String, issuer));
            claims.Add(new Claim("sid", sid, ClaimValueTypes.String, issuer));
            claims.Add(new Claim("aud", aud, ClaimValueTypes.String, issuer));
            if(acr == null || acr.Length == 0)
            {
                acr = new string[] { "Level4" };
            }
            claims.Add(new Claim("acr", string.Join(" ", acr), ClaimValueTypes.String, issuer));
            claims.Add(new Claim("auth_time", auth_time.ToUnixTimeSeconds().ToString(), ClaimValueTypes.DateTime, issuer));
            if (amr != null && amr.Length != 0)
            {
                var amrr = amr
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .Distinct()
                .ToArray();

                if (amrr.Length > 0)
                {
                    string amrJson = JsonSerializer.Serialize(amrr); // e.g. ["TestID","pwd"]
                    claims.Add(new Claim("amr", amrJson, JsonClaimValueTypes.JsonArray));
                }
            }

            ClaimsIdentity identity = new("mock");
            identity.AddClaims(claims);
            ClaimsPrincipal principal = new(identity);

            return principal;
        }


        public async Task<string> GetTokenFromCode(string code, string codeVerifier)
        {
            JwtSecurityToken codeToken;
            try
            {
                codeToken = await ValidateAuthorizationCode(code);
            }
            catch (Exception ex) when (ex is SecurityTokenException || ex is ArgumentException)
            {
                throw new OidcRequestException("invalid_grant", "Invalid or expired authorization code");
            }

            if (GetClaim(codeToken, TokenUseClaim) != TokenUseAuthzCode)
            {
                throw new OidcRequestException("invalid_grant", "Token is not an authorization code");
            }

            // PKCE verification (stateless - the challenge travels in the code).
            string codeChallenge = GetClaim(codeToken, "code_challenge");
            if (!string.IsNullOrEmpty(codeChallenge))
            {
                string method = GetClaim(codeToken, "code_challenge_method");
                if (!Pkce.Verify(codeChallenge, method, codeVerifier))
                {
                    throw new OidcRequestException("invalid_grant", "PKCE verification failed");
                }
            }
            else if (_generalSettings.RequirePkce)
            {
                throw new OidcRequestException("invalid_grant", "PKCE is required");
            }
            else
            {
                _logger.LogWarning("Authorization code redeemed without PKCE (RequirePkce is false)");
            }

            ClaimsIdentity identity = new("token");
            identity.AddClaims(codeToken.Claims.Where(c =>
                c.Type != "code_challenge" && c.Type != "code_challenge_method" && c.Type != TokenUseClaim));
            ClaimsPrincipal principal = new ClaimsPrincipal(identity);
            return await GenerateAccessToken(principal, DateTime.Now.AddMinutes(60));
        }

        public async Task<string> CreateRequestObject(OidcAuthorizationModel m)
        {
            string issuer = _generalSettings.IssCode;
            List<Claim> claims = new()
            {
                new Claim(TokenUseClaim, TokenUseRequestObject, ClaimValueTypes.String, issuer)
            };

            void Add(string name, string value)
            {
                if (!string.IsNullOrEmpty(value))
                {
                    claims.Add(new Claim(name, value, ClaimValueTypes.String, issuer));
                }
            }

            Add("response_type", m.Response_type);
            Add("client_id", m.Client_id);
            Add("redirect_uri", m.Redirect_uri);
            Add("scope", m.Scope);
            Add("state", m.State);
            Add("nonce", m.Nonce);
            Add("acr_values", m.Acr_values);
            Add("response_mode", m.Response_mode);
            Add("ui_locales", m.Ui_locales);
            Add("prompt", m.Prompt);
            Add("code_challenge", m.Code_challenge);
            Add("code_challenge_method", m.Code_challenge_method);
            Add("login_hint", m.Login_hint);
            Add("claims", m.Claims);

            ClaimsIdentity identity = new("par");
            identity.AddClaims(claims);

            // Short-lived (60s) signed request object - no storage (RFC 9126).
            return await GenerateAccessToken(new ClaimsPrincipal(identity), DateTime.Now.AddSeconds(60));
        }

        public async Task<OidcAuthorizationModel> ReadRequestObject(string requestUri)
        {
            const string prefix = "urn:ietf:params:oauth:request_uri:";
            if (string.IsNullOrEmpty(requestUri) || !requestUri.StartsWith(prefix, StringComparison.Ordinal))
            {
                throw new OidcRequestException("invalid_request_uri", "Unrecognized request_uri");
            }

            string jwt = requestUri.Substring(prefix.Length);
            JwtSecurityToken token;
            try
            {
                token = await ValidateAuthorizationCode(jwt);
            }
            catch (Exception ex) when (ex is SecurityTokenException || ex is ArgumentException)
            {
                throw new OidcRequestException("invalid_request_uri", "Invalid or expired request_uri");
            }

            if (GetClaim(token, TokenUseClaim) != TokenUseRequestObject)
            {
                throw new OidcRequestException("invalid_request_uri", "Token is not a request object");
            }

            return new OidcAuthorizationModel
            {
                Response_type = GetClaim(token, "response_type"),
                Client_id = GetClaim(token, "client_id"),
                Redirect_uri = GetClaim(token, "redirect_uri"),
                Scope = GetClaim(token, "scope"),
                State = GetClaim(token, "state"),
                Nonce = GetClaim(token, "nonce"),
                Acr_values = GetClaim(token, "acr_values"),
                Response_mode = GetClaim(token, "response_mode"),
                Ui_locales = GetClaim(token, "ui_locales"),
                Prompt = GetClaim(token, "prompt"),
                Code_challenge = GetClaim(token, "code_challenge"),
                Code_challenge_method = GetClaim(token, "code_challenge_method"),
                Login_hint = GetClaim(token, "login_hint"),
                Claims = GetClaim(token, "claims")
            };
        }

        private static string GetClaim(JwtSecurityToken token, string type)
        {
            return token.Claims.FirstOrDefault(c => c.Type == type)?.Value;
        }

        private async Task<string> GenerateAccessToken(ClaimsPrincipal principal, DateTime? expires = null)
        {
            List<X509Certificate2> certificates = await _certificateProvider.GetCertificates();

            X509Certificate2 certificate = GetLatestCertificateWithRolloverDelay(
                certificates, _generalSettings.JwtSigningCertificateRolloverDelayHours);

            TimeSpan tokenExpiry = new TimeSpan(0, _generalSettings.JwtValidityMinutes, 0);
            if (expires == null)
            {
                expires = DateTime.UtcNow.AddSeconds(tokenExpiry.TotalSeconds);
            }

            JwtSecurityTokenHandler tokenHandler = new JwtSecurityTokenHandler();
            SecurityTokenDescriptor tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(principal.Identity),
                Expires = expires,
                SigningCredentials = new X509SigningCredentials(certificate)
            };

            SecurityToken token = tokenHandler.CreateToken(tokenDescriptor);
            string serializedToken = tokenHandler.WriteToken(token);

            return serializedToken;
        }

        private X509Certificate2 GetLatestCertificateWithRolloverDelay(
    List<X509Certificate2> certificates, int rolloverDelayHours)
        {
            // First limit the search to just those certificates that have existed longer than the rollover delay.
            var rolloverCutoff = DateTime.Now.AddHours(-rolloverDelayHours);
            var potentialCerts =
                certificates.Where(c => c.NotBefore < rolloverCutoff).ToList();

            // If no certs could be found, then widen the search to any usable certificate.
            if (!potentialCerts.Any())
            {
                potentialCerts = certificates.Where(c => c.NotBefore < DateTime.Now).ToList();
            }

            // Of the potential certs, return the newest one.
            return potentialCerts
                .OrderByDescending(c => c.NotBefore)
                .FirstOrDefault();
        }

        private async Task<JwtSecurityToken> ValidateAuthorizationCode(string originalToken)
        {
            List<X509Certificate2> certificates = await _certificateProvider.GetCertificates();

            X509Certificate2 certificate = GetLatestCertificateWithRolloverDelay(
                certificates, _generalSettings.JwtSigningCertificateRolloverDelayHours);
            SecurityKey key = new X509SecurityKey(certificate);

            TokenValidationParameters validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateIssuer = false,
                ValidateAudience = false,
                RequireExpirationTime = true,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            _validator.ValidateToken(originalToken, validationParameters, out _);

            JwtSecurityToken token = _validator.ReadJwtToken(originalToken);
            return token;
        }
    }
}
