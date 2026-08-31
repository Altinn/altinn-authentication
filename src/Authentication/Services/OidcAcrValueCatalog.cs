using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Core.Services.Interfaces;
using Altinn.Platform.Authentication.Enum;
using Altinn.Platform.Authentication.Model;
using Microsoft.Extensions.Options;

#nullable enable

namespace Altinn.Platform.Authentication.Services
{
    /// <summary>
    /// Builds the Altinn-facing acr catalogue from <c>OidcProviders</c> configuration.
    /// <para>
    /// Providers that declare <see cref="OidcProvider.AuthLevels"/> contribute their own levels.
    /// Providers that do not are assumed to speak ID-porten's acr vocabulary and get the built-in
    /// table below, so existing configuration keeps working untouched.
    /// </para>
    /// </summary>
    public sealed class OidcAcrValueCatalog : IAcrValueCatalog
    {
        /// <summary>
        /// The provider ID-porten's acr vocabulary belongs to. Mirrors
        /// <c>OidcServerService.DefaultProviderKey</c>.
        /// </summary>
        private const string DefaultProviderKey = "idporten";

        private readonly Dictionary<string, Entry> _byAcr;
        private readonly HashSet<string> _allowed;

        /// <summary>
        /// Initializes a new instance of the <see cref="OidcAcrValueCatalog"/> class.
        /// </summary>
        public OidcAcrValueCatalog(IOptions<OidcProviderSettings> providerSettings)
        {
            _byAcr = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);

            // Only providers that explicitly declare AuthLevels take part in routing. Falling back
            // to the ID-porten table here instead would make every provider that has not opted in
            // — 'altinn', 'maskinporten', 'uidp' — a candidate for ID-porten's acr values, and the
            // first one configured would capture them.
            foreach (KeyValuePair<string, OidcProvider> kvp in providerSettings.Value)
            {
                if (kvp.Value.AuthLevels is not { Count: > 0 } levels)
                {
                    continue;
                }

                foreach (OidcAuthLevel level in levels)
                {
                    if (string.IsNullOrWhiteSpace(level.Acr))
                    {
                        continue;
                    }

                    // Fail startup rather than resolving collisions by configuration order. A
                    // silently ignored duplicate would route an acr value to whichever provider
                    // happened to be configured first, redirecting existing clients to a
                    // different ID-provider with no error anywhere.
                    if (_byAcr.TryGetValue(level.Acr, out Entry? clash))
                    {
                        throw new ConfigurationErrorsException(
                            $"OidcProviders: acr value '{level.Acr}' is declared by both '{clash.ProviderKey}' and '{kvp.Key}'. Each acr value must belong to exactly one provider.");
                    }

                    _byAcr[level.Acr] = new Entry(kvp.Key, level);
                }
            }

            // ID-porten's vocabulary stays bound to the default provider, which is what the
            // previous hardcoded mapping did.
            foreach (OidcAuthLevel level in OidcAuthLevelDefaults.IdPorten)
            {
                if (_byAcr.TryGetValue(level.Acr, out Entry? existing))
                {
                    // Only the default provider may declare ID-porten's own acr values
                    // explicitly. Any other provider claiming them would take over traffic from
                    // clients that have requested these values for years.
                    if (!string.Equals(existing.ProviderKey, DefaultProviderKey, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new ConfigurationErrorsException(
                            $"OidcProviders: provider '{existing.ProviderKey}' declares the built-in ID-porten acr value '{level.Acr}'. Only '{DefaultProviderKey}' may declare it.");
                    }

                    continue;
                }

                _byAcr[level.Acr] = new Entry(DefaultProviderKey, level);
            }

            _allowed = new HashSet<string>(_byAcr.Keys, StringComparer.Ordinal);
        }

        /// <inheritdoc />
        public IReadOnlySet<string> AllowedAcrValues => _allowed;

        /// <inheritdoc />
        public bool TryGetLevel(string? acr, out int level)
        {
            level = 0;
            if (string.IsNullOrWhiteSpace(acr))
            {
                return false;
            }

            if (_byAcr.TryGetValue(acr, out Entry? entry))
            {
                level = (int)entry.Level.Level;
                return true;
            }

            return false;
        }

        /// <inheritdoc />
        public int? GetRequestedLevel(IEnumerable<string>? acrValues)
        {
            if (acrValues is null)
            {
                return null;
            }

            int? highest = null;
            foreach (string acr in acrValues)
            {
                if (TryGetLevel(acr, out int level) && (highest is null || level > highest))
                {
                    highest = level;
                }
            }

            return highest;
        }

        /// <inheritdoc />
        public string? ResolveProviderKey(IEnumerable<string>? acrValues)
        {
            if (acrValues is null)
            {
                return null;
            }

            // Highest requested level decides the provider, so that a request mixing
            // 'selfregistered-email' with a real level still routes to the provider that can
            // actually deliver the level.
            string? key = null;
            int best = -1;
            foreach (string acr in acrValues)
            {
                if (_byAcr.TryGetValue(acr, out Entry? entry) && (int)entry.Level.Level > best)
                {
                    best = (int)entry.Level.Level;
                    key = entry.ProviderKey;
                }
            }

            return key;
        }

        /// <inheritdoc />
        public string? GetUpstreamAcrValues(string providerKey, IEnumerable<string>? acrValues)
        {
            if (acrValues is null)
            {
                return null;
            }

            List<string> upstream = [];
            foreach (string acr in acrValues)
            {
                if (!_byAcr.TryGetValue(acr, out Entry? entry))
                {
                    continue;
                }

                // Only translate acr values that belong to the provider we are about to call.
                // Sending one provider's vocabulary to another is exactly the bug this replaces.
                if (!string.Equals(entry.ProviderKey, providerKey, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(entry.Level.UpstreamAcrValues))
                {
                    upstream.AddRange(entry.Level.UpstreamAcrValues!.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                }
            }

            return upstream.Count == 0 ? null : string.Join(' ', upstream.Distinct(StringComparer.Ordinal));
        }

        private sealed record Entry(string ProviderKey, OidcAuthLevel Level);
    }
}
