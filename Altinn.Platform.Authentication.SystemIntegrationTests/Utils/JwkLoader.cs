using System.Text.Json;
using Altinn.Platform.Authentication.SystemIntegrationTests.Domain;

namespace Altinn.Platform.Authentication.SystemIntegrationTests.Utils;

public static class JwkLoader
{
    public static Jwk LoadJwk(string? localFilePath)
    {
        // Try loading from environment variable (Github actions)
        var jwkJson = Environment.GetEnvironmentVariable("JWKS_JSON");
        if (!string.IsNullOrEmpty(jwkJson))
        {
            var jwk = JsonSerializer.Deserialize<Jwk>(jwkJson);
            if (jwk != null)
            {
                return jwk;
            }
            throw new Exception("Failed to deserialize JWK from environment variable.");
        }

        // Fallback to loading from a local file
        var jwkString = Helper.ReadFile(localFilePath).Result;
        var jwkFromFile = JsonSerializer.Deserialize<Jwk>(jwkString);
        return jwkFromFile ?? throw new Exception("Unable to read JWK from file.");
    }
}