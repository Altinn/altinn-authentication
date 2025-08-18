#nullable enable

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Altinn.Platform.Authentication.Core.Models.Pagination;

/// <summary>
/// A list object is a wrapper around a list of items to allow for the API to be
/// extended in the future without breaking backwards compatibility.
/// </summary>
public abstract record ListObject
{
    /// <summary>
    /// Creates a new <see cref="ListObject{T}"/> from a list of items.
    /// </summary>
    /// <typeparam name="T">The list type.</typeparam>
    /// <param name="items">The list of items.</param>
    /// <returns>A <see cref="ListObject{T}"/>.</returns>
    public static ListObject<T> Create<T>(IEnumerable<T> items)
        => new(items);

}

/// <summary>
/// A concrete list object.
/// </summary>
/// <typeparam name="T">The item type.</typeparam>
/// <param name="Items">The items.</param>
public record ListObject<T>(
    [property: JsonPropertyName("data")]
    IEnumerable<T> Items)
    : ListObject;
