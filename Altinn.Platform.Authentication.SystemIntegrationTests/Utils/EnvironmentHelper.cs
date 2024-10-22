using System.Text.Json.Serialization;

namespace Altinn.AccessManagement.SystemIntegrationTests.Utils;

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
}