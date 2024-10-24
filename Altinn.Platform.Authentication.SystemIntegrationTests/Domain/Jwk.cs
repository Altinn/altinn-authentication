namespace Altinn.Platform.Authentication.SystemIntegrationTests.Domain;

/// <summary>
/// Helper class allowing deserialization of content in Resources/jwks
/// </summary>
public class Jwk
{
    public string alg { get; set; } = "RS256"; // Algorithm
    public string d { get; set; } // Private exponent (optional)
    public string dp { get; set; } // First factor CRT exponent
    public string dq { get; set; } // Second factor CRT exponent
    public string e { get; set; } = "AQAB"; // Public exponent
    public List<string> key_ops { get; set; } = new List<string>(); // Key operations
    public string kid { get; set; } // Key ID
    public string kty { get; set; } = "RSA"; // Key type
    public string n { get; set; } // Modulus (public key part)
    public List<object> oth { get; set; } = new List<object>(); // Other primes info (optional)
    public string p { get; set; } // First prime factor
    public string q { get; set; } // Second prime factor
    public string qi { get; set; } // CRT coefficient
    public string use { get; set; } // Intended use of the key (sig/enc)
    public List<string> x5c { get; set; } = new List<string>(); // Certificate chain
}