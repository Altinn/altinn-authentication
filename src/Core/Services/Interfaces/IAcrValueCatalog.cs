namespace Altinn.Platform.Authentication.Core.Services.Interfaces
{
    /// <summary>
    /// Catalogue of the <c>acr</c> values Altinn exposes to its own clients, built from the
    /// configured ID-providers.
    /// <para>
    /// An acr value in this catalogue is <em>Altinn-facing</em>: it is what a client may ask for
    /// in <c>acr_values</c>, what is stored on the session, and what is emitted in the <c>acr</c>
    /// claim. It is deliberately not the same vocabulary as the upstream provider's — a provider
    /// that does not follow ID-porten's conventions (HelseID, for instance, which sends no
    /// <c>acr</c> at all) maps its own claim values onto these through configuration.
    /// </para>
    /// </summary>
    public interface IAcrValueCatalog
    {
        /// <summary>
        /// Every acr value a client is allowed to request. Derived from configuration, so adding a
        /// provider extends the allow-list without a code change.
        /// </summary>
        IReadOnlySet<string> AllowedAcrValues { get; }

        /// <summary>
        /// Resolves an Altinn-facing acr value to its normalised authentication level
        /// (0-4, matching <c>SecurityLevel</c>).
        /// </summary>
        /// <returns><c>true</c> when the acr value is known; otherwise <c>false</c>.</returns>
        bool TryGetLevel(string? acr, out int level);

        /// <summary>
        /// Returns the highest level among <paramref name="acrValues"/>, or <c>null</c> when none
        /// of them is known. Used to decide whether a session satisfies a requested level.
        /// </summary>
        int? GetRequestedLevel(IEnumerable<string>? acrValues);

        /// <summary>
        /// Resolves which configured provider should handle the requested acr values.
        /// Returns <c>null</c> when no provider claims any of them.
        /// </summary>
        string? ResolveProviderKey(IEnumerable<string>? acrValues);

        /// <summary>
        /// Translates Altinn-facing acr values into the <c>acr_values</c> string to send to
        /// <paramref name="providerKey"/>'s authorize endpoint, or <c>null</c> when the provider
        /// wants none.
        /// </summary>
        string? GetUpstreamAcrValues(string providerKey, IEnumerable<string>? acrValues);
    }
}
