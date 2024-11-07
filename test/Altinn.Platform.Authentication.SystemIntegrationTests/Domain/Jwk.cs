namespace Altinn.Platform.Authentication.SystemIntegrationTests.Domain;

/// <summary>
/// Helper class allowing deserialization of content in Resources/jwks
/// </summary>
public class Jwk
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