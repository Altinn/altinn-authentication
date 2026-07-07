using System.Runtime.Serialization;

using Microsoft.AspNetCore.Mvc;

namespace Altinn.Platform.Authentication.Model
{
    /// <summary>
    /// Introspective request model
    /// </summary>
    [DataContract]
    public class IntrospectionRequest
    {
        /// <summary>
        /// Gets or sets the token. Nullable because presence is validated in the controller
        /// (returns a specific BadRequest when empty) rather than via model-binding required-ness.
        /// </summary>
        [FromForm(Name = "token")]
        public string? Token { get; set; }

        /// <summary>
        /// Gets or sets the token type hint. Optional per RFC 7662.
        /// </summary>
        [FromForm(Name = "token_type_hint")]
        public string? TokenTypeHint { get; set; }
    }
}
