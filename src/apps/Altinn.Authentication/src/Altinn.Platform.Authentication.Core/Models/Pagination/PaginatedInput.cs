#nullable enable
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Altinn.Platform.Authentication.Core.Models.Pagination;

[ExcludeFromCodeCoverage]
/// <summary>
/// A paginated <see cref="ListObject{T}"/>.
/// </summary>
public static class PaginatedInput
{
    /// <summary>
    /// Create a new <see cref="PaginatedInput{T}"/>.
    /// </summary>
    /// <typeparam name="T">The type of items</typeparam>
    /// <param name="items">The items</param>
    /// <param name="next">The optional next-link</param>
    /// <returns>A new <see cref="PaginatedInput{T}"/>.</returns>
    public static PaginatedInput<T> Create<T>(
        IEnumerable<T> items,
        string? next)
        => new(new(next), items);
}

/// <summary>
/// A paginated <see cref="ListObject{T}"/>.
/// </summary>
/// <typeparam name="T">The item type.</typeparam>
/// <param name="Links">Pagination links.</param>
/// <param name="Items">The items.</param>
public record PaginatedInput<T>(
    PaginatedLinks Links,
    IEnumerable<T> Items)
    : ListObject<T>(Items);

/// <summary>
/// Pagination links.
/// </summary>
/// <param name="Next">Link to the next page of items (if any).</param>
public record PaginatedLinks(
    string? Next)
{
}
