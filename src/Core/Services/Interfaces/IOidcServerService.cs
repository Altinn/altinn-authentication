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
        public Task<AuthorizeResult> Authorize(AuthorizeRequest request, CancellationToken cancellationToken);

        /// <summary>
        /// Handles an upstream callback by processing the provided input and returning the result.
        /// </summary>
        /// <param name="input">The input data required to process the upstream callback.</param>
        /// <param name="ct">A cancellation token that can be used to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains an  <see
        /// cref="UpstreamCallbackResult"/> object representing the outcome of the callback processing.</returns>
        public Task<UpstreamCallbackResult> HandleUpstreamCallback(UpstreamCallbackInput input, CancellationToken ct);

    }
}
