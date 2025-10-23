using System.Globalization;

namespace Altinn.Platform.Authentication.Core.Helpers
{
    public static class HostMatch
    {
        /// <summary>
        /// Returns true if target's host is equal to, or a subdomain of,
        /// any host in allowedHosts.
        /// </summary>
        public static bool IsOnOrSubdomainOfAny(Uri target, IEnumerable<string> allowedHosts)
        {
            if (target is null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (allowedHosts is null)
            {
                throw new ArgumentNullException(nameof(allowedHosts));
            }

            if (!target.IsAbsoluteUri) throw new ArgumentException("Target must be an absolute URI.", nameof(target));

            string targetHost = NormalizeHost(target.Host);
            if (string.IsNullOrEmpty(targetHost)) return false;

            foreach (string? h in allowedHosts.Where(s => !string.IsNullOrWhiteSpace(s)))
            {
                string baseHost = NormalizeHost(h);
                if (string.IsNullOrEmpty(baseHost)) continue;

                // Exact hostname match
                if (string.Equals(targetHost, baseHost, StringComparison.Ordinal)) return true;

                // Subdomain match (requires dot boundary)
                if (targetHost.Length > baseHost.Length &&
                    targetHost.EndsWith("." + baseHost, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Convenience overload taking a target URI string and hostname allow-list.
        /// </summary>
        public static bool IsOnOrSubdomainOfAny(string targetUri, IEnumerable<string> allowedHosts)
        {
            if (targetUri is null)
            {
                throw new ArgumentNullException(nameof(targetUri));
            }

            if (allowedHosts is null) throw new ArgumentNullException(nameof(allowedHosts));

            var target = new Uri(targetUri, UriKind.Absolute);
            return IsOnOrSubdomainOfAny(target, allowedHosts);
        }

        /// <summary>
        /// Convenience overload taking a raw target host (e.g., from Request.Host.Host) and hostname allow-list.
        /// </summary>
        public static bool IsOnOrSubdomainOfAnyHost(string targetHost, IEnumerable<string> allowedHosts)
        {
            if (targetHost is null)
            {
                throw new ArgumentNullException(nameof(targetHost));
            }

            if (allowedHosts is null) throw new ArgumentNullException(nameof(allowedHosts));

            string normalizedTarget = NormalizeHost(targetHost);
            if (string.IsNullOrEmpty(normalizedTarget)) return false;

            foreach (var h in allowedHosts.Where(s => !string.IsNullOrWhiteSpace(s)))
            {
                string baseHost = NormalizeHost(h);
                if (string.IsNullOrEmpty(baseHost)) continue;

                if (string.Equals(normalizedTarget, baseHost, StringComparison.Ordinal)) return true;

                if (normalizedTarget.Length > baseHost.Length &&
                    normalizedTarget.EndsWith("." + baseHost, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        // --- helpers ---

        private static string NormalizeHost(string host)
        {
            if (string.IsNullOrWhiteSpace(host)) return string.Empty;

            // Trim whitespace and trailing dot
            host = host.Trim().TrimEnd('.');

            // Normalize IDNs to ASCII (punycode) and lower-invariant
            IdnMapping idn = new();
            string[] labels = host.Split('.', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < labels.Length; i++)
            {
                try { labels[i] = idn.GetAscii(labels[i]); }
                catch { /* keep original label if invalid */ }
            }

            return string.Join(".", labels).ToLowerInvariant();
        }
    }
}
