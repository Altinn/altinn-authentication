namespace Altinn.Platform.Authentication.Core.Models.ResourceRegistry
{
    /// <summary>
    /// Model for defining keywords.
    /// </summary>
    /// <remarks>
    /// Nothing in this service reads a keyword, and the only observed payload carrying the
    /// property had an empty array, so neither member can be claimed non-null.
    /// </remarks>
    public class Keyword
    {
        /// <summary>
        /// The key word
        /// </summary>
        public string? Word { get; set; }

        /// <summary>
        /// Language of the key word
        /// </summary>
        public string? Language { get; set; }
    }
}
