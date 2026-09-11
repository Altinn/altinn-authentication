#nullable enable
namespace Altinn.Platform.Authentication.Services.Interfaces
{
    /// <summary>
    /// Remembers the most recent DPoP nonce each upstream provider has supplied, so the next token
    /// request to that provider can carry it up front.
    /// </summary>
    /// <remarks>
    /// RFC 9449 section 8.2: a nonce supplied on a successful response "MUST" be used "for the
    /// next token request and for all subsequent token requests until the authorization server
    /// supplies a new nonce". Without this, every request would start without a nonce and pay a
    /// challenge round-trip, and the client would be out of conformance besides.
    /// <para>
    /// Scoped per provider. Section 9 of the RFC: a nonce is accepted only by the server that
    /// issued it. Lives outside <see cref="IOidcProvider"/> because that is a typed HttpClient
    /// and therefore transient.
    /// </para>
    /// </remarks>
    public interface IDpopNonceStore
    {
        /// <summary>The nonce to send on the next request to <paramref name="providerKey"/>, or <c>null</c> when none has been supplied yet.</summary>
        string? Get(string providerKey);

        /// <summary>Records the nonce most recently supplied by <paramref name="providerKey"/>.</summary>
        void Set(string providerKey, string nonce);
    }
}
