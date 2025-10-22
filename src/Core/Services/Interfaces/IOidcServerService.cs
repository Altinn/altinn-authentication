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
        public Task<AuthorizeResult> Authorize(AuthorizeRequest request, ClaimsPrincipal principal, string? sessionHandle, CancellationToken cancellationToken);

        /// <summary>
        /// This is used for unregistered_client flows where no client_id is sent in the authorize request and the result will only be a JWT token inside a cookie.
        /// Used by Altinn Apps and other application in Altinn Platform that do not have a client_id.
        /// Result is needed information to redirect to upstream with a valid 
        /// </summary>
        public Task<AuthorizeResult> AuthorizeUnregisteredClient(AuthorizeUnregisteredClientRequest request, CancellationToken cancellationToken);

        /// <summary>
        /// Handles an upstream callback by processing the provided input and returning the result.
        /// </summary>
        /// <param name="input">The input data required to process the upstream callback.</param>
        /// <param name="ct">A cancellation token that can be used to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains an  <see
        /// cref="UpstreamCallbackResult"/> object representing the outcome of the callback processing.</returns>
        public Task<UpstreamCallbackResult> HandleUpstreamCallback(UpstreamCallbackInput input, string? existingSessionHandle, CancellationToken ct);

        /// <summary>
        /// Based on session from cookie, verify session is valid and return result with a new valid Jwt token/AltinnRuntime cookie.
        /// </summary>
        public Task<AuthenticateFromSessionResult> HandleAuthenticateFromSessionResult(AuthenticateFromSessionInput sessionInfo, CancellationToken ct);

        /// <summary>
        /// Based on Altinn 2 ticket, verify session is valid and return result with a new valid Jwt token/AltinnRuntime cookie.
        /// </summary>
        public Task<AuthenticateFromAltinn2TicketResult> HandleAuthenticateFromTicket(AuthenticateFromAltinn2TicketInput sessionInfo, CancellationToken ct);

        /// <summary>
        /// Handles refresh of an OIDC session based on the provided principal.Used when the Altinn Studio runtimecookie
        /// is used as session cookie for OIDC and app tyical refresh endpoint runtime
        /// </summary>
        /// <param name="principal">The principal containing all claims including the SID</param>
        /// <param name="ct"></param>
        /// <returns></returns>
        public Task<OidcSession?> HandleSessionRefresh(ClaimsPrincipal principal, CancellationToken ct);

        /// <summary>
        /// Handles ending an OIDC session based on the provided input.
        /// </summary>
        /// <param name="input">The end session put</param>
        /// <param name="ct">The cancellationtoken</param>
        /// <returns></returns>
        public Task<EndSessionResult> EndSessionAsync(EndSessionInput input, CancellationToken ct);

        /// <summary>
        /// Handles upstream front-channel logout requests.
        /// </summary>
        public Task<UpstreamFrontChannelLogoutResult> HandleUpstreamFrontChannelLogoutAsync(UpstreamFrontChannelLogoutInput upstreamFrontChannelLogoutInput, CancellationToken ct);
    }
}
