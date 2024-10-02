#nullable enable
using System.Collections.Generic;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.Annotations;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Altinn.Platform.Authentication.Model;

/// <summary>
/// A paginated <see cref="ListObject{T}"/>.
/// </summary>
public static class Paginated
{
    /// <summary>
    /// Create a new <see cref="Paginated{T}"/>.
    /// </summary>
    /// <typeparam name="T">The type of items</typeparam>
    /// <param name="items">The items</param>
    /// <param name="next">The optional next-link</param>
    /// <returns>A new <see cref="Paginated{T}"/>.</returns>
    public static Paginated<T> Create<T>(
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
public record Paginated<T>(
    PaginatedLinks Links,
    IEnumerable<T> Items)
    : ListObject<T>(Items);

/// <summary>
/// Pagination links.
/// </summary>
/// <param name="Next">Link to the next page of items (if any).</param>
[SwaggerSchemaFilter(typeof(SchemaFilter))]
public record PaginatedLinks(
    string? Next)
{
    private sealed class SchemaFilter : ISchemaFilter
    {
        /// <inheritdoc/>
        public void Apply(OpenApiSchema schema, SchemaFilterContext context)
        {
            schema.Required.Add("next");

            var nextSchema = schema.Properties["next"];
            nextSchema.Nullable = true;
            nextSchema.Format = "uri-reference";
            nextSchema.Example = new OpenApiString("/foo/bar/bat?page=2");
        }
    }
}
