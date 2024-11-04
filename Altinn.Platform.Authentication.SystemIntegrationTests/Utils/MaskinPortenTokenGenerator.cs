using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Altinn.Platform.Authentication.SystemIntegrationTests.Domain;
using Microsoft.IdentityModel.Tokens;
using Xunit;
using JwtRegisteredClaimNames = Microsoft.IdentityModel.JsonWebTokens.JwtRegisteredClaimNames;

namespace Altinn.Platform.Authentication.SystemIntegrationTests.Utils;

/// <summary>
/// Helper class to generate a machineporten token
/// </summary>
public class MaskinPortenTokenGenerator
{
    private static string ToStandardBase64(string? base64Url)
    {
        Assert.True(base64Url != null, "Base64 url should not be null");
        var base64 = base64Url.Replace('-', '+').Replace('_', '/');
        switch (base64.Length % 4)
        {
            case 2:
                base64 += "==";
                break;
            case 3:
                base64 += "=";
                break;
        }

        return base64;
    }

    public static Task<string> GenerateJwt(Jwk jwk, EnvironmentHelper.MaskinportenClient client)
    {
        const string audience = "https://test.maskinporten.no/token";
        var iss = client.MaskinportenClientId;

        Assert.True(iss != null, "iss is null somehow, check it");

        const string scope = "altinn:authentication/systemuser.request.write altinn:authentication/systemregister.write";
        //const string scope = "altinn:authentication/systemregister.write";

        // Set the current time and expiration time for the token
        var now = DateTimeOffset.UtcNow;
        var exp = now.AddMinutes(1).ToUnixTimeSeconds();
        var iat = now.ToUnixTimeSeconds();
        var jti = Guid.NewGuid().ToString(); // Unique ID for the JWT   

        // Create RSA key from your JSON key parameters
        var rsa = new RSACryptoServiceProvider();
        rsa.ImportParameters(new RSAParameters
        {
            Modulus = Convert.FromBase64String(ToStandardBase64(
                jwk.n)), // n
            Exponent = Convert.FromBase64String("AQAB"), // e
            D = Convert.FromBase64String(ToStandardBase64(
                jwk.d)), // d
            P = Convert.FromBase64String(ToStandardBase64(
                jwk.p)), // p
            Q = Convert.FromBase64String(ToStandardBase64(
                jwk.q)), // q
            DP = Convert.FromBase64String(ToStandardBase64(
                jwk.dp)), // dp
            DQ = Convert.FromBase64String(ToStandardBase64(
                jwk.dq)), // dq
            InverseQ = Convert.FromBase64String(ToStandardBase64(
                jwk.qi)) // qi
        });

        var signingCredentials = new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Aud, audience),
            new Claim(JwtRegisteredClaimNames.Iss, iss),
            new Claim("scope", scope),
            new Claim(JwtRegisteredClaimNames.Exp, exp.ToString(), ClaimValueTypes.Integer64),
            new Claim(JwtRegisteredClaimNames.Iat, iat.ToString(), ClaimValueTypes.Integer64),
            new Claim(JwtRegisteredClaimNames.Jti, jti)
        };

        var header = new JwtHeader(signingCredentials)
        {
            { "kid", jwk.kid }
        };

        var payload = new JwtPayload(claims);

        // Create the JWT token manually with the custom header
        var token = new JwtSecurityToken(header, payload);
        var tokenHandler = new JwtSecurityTokenHandler();

        // Write and return the JWT
        return Task.FromResult(tokenHandler.WriteToken(token));
    }

    /// <summary>
    /// Returns a Maskinporten token based on a given pregenerated jwt
    /// </summary>
    /// <param name="jwt">The generated jwt needed for the assertion parameter</param>
    /// <returns></returns>
    /// <exception cref="Exception">Throws a failure if unable to retrieve token</exception>
    public static async Task<string> RequestToken(string jwt)
    {
        using var client = new HttpClient();
        var requestContent = new FormUrlEncodedContent([
            new KeyValuePair<string, string>("grant_type", "urn:ietf:params:oauth:grant-type:jwt-bearer"),
            new KeyValuePair<string, string>("assertion", jwt)
        ]);

        var response = await client.PostAsync("https://test.maskinporten.no/token", requestContent);

        if (response.IsSuccessStatusCode)
        {
            // Read the response body
            var responseBody = await response.Content.ReadAsStringAsync();
            return responseBody;
        }

        throw new Exception(
            $"Failed to retrieve token: {response.StatusCode} {await response.Content.ReadAsStringAsync()}");
    }

    /// <summary>
    /// This fetches a bearer token from Maskinporten
    /// </summary>
    /// <param name="maskinportenClient"></param>
    /// <returns></returns>
    /// <exception cref="Exception">Gives an exception if unable to find access token in jsonDoc response</exception>
    public static async Task<string> GetMaskinportenBearerToken(EnvironmentHelper.MaskinportenClient maskinportenClient)
    {
        var jwk = JwkLoader.LoadJwk(maskinportenClient.PathToJwks);

        var jwt = await GenerateJwt(jwk, maskinportenClient);
        var maskinportenTokenResponse = await RequestToken(jwt);
        var jsonDoc = JsonDocument.Parse(maskinportenTokenResponse);
        var root = jsonDoc.RootElement;

        return root.GetProperty("access_token").GetString() ??
               throw new Exception("Unable to get access token from response.");
    }
}