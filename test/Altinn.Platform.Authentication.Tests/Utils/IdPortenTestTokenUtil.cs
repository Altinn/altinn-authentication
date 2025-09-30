using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using Altinn.Platform.Authentication.Model;

namespace Altinn.Platform.Authentication.Tests.Utils
{
    public static class IdPortenTestTokenUtil
    {
        public static OidcCodeResponse GetIdPortenTokenResponse(string pid, string nonce, string sid, string[] acr, string client_id, string[] scope)
        {
            string sub = Guid.NewGuid().ToString(); 
            string locale = "nb";
            string amr = "BankID Mobil";
            string digDirOrgNo = "991825827";
            DateTimeOffset auth_time = DateTimeOffset.Now;
            DateTimeOffset iat = DateTimeOffset.Now;

            OidcCodeResponse response = new()
            {
                TokenType = "bearer",
                ExpiresIn = 600,
                RefreshToken = "dummy-refresh",
                RefreshTokenExpiresIn = 600,
                Scope = string.Join(' ', scope),
                IdToken = GetIdPortenIDToken(sub, pid, locale, nonce, sid, client_id, acr, amr, auth_time),
                AccessToken = GetIdPortenAccessToken(sub, "unspecificed", acr, client_id, "virksomhetssertifikat", digDirOrgNo, scope, pid, iat)
            };
            return response;
        }

        public static string GetIdPortenIDToken(string sub, string pid, string locale, string nonce, string sid, string aud, string[] acr, string amr, DateTimeOffset auth_time)
        {
            List<Claim> claims = new List<Claim>();
            string issuer = "www.idporten.no";
            claims.Add(new Claim("iss", issuer, ClaimValueTypes.String, issuer));
            claims.Add(new Claim("sub", sub, ClaimValueTypes.String, issuer));
            claims.Add(new Claim("pid", pid, ClaimValueTypes.String, issuer));
            claims.Add(new Claim("locale", locale, ClaimValueTypes.String, issuer));
            claims.Add(new Claim("nonce", nonce, ClaimValueTypes.String, issuer));
            claims.Add(new Claim("sid", sid, ClaimValueTypes.String, issuer));
            claims.Add(new Claim("aud", aud, ClaimValueTypes.String, issuer));
            claims.Add(new Claim("acr", string.Join(" ", acr), ClaimValueTypes.String, issuer));
            claims.Add(new Claim("auth_time", auth_time.ToUnixTimeSeconds().ToString(), ClaimValueTypes.DateTime, issuer));
            claims.Add(new Claim("amr", "[\"TestID\"]", JsonClaimValueTypes.JsonArray));
            ClaimsIdentity identity = new ClaimsIdentity("mock");
            identity.AddClaims(claims);
            ClaimsPrincipal principal = new(identity);
            string token = JwtTokenMock.GenerateToken(principal, new TimeSpan(1, 1, 1));
            return token;
        }

        public static string GetIdPortenAccessToken(string sub, string aud, string[] acr, string client_id, string client_amr, string consumer, string[] scope, string pid, DateTimeOffset iat)
        {
            List<Claim> claims = new List<Claim>();
            string issuer = "www.idporten.no";
            claims.Add(new Claim("iss", issuer, ClaimValueTypes.String, issuer));
            claims.Add(new Claim("sub", sub, ClaimValueTypes.String, issuer));
            claims.Add(new Claim("aud", aud, ClaimValueTypes.String, issuer));
            claims.Add(new Claim("acr", string.Join(" ", acr), ClaimValueTypes.String, issuer));
            claims.Add(new Claim("client_id", client_id, ClaimValueTypes.String, issuer));
            claims.Add(new Claim("client_amr", client_amr, ClaimValueTypes.String, issuer));
            claims.Add(new Claim("consumer", consumer, ClaimValueTypes.String, issuer));
            claims.Add(new Claim("scope", string.Join(' ', scope), ClaimValueTypes.String, issuer));
            claims.Add(new Claim("pid", pid, ClaimValueTypes.String, issuer));
            claims.Add(new Claim("iat", iat.ToUnixTimeSeconds().ToString(), ClaimValueTypes.DateTime, issuer));
            ClaimsIdentity identity = new("mock");
            identity.AddClaims(claims);
            ClaimsPrincipal principal = new(identity);
            string token = JwtTokenMock.GenerateToken(principal, new TimeSpan(1, 1, 1));
            return token;
        }
    }
}
