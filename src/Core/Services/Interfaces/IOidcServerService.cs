using Altinn.Platform.Authentication.Core.Models.Oidc;
using System.Security.Claims;

namespace Altinn.Platform.Authentication.Core.Services.Interfaces
{
    public interface IOidcServerService
    {
        /// <summary>
        /// Authorizes a request based on the provided authorization details or user principal.
        /// </summary>
        /// <remarks>The method processes the provided <see cref="AuthorizeRequest"/> to determine whether
        /// the request is authorized. Ensure that the <paramref name="request"/> contains all required fields before
        /// calling this method.</remarks>
        /// <param name="request">The authorization request containing the necessary details to perform the authorization.  This parameter
        /// cannot be <see langword="null"/>.</param>
        public Task<AuthorizeResult> Authorize(AuthorizeRequest request, ClaimsPrincipal principal,  CancellationToken cancellationToken);

        /// <summary>
        /// Handles an upstream callback by processing the provided input and returning the result.
        /// </summary>
        /// <param name="input">The input data required to process the upstream callback.</param>
        /// <param name="ct">A cancellation token that can be used to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains an  <see
        /// cref="UpstreamCallbackResult"/> object representing the outcome of the callback processing.</returns>
        public Task<UpstreamCallbackResult> HandleUpstreamCallback(UpstreamCallbackInput input, CancellationToken ct);

        /// <summary>
        /// Handles refresh of an OIDC session based on the provided principal.Used when the Altinn Studio runtimecookie
        /// is used as session cookie for OIDC and app tyical refresh endpoint runtime
        /// </summary>
        /// <param name="principal">The principal containing all claims including the SID</param>
        /// <param name="ct"></param>
        /// <returns></returns>
        public Task<OidcSession> HandleSessionRefresh(ClaimsPrincipal principal, CancellationToken ct);

        /// <summary>
        /// Handles ending an OIDC session based on the provided input.
        /// </summary>
        /// <param name="input">The end session put</param>
        /// <param name="ct">The cancellationtoken</param>
        /// <returns></returns>
        public Task<EndSessionResult> EndSessionAsync(EndSessionInput input, CancellationToken ct);
    }
}
