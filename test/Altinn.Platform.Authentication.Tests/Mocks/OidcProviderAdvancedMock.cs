#nullable enable
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Model;
using Altinn.Platform.Authentication.Services.Interfaces;

namespace Altinn.Platform.Authentication.Tests.Mocks
{
    /// <summary>
    /// In-memory test double for <see cref="IOidcProvider"/>.
    /// Allows tests to configure responses keyed by (authorizationCode, clientId, redirectUri, codeVerifier).
    /// Any key component can be set to null to act as a wildcard.
    /// </summary>
    public sealed class OidcProviderAdvancedMock : IOidcProvider
    {
        private readonly ConcurrentBag<Rule> _rules = new();

        /// <summary>
        /// Configure a successful token response.
        /// Any parameter set to <c>null</c> is treated as a wildcard (matches anything).
        /// </summary>
        public void SetupSuccess(
            string? authorizationCode,
            string? clientId,
            string? redirectUri,
            string? codeVerifier,
            OidcCodeResponse response)
        {
            _rules.Add(new Rule(
                Code: authorizationCode,
                ClientId: clientId,
                RedirectUri: redirectUri,
                CodeVerifier: codeVerifier,
                Handler: (_, _, _, _, _) => Task.FromResult(response),
                Name: "success"));
        }

        /// <summary>
        /// Configure a failure that returns null (or throw if you prefer).
        /// Any parameter set to <c>null</c> is treated as a wildcard (matches anything).
        /// </summary>
        public void SetupFailure(
            string? authorizationCode,
            string? clientId,
            string? redirectUri,
            string? codeVerifier,
            Func<string, OidcProvider, string, string?, CancellationToken, Task<OidcCodeResponse?>>? handler = null)
        {
            _rules.Add(new Rule(
                Code: authorizationCode,
                ClientId: clientId,
                RedirectUri: redirectUri,
                CodeVerifier: codeVerifier,
                Handler: handler ?? ((_, _, _, _, _) => Task.FromResult<OidcCodeResponse?>(null)),
                Name: "failure"));
        }

        /// <summary>
        /// The method under test: returns a configured response or throws if no matching rule exists.
        /// </summary>
        public async Task<OidcCodeResponse?> GetTokens(
            string authorizationCode,
            OidcProvider provider,
            string redirect_uri,
            string? codeVerifier,
            CancellationToken cancellationToken = default)
        {
            // Find the first rule whose non-null fields match the inputs.
            var match = _rules.FirstOrDefault(r =>
                (r.Code is null || string.Equals(r.Code, authorizationCode, StringComparison.Ordinal)) &&
                (r.ClientId is null || string.Equals(r.ClientId, provider?.ClientId, StringComparison.Ordinal)) &&
                (r.RedirectUri is null || string.Equals(r.RedirectUri, redirect_uri, StringComparison.Ordinal)) &&
                (r.CodeVerifier is null || string.Equals(r.CodeVerifier, codeVerifier, StringComparison.Ordinal)));

            if (match is null)
            {
                throw new InvalidOperationException(
                    $"FakeOidcProvider: no rule matched (code='{authorizationCode}', clientId='{provider?.ClientId}', redirect_uri='{redirect_uri}', verifier='{codeVerifier ?? "<null>"}').");
            }

            return await match.Handler(authorizationCode, provider, redirect_uri, codeVerifier, cancellationToken);
        }

        private sealed record Rule(
            string? Code,
            string? ClientId,
            string? RedirectUri,
            string? CodeVerifier,
            Func<string, OidcProvider, string, string?, CancellationToken, Task<OidcCodeResponse?>> Handler,
            string Name);
    }
}
