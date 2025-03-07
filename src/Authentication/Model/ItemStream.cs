#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.Annotations;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Altinn.Platform.Authentication.Model;

/// <summary>
/// A paginated <see cref="ListObject{T}"/>.
/// </summary>
public static class ItemStream
{
    /// <summary>
    /// Create a new <see cref="ItemStream{T}"/>.
    /// </summary>
    /// <typeparam name="T">The type of items</typeparam>
    /// <param name="items">The items.</param>
    /// <param name="next">The optional next-link.</param>
    /// <param name="stats">The item stream statistics.</param>
    /// <returns>A new <see cref="ItemStream{T}"/>.</returns>
    public static ItemStream<T> Create<T>(
        IEnumerable<T> items,
        string? next,
        ItemStreamStats stats)
        => new(new(next), stats, items);

    /// <summary>
    /// Create a new <see cref="ItemStream{T}"/>.
    /// </summary>
    /// <typeparam name="T">The type of items.</typeparam>
    /// <param name="items">The items.</param>
    /// <param name="next">The optional next-link.</param>
    /// <param name="sequenceMax">The highest sequence number in the database.</param>
    /// <param name="sequenceNumberFactory">A function to get the sequence number from a item.</param>
    /// <returns>A new <see cref="ItemStream{T}"/>.</returns>
    public static ItemStream<T> Create<T>(
        IEnumerable<T> items,
        string? next,
        long sequenceMax,
        Func<T, long> sequenceNumberFactory)
    {
        if (items is not IReadOnlyList<T> list)
        {
            list = items.ToList();
        }

        var stats = list.Count == 0
            ? new ItemStreamStats(sequenceMax, sequenceMax, sequenceMax)
            : new ItemStreamStats(
                sequenceNumberFactory(list[0]),
                sequenceNumberFactory(list[^1]),
                sequenceMax);

        return Create(list, next, stats);
    }
}

/// <summary>
/// A stream of all <typeparamref name="T"/> items in a data source.
/// </summary>
/// <typeparam name="T">The item type.</typeparam>
/// <param name="Links">Pagination links.</param>
/// <param name="Stats">Stream statistics.</param>
/// <param name="Items">The items.</param>
public record ItemStream<T>(
    PaginatedLinks Links,
    ItemStreamStats Stats,
    IEnumerable<T> Items)
    : Paginated<T>(Links, Items);

/// <summary>
/// Item stream statistics.
/// </summary>
/// <param name="PageStart">The first item on the page.</param>
/// <param name="PageEnd">The last item on the page.</param>
/// <param name="SequenceMax">The highest item in the database.</param>
[SwaggerSchemaFilter(typeof(SchemaFilter))]
public record ItemStreamStats(
    long PageStart,
    long PageEnd,
    long SequenceMax)
{
    private sealed class SchemaFilter : ISchemaFilter
    {
        /// <inheritdoc/>
        public void Apply(OpenApiSchema schema, SchemaFilterContext context)
        {
            schema.Required.Add("pageStart");
            schema.Required.Add("pageEnd");
            schema.Required.Add("sequenceMax");
        }
    }
}
