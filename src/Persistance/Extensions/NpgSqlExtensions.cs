using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Npgsql;
using NpgsqlTypes;

namespace Altinn.Platform.Authentication.Persistance.Extensions;

/// <summary>
/// Helper extensions for Npgsql.
/// </summary>
[ExcludeFromCodeCoverage]
internal static class NpgSqlExtensions
{
    /// <summary>
    /// Adds a typed parameter to the collection.
    /// </summary>
    /// <typeparam name="T">The parameter type.</typeparam>
    /// <param name="collection">The parameter collection.</param>
    /// <param name="parameterName">The parameter name.</param>
    /// <returns>The newly created parameter.</returns>
    public static NpgsqlParameter<T> Add<T>(this NpgsqlParameterCollection collection, string parameterName)
    {
        var parameter = new NpgsqlParameter<T>()
        {
            ParameterName = parameterName,
        };

        collection.Add(parameter);
        return parameter;
    }

    /// <summary>
    /// Adds a typed parameter to the collection.
    /// </summary>
    /// <typeparam name="T">The parameter type.</typeparam>
    /// <param name="collection">The parameter collection.</param>
    /// <param name="parameterName">The parameter name.</param>
    /// <param name="dbType">The parameter <see cref="NpgsqlDbType"/>.</param>
    /// <returns>The newly created parameter.</returns>
    public static NpgsqlParameter<T> Add<T>(this NpgsqlParameterCollection collection, string parameterName, NpgsqlDbType dbType)
    {
        var parameter = new NpgsqlParameter<T>(parameterName, dbType);

        collection.Add(parameter);
        return parameter;
    }

    /// <summary>
    /// Executes a command against the database, returning a <see cref="IAsyncEnumerable{T}"/>
    /// that can be easily mapped over.
    /// </summary>
    /// <param name="cmd">The <see cref="NpgsqlCommand"/>.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/>.</param>
    /// <returns>IAsyncEnumerable of the Query result from the DB</returns>
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
    /// <param name="cancellationToken">The magnificent cancellation token</param>
    /// <returns>Query field converted to bool</returns>
    internal static ValueTask<bool> ConvertFromReaderToBoolean(NpgsqlDataReader reader, CancellationToken cancellationToken = default)
    {
        return new ValueTask<bool>(reader.GetFieldValueAsync<bool>(0, cancellationToken));
    }

    /// <summary>
    /// Helper method
    /// </summary>
    /// <param name="reader">NpgsqlDataReader</param>
    /// <param name="cancellationToken">The magnificent cancellation token</param>
    /// <returns>Query field converted to string</returns>
    internal static ValueTask<string> ConvertFromReaderToString(NpgsqlDataReader reader, CancellationToken cancellationToken = default)
    {
        return new ValueTask<string>(reader.GetFieldValueAsync<string>(0, cancellationToken));
    }

    /// <summary>
    /// Helper method
    /// </summary>
    /// <param name="reader">NpgsqlDataReader</param>
    /// <param name="cancellationToken">The magnificent cancellation token</param>
    /// <returns>Query field converted to Guid</returns>
    internal static ValueTask<Guid> ConvertFromReaderToGuid(NpgsqlDataReader reader, CancellationToken cancellationToken = default)
    {
        return new(reader.GetFieldValueAsync<Guid>(0, cancellationToken));
    }

    /// <summary>
    /// Helper method
    /// </summary>
    /// <param name="reader">NpgsqlDataReader</param>
    /// <param name="cancellationToken">The magnificent cancellation token</param>
    /// <returns>Query field converted to Int</returns>
    internal static ValueTask<int> ConvertFromReaderToInt(NpgsqlDataReader reader, CancellationToken cancellationToken = default)
    {
        return new ValueTask<int>(reader.GetFieldValueAsync<int>(0, cancellationToken));
    }
}