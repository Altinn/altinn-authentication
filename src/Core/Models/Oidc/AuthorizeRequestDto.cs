using Microsoft.AspNetCore.Mvc;

namespace Altinn.Platform.Authentication.Core.Models.Oidc
{
    /// <summary>
    /// Raw OIDC authorize request as received on the wire.
    /// Properties map to spec parameter names via [FromQuery]/[FromForm] name mapping.
    /// </summary>
    public sealed class AuthorizeRequestDto
    {
        [FromQuery(Name = "response_type")]
        public string? ResponseType { get; set; }

        [FromQuery(Name = "client_id")]
        public string? ClientId { get; set; }

        [FromQuery(Name = "redirect_uri")]
        public string? RedirectUri { get; set; }

        [FromQuery(Name = "scope")]
        public string? Scope { get; set; }

        [FromQuery(Name = "state")]
        public string? State { get; set; }

        [FromQuery(Name = "nonce")]
        public string? Nonce { get; set; }

        [FromQuery(Name = "code_challenge")]
        public string? CodeChallenge { get; set; }

        [FromQuery(Name = "code_challenge_method")]
        public string? CodeChallengeMethod { get; set; }

        [FromQuery(Name = "acr_values")]
        public string? AcrValues { get; set; }

        [FromQuery(Name = "prompt")]
        public string? Prompt { get; set; }

        [FromQuery(Name = "ui_locales")]
        public string? UiLocales { get; set; }

        [FromQuery(Name = "max_age")]
        public int? MaxAge { get; set; }

        // PAR & JAR
        [FromQuery(Name = "request_uri")]
        public string? RequestUri { get; set; }

        [FromQuery(Name = "request")]
        public string? RequestObject { get; set; }

        // Optional but useful soon
        [FromQuery(Name = "response_mode")]
        public string? ResponseMode { get; set; }

        [FromQuery(Name = "login_hint")]
        public string? LoginHint { get; set; }

        [FromQuery(Name = "id_token_hint")]
        public string? IdTokenHint { get; set; }

        [FromQuery(Name = "claims")]
        public string? Claims { get; set; } // JSON per OIDC

        [FromQuery(Name = "claims_locales")]
        public string? ClaimsLocales { get; set; }

        [FromQuery(Name = "authorization_details")]
        public string? AuthorizationDetails { get; set; } // JSON per RAR

        [FromQuery(Name = "resource")]
        public string? Resource { get; set; }
    }
}
