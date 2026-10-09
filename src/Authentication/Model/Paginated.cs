#nullable enable
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.OpenApi;
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
    [ExcludeFromCodeCoverage]
    private sealed class SchemaFilter : ISchemaFilter
    {        
        public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
        {
            if (schema is OpenApiSchema openApiSchema)
            {
                openApiSchema.Required.Add("next");
                if (openApiSchema.Properties.TryGetValue("next", out var nextSchema) && nextSchema is OpenApiSchema nextOpenApiSchema)
                {
                    nextOpenApiSchema.Format = "uri-reference";
                    nextOpenApiSchema.Example = "/foo/bar/bat?page=2";
                    nextOpenApiSchema.Type |= JsonSchemaType.Null;
                }
            }
        }
    }
}
