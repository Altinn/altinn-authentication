using Altinn.Platform.Authentication.Core.Models.Oidc;

namespace Altinn.Platform.Authentication.Core.Services.Interfaces
{
    public interface IAuthorizeRequestValidator
    {
        /// <summary>Validates the basic OIDC semantics (pre–client lookup).</summary>
        /// <returns>null if OK; otherwise a single error describing the first failure.</returns>
        AuthorizeValidationError? ValidateBasics(AuthorizeRequest request);
    }
}
