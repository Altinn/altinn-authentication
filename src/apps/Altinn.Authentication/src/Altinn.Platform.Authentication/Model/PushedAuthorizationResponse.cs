using System.Text.Json.Serialization;

#nullable enable

namespace Altinn.Platform.Authentication.Model
{
    /// <summary>
    /// A successful response from a provider's Pushed Authorization Request endpoint (RFC 9126).
    /// </summary>
    public sealed class PushedAuthorizationResponse
    {
        /// <summary>
        /// Reference to the pushed parameters, sent to the authorize endpoint in place of them.
        /// </summary>
        /// <remarks>
        /// A reference, not a URL to fetch or redirect to. It is short-lived and single-purpose,
        /// and is treated as a credential in logging.
        /// </remarks>
        [JsonPropertyName("request_uri")]
        public string? RequestUri { get; set; }

        /// <summary>
        /// Seconds the <see cref="RequestUri"/> remains usable. Typically 600.
        /// </summary>
        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }
}
