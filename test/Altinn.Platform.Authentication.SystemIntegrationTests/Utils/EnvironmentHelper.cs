using System.Text.Json.Serialization;

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
    /// 
    /// </summary>
    [JsonPropertyName("TestCredentials")]
    public required TestCredentials testCredentials { get; set; }

    /// <summary>
    /// Find client names in environment.json
    /// </summary>
    [JsonPropertyName("MaskinportenClients")]
    public required List<MaskinportenClient> MaskinportenClients { get; set; }

    public MaskinportenClient GetMaskinportenClientByName(string name)
    {
        return MaskinportenClients.Find(user => user.Name == name)
               ?? throw new Exception($"Maskinporten client with name '{name}' not found");
    }

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

    public class MaskinportenClient
    {
        public string? MaskinportenClientId { get; set; }
        public string? Name { get; set; }

        public string? PathToJwks { get; set; }
        // Constructor to set the values
    }
}