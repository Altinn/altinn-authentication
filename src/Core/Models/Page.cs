#nullable enable

using System.ComponentModel;

namespace Altinn.Platform.Authentication.Core.Models;

/// <summary>
/// Abstract base class for a page of items.
/// </summary>
public abstract class Page()
{
    /// <summary>
    /// Creates a request for a page of items based on an optional continuation token.
    /// </summary>
    /// <typeparam name="TToken">The continuation token type</typeparam>
    /// <param name="token">The optional continuation token</param>
    /// <returns>A <see cref="Page{TToken}.Request"/></returns>
    public static Page<TToken>.Request ContinueFrom<TToken>(TToken? token) => new(token);

    /// <summary>
    /// Creates a request for the first page of items.
    /// </summary>
    /// <returns>A <see cref="DefaultRequestSentinel"/> that implicitly converts to a <see cref="Page{TToken}.Request"/>.</returns>
    public static DefaultRequestSentinel DefaultRequest() => default;

    /// <summary>
    /// Creates a page of items.
    /// </summary>
    /// <typeparam name="TItem">The item type</typeparam>
    /// <typeparam name="TToken">The continuation token type</typeparam>
    /// <param name="allItems">The full set of items (should be limited to <c><paramref name="pageSize"/> + 1</c>)</param>
    /// <param name="pageSize">The page size</param>
    /// <param name="tokenFactory">A function to create a continuation token from a <typeparamref name="TItem"/></param>
    /// <returns>A <see cref="Page{TItem, TToken}"/> of items, and an optional continuation token</returns>
    public static Page<TItem, TToken> Create<TItem, TToken>(
        IReadOnlyList<TItem> allItems,
        int pageSize,
        Func<TItem, TToken> tokenFactory)
    {
        if (allItems.Count <= pageSize)
        {
            return new(allItems, default);
        }

        var items = allItems.Take(pageSize).ToList();
        var nextToken = tokenFactory(allItems[pageSize]);
        return new(items, nextToken);
    }

    /// <summary>
    /// A sentinel type to indicate that the default request should be used.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public readonly struct DefaultRequestSentinel
    {
    }
}

/// <summary>
/// Abstract base class for a page of items where
/// the next page can be requested using a <typeparamref name="TToken"/>.
/// </summary>
/// <typeparam name="TToken">The token type used to request the next page</typeparam>
public abstract class Page<TToken>(
    Optional<TToken> continuationToken)
    : Page()
{
    /// <summary>
    /// Gets the continuation token, if any.
    /// </summary>
    public Optional<TToken> ContinuationToken => continuationToken;

    /// <summary>
    /// A request for the next page of items.
    /// </summary>
    public sealed class Request(TToken? continuationToken)
    {
        /// <summary>
        /// Gets a continuation token from a previous page, or <see langword="null"/> if requesting the first page.
        /// </summary>
        public TToken? ContinuationToken => continuationToken;

        public static implicit operator Request(DefaultRequestSentinel _) => new(default);
    }
}

/// <summary>
/// A page of items and an optional continuation token.
/// </summary>
/// <typeparam name="TItem">The item type</typeparam>
/// <typeparam name="TToken">The token type used to request the next page</typeparam>
public class Page<TItem, TToken>(
    IReadOnlyList<TItem> items,
    Optional<TToken> continuationToken)
    : Page<TToken>(continuationToken)
{
    /// <summary>
    /// Gets the list of items.
    /// </summary>
    public IReadOnlyList<TItem> Items => items;
}
