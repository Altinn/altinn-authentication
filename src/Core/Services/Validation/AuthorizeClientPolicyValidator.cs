using Altinn.Platform.Authentication.Core.Models.Oidc;
using Altinn.Platform.Authentication.Core.Services.Interfaces;

namespace Altinn.Platform.Authentication.Core.Services.Validation
{
    /// <summary>Validates the authorize request against a resolved client’s configuration.</summary>
    public sealed class AuthorizeClientPolicyValidator : IAuthorizeClientPolicyValidator
    {
        public AuthorizeValidationError? ValidateClientBinding(AuthorizeRequest request, OidcClient client)
        {
            if (client is null)
            {
                return Err("unauthorized_client", "Unknown client.");
            }

            // Strict redirect_uri validation: absolute, no fragment, exact string match
            if (!request.RedirectUri.IsAbsoluteUri || !string.IsNullOrEmpty(request.RedirectUri.Fragment))
            {
                return Err("invalid_request", "redirect_uri must be absolute and must not contain a fragment.");
            }
            var redirectAllowed = client.RedirectUris.Any(
            u => string.Equals(u.ToString(), request.RedirectUri.ToString(), StringComparison.Ordinal));
            if (!redirectAllowed)
            {
                return Err("invalid_request", "redirect_uri not registered for this client.");
            }

            // Requested scopes must be subset of client’s allowed scopes
            var requested = request.Scopes ?? Array.Empty<string>();
            if (!requested.All(s => client.AllowedScopes.Contains(s)))
            {
                return Err("invalid_scope", "One or more requested scopes are not allowed for this client.");
            }

            return null;
        }

        private static AuthorizeValidationError Err(string code, string desc)
            => new() { Error = code, Description = desc };
    }
}
