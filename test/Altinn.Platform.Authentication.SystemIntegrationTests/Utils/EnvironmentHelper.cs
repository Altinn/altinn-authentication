using System.Text.Json.Serialization;
using Altinn.Platform.Authentication.SystemIntegrationTests.Domain;

namespace Altinn.Platform.Authentication.SystemIntegrationTests.Utils;

/// <summary>
/// Used for deserialization of environment file
/// </summary>
public class EnvironmentHelper
{
    /// <summary>
    /// Environment. Eg: AT22
    /// </summary>
    public required string Testenvironment { get; set; }
    
    /// <summary>
    /// Environment. Eg: AT22
    /// </summary>
    public string? MaskinportenEnvironment { get; set; }

    [JsonPropertyName("Jwks")] public required Jwk jwk { get; set; }
    
    [JsonPropertyName("JwksDev")] public Jwk? jwkDev { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [JsonPropertyName("TestCredentials")]
    public required TestCredentials testCredentials { get; set; }

    [JsonPropertyName("MaskinportenClientId")]
    public required string maskinportenClientId { get; set; }

    public required string? Vendor { get; set; }
    
    [JsonPropertyName("AuthorizationSubscriptionKeyAt22")]
    public required string AuthorizationSubscriptionKeyAt22 { get; set; }
    
    [JsonPropertyName("AuthorizationSubscriptionKeyTT02")]
    public required string AuthorizationSubscriptionKeyTT02 { get; set; }

    /// <summary>
    /// Credentials for test api
    /// </summary>
    public class TestCredentials
    {
        /// <summary>
        /// password for username
        /// </summary>
        public required string username { get; set; }

        /// <summary>
        /// password for test api
        /// </summary>
        public required string password { get; set; }
    }

    public class Jwks
    {
        public required string alg { get; set; } = "RS256"; // Algorithm
        public required string d { get; set; } // Private exponent (optional)
        public required string dp { get; set; } // First factor CRT exponent
        public required string dq { get; set; } // Second factor CRT exponent
        public required string e { get; set; } = "AQAB"; // Public exponent
        public required List<string> key_ops { get; set; } = new List<string>(); // Key operations
        public required string kid { get; set; } // Key ID
        public required string kty { get; set; } = "RSA"; // Key type
        public required string n { get; set; } // Modulus (public key part)
        public required List<object> oth { get; set; } = new List<object>(); // Other primes info (optional)
        public required string p { get; set; } // First prime factor
        public required string q { get; set; } // Second prime factor
        public required string qi { get; set; } // CRT coefficient
        public required string use { get; set; } // Intended use of the key (sig/enc)
        public required List<string> x5c { get; set; } = new(); // Certificate chain
    }
}