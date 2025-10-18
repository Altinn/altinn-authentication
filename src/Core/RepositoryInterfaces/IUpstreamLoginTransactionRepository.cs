using Altinn.Platform.Authentication.Core.Models.Oidc;

namespace Altinn.Platform.Authentication.Core.RepositoryInterfaces
{
    public interface IUpstreamLoginTransactionRepository
    {
        Task<UpstreamLoginTransaction> InsertAsync(UpstreamLoginTransactionCreate create, CancellationToken ct = default);

        /// <summary>Finds a pending/callback_received row by upstream <c>state</c> (used in callback).</summary>
        Task<UpstreamLoginTransaction?> GetForCallbackByStateAsync(string upstreamState, CancellationToken ct = default);

        /// <summary>Stores the upstream authorization code received at the callback and moves status to <c>callback_received</c>.</summary>
        Task<int> SetCallbackSuccessAsync(Guid upstreamRequestId, string authCode, DateTimeOffset receivedAt, CancellationToken ct = default);

        /// <summary>Stores the upstream error at callback and moves status to <c>error</c>.</summary>
        Task<int> SetCallbackErrorAsync(Guid upstreamRequestId, string error, string? errorDescription, DateTimeOffset receivedAt, CancellationToken ct = default);

        /// <summary>Stores token exchange results and moves status to <c>token_exchanged</c>.</summary>
        Task<int> SetTokenExchangedAsync(
            Guid upstreamRequestId,
            string issuer,
            string sub,
            string? acr,
            DateTimeOffset? authTime,
            string? idTokenJti,
            string? sessionSid,
            DateTimeOffset exchangedAt,
            CancellationToken ct = default);

        /// <summary>Marks the upstream transaction completed (or cancelled) and sets completed_at.</summary>
        Task<int> MarkCompletedAsync(Guid upstreamRequestId, bool success, DateTimeOffset completedAt, CancellationToken ct = default);

        /// <summary>
        /// Marks that the token from the upstream has been exchanged and the relevant claims have been stored in the local token service.
        /// </summary>
        Task MarkTokenExchangedAsync(
          Guid upstreamRequestId,
          string issuer,
          string sub,
          string? acr,
          DateTimeOffset? authTime,
          string? idTokenJti,
          string? upstreamSid,
          CancellationToken cancellationToken = default);
    }       
}
