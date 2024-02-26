using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Npgsql;

namespace Altinn.Platform.Authentication.Persistance.Extensions;

/// <summary>
/// Helper extensions for Npgsql.
/// </summary>
[ExcludeFromCodeCoverage]
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

    /// <summary>
    /// Helper method
    /// </summary>
    /// <param name="reader">Npqsqldatareader</param>
    /// <returns></returns>
    internal static ValueTask<bool> ConvertFromReaderToBoolean(NpgsqlDataReader reader)
    {
        return new ValueTask<bool>(reader.GetBoolean(0));
    }

    /// <summary>
    /// Helper method
    /// </summary>
    /// <param name="reader">NpgsqlDataReader</param>
    /// <returns></returns>
    internal static ValueTask<string> ConvertFromReaderToString(NpgsqlDataReader reader)
    {
        return new ValueTask<string>(reader.GetString(0));
    }

    /// <summary>
    /// Helper method
    /// </summary>
    /// <param name="reader">NpgsqlDataReader</param>
    /// <returns></returns>
    internal static ValueTask<Guid> ConvertFromReaderToGuid(NpgsqlDataReader reader)
    {
        return new ValueTask<Guid>(reader.GetGuid(0));
    }
}