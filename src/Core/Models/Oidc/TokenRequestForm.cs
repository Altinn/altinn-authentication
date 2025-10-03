using Microsoft.AspNetCore.Mvc;

namespace Altinn.Platform.Authentication.Core.Models.Oidc
{
    public sealed class TokenRequestForm
    {
        [FromForm(Name = "grant_type")] public string? GrantType { get; init; }
        [FromForm(Name = "code")] public string? Code { get; init; }
        [FromForm(Name = "redirect_uri")] public string? RedirectUri { get; init; }
        [FromForm(Name = "client_id")] public string? ClientId { get; init; }
        [FromForm(Name = "client_secret")] public string? ClientSecret { get; init; }
        [FromForm(Name = "code_verifier")] public string? CodeVerifier { get; init; }

        [FromForm(Name = "refresh_token")] public string? RefreshToken { get; init; }

        [FromForm(Name = "scope")] public string? Scope { get; init; }

        // private_key_jwt support (when you add it)
        [FromForm(Name = "client_assertion_type")] public string? ClientAssertionType { get; init; }
        [FromForm(Name = "client_assertion")] public string? ClientAssertion { get; init; }
    }
}
