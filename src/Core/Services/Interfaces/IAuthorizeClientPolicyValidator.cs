using Altinn.Platform.Authentication.Core.Models.Oidc;

namespace Altinn.Platform.Authentication.Core.Services.Interfaces
{
    public interface IAuthorizeClientPolicyValidator
    {
        /// <summary>Validates request against a resolved client (redirect URI exact match, scope subset, etc.).</summary>
        AuthorizeValidationError? ValidateClientBinding(AuthorizeRequest request, OidcClient client);
    }
}
