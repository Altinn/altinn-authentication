using Altinn.Platform.Authentication.Core.Models.Profile;

namespace Altinn.Authentication.Core.Clients.Interfaces
{
    /// <summary>
    /// Client for register's permanent self-identified-user provisioning endpoint
    /// (<c>POST /register/api/v2/internal/users/self-identified</c>). Replaces the
    /// SBL Bridge <c>users/</c> + <c>users/create/</c> two-call flow with one
    /// atomic get-or-create call. Gated by the
    /// <c>RegisterSelfIdentifiedUserProvisioning</c> feature flag.
    /// </summary>
    public interface IRegisterUserProvisioningClient
    {
        /// <summary>
        /// Get an existing self-identified user, or create one if none exists.
        /// </summary>
        /// <param name="request">The provisioning request.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The provisioned user, or <see langword="null"/> on transport failure.</returns>
        Task<SelfIdentifiedUserProvisioningResponse?> GetOrCreateUser(
            SelfIdentifiedUserProvisioningRequest request,
            CancellationToken cancellationToken = default);
    }
}
