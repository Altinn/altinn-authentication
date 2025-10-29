using Altinn.Platform.Authentication.Core.Models.Oidc;

namespace Altinn.Platform.Authentication.Core.RepositoryInterfaces
{
    /// <summary>
    /// Repository abstraction for persisting and retrieving OIDC server configuration/state.
    /// Implementations should use parameterized SQL and avoid logging secrets.
    /// </summary>
    public interface IOidcServerClientRepository
    {
        /// <summary>
        /// Retrieves a registered OIDC client by its <c>client_id</c>.
        /// </summary>
        /// <param name="clientId">
        /// The unique client identifier as supplied by the RP (e.g., in <c>/authorize</c> or <c>/token</c>).
        /// </param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>
        /// The <see cref="OidcClient"/> if found (and enabled, if implementation filters on that), otherwise <c>null</c>.
        /// </returns>
        /// <exception cref="System.ArgumentException">
        /// Thrown if <paramref name="clientId"/> is null or whitespace.
        /// </exception>
        /// <remarks>
        /// Typical usage: validation of <c>/authorize</c> requests—checking exact <c>redirect_uri</c> matches,
        /// allowed scopes, PKCE policy, and client authentication method for <c>/token</c>.
        /// Implementations should ensure case handling matches storage semantics (usually case-sensitive).
        /// </remarks>
        Task<OidcClient?> GetClientAsync(string clientId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Inserts a new OIDC client configuration row and returns the stored object as read back from the database.
        /// </summary>
        /// <param name="create">
        /// The input model containing normalized values (absolute <c>redirect_uris</c>, lowercased/unique <c>allowed_scopes</c>,
        /// and a <b>hash</b> of the client secret if used—never plaintext).
        /// </param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>
        /// The persisted <see cref="OidcClient"/> as mapped from the <c>RETURNING</c> clause.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">
        /// Thrown if <paramref name="create"/> is null.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// Thrown if required fields are missing or invalid (e.g., no redirect URIs, no allowed scopes).
        /// </exception>
        /// <exception cref="Npgsql.PostgresException">
        /// May be thrown on database errors. In particular, <c>SqlState=23505</c> indicates a unique-violation
        /// (e.g., <c>client_id</c> already exists) if the table enforces such a constraint.
        /// </exception>
        /// <remarks>
        /// Implementations should:
        /// <list type="bullet">
        ///   <item><description>Use parameterized SQL and correct <c>NpgsqlDbType</c> for arrays/JSON.</description></item>
        ///   <item><description>Set <c>created_at</c> from a time provider and keep <c>updated_at</c> null on insert.</description></item>
        ///   <item><description>Return the inserted row using <c>RETURNING</c> so the caller gets the canonical stored values.</description></item>
        /// </list>
        /// </remarks>
        Task<OidcClient> InsertClientAsync(OidcClientCreate create, CancellationToken cancellationToken = default);
    }
}
