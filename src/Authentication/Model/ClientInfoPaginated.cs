#nullable enable
using System.Collections.Generic;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.Annotations;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Altinn.Platform.Authentication.Model;

/// <summary>
/// A paginated <see cref="ListObject{T}"/>.
/// </summary>
public static class ClientInfoPaginated
{
    /// <summary>
    /// Create a new <see cref="ClientInfoPaginated{T}"/>.
    /// </summary>
    /// <typeparam name="T">The type of items</typeparam>
    /// <param name="items">The items</param>
    /// <param name="next">The optional next-link</param>
    /// <param name="systemUserInformation">The system user info</param>
    /// <returns>A new <see cref="ClientInfoPaginated{T}"/>.</returns>
    public static ClientInfoPaginated<T> Create<T>(
        IEnumerable<T> items,
        string? next,
        SystemUserInfo systemUserInformation)
        => new(new(next), items, systemUserInformation);
}

/// <summary>
/// A paginated <see cref="ListObject{T}"/>.
/// </summary>
/// <typeparam name="T">The item type.</typeparam>
/// <param name="Links">Pagination links.</param>
/// <param name="Items">The items.</param>
/// <param name="SystemUserInformation">The system user info</param>
public record ClientInfoPaginated<T>(
    PaginatedLinks Links,
    IEnumerable<T> Items,
    SystemUserInfo SystemUserInformation)
    : ListObject<T>(Items);
