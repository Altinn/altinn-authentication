using Altinn.Platform.Authentication.Core.Models.Oidc;

namespace Altinn.Platform.Authentication.Core.Services.Interfaces
{
    public interface IOidcServerService
    {
        /// <summary>
        /// Authorizes a request based on the provided authorization details.
        /// </summary>
        /// <remarks>The method processes the provided <see cref="AuthorizeRequest"/> to determine whether
        /// the request is authorized. Ensure that the <paramref name="request"/> contains all required fields before
        /// calling this method.</remarks>
        /// <param name="request">The authorization request containing the necessary details to perform the authorization.  This parameter
        /// cannot be <see langword="null"/>.</param>
        public Task<AuthorizeResult> Authorize(AuthorizeRequest request);
    }
}
