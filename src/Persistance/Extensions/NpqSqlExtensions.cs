using System.Runtime.CompilerServices;
using Npgsql;

namespace Altinn.Platform.Authentication.Persistance.Extensions;

/// <summary>
/// Helper extensions for Npgsql.
/// </summary>
internal static class NpqSqlExtensions
{
    /// <summary>
    /// Executes a command against the database, returning a <see cref="IAsyncEnumerable{T}"/>
    /// that can be easily mapped over.
    /// </summary>
    /// <param name="cmd">The <see cref="NpgsqlCommand"/>.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/>.</param>
    /// <returns></returns>
    public static async IAsyncEnumerable<NpgsqlDataReader> ExecuteEnumerableAsync(
        this NpgsqlCommand cmd,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            yield return reader;
        }
    }
}