#nullable enable
using System;
using System.Collections.Concurrent;
using Altinn.Platform.Authentication.Services.Interfaces;

namespace Altinn.Platform.Authentication.Services
{
    /// <summary>
    /// In-memory, per-provider store of the most recent DPoP nonce. Registered as a singleton.
    /// </summary>
    /// <remarks>
    /// Per instance is enough: a nonce is a liveness hint, not a credential. If another instance
    /// has a newer one, this instance simply pays one challenge round-trip and catches up.
    /// </remarks>
    public sealed class DpopNonceStore : IDpopNonceStore
    {
        private readonly ConcurrentDictionary<string, string> _nonces = new(StringComparer.Ordinal);

        /// <inheritdoc />
        public string? Get(string providerKey)
            => _nonces.TryGetValue(providerKey, out string? nonce) ? nonce : null;

        /// <inheritdoc />
        public void Set(string providerKey, string nonce)
            => _nonces[providerKey] = nonce;
    }
}
