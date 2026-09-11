#nullable enable
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Model;
using Altinn.Platform.Authentication.Services.Interfaces;

namespace Altinn.Platform.Authentication.Tests.Mocks
{
    /// <summary>
    /// Oidc provider for mocking OIDC Providers
    /// </summary>
    public class OidcProviderServiceMock : IOidcProvider
    {
        /// <inheritdoc />
        public Task<PushedAuthorizationResponse?> PushAuthorizationRequest(OidcProvider provider, IDictionary<string, string> parameters, CancellationToken cancellationToken = default)
            => Task.FromResult<PushedAuthorizationResponse?>(new PushedAuthorizationResponse { RequestUri = "urn:ietf:params:oauth:request_uri:mock", ExpiresIn = 600 });

        /// <summary>
        /// Initializes a new instance of the <see cref="OidcProviderServiceMock"/> class.
        /// </summary>
        public OidcProviderServiceMock()
        {
        }

        /// <summary>
        /// Performs a AccessToken Request as described in https://datatracker.ietf.org/doc/html/rfc6749#section-4.1.3
        /// </summary>
        public Task<OidcCodeResponse?> GetTokens(string authorizationCode, OidcProvider provider, string redirect_uri, string? codeVerifier, CancellationToken cancellationToken = default)
        {
            OidcCodeResponse codeResponse = new()
            {
                AccessToken = authorizationCode,
                IdToken = authorizationCode
            };

            return Task.FromResult<OidcCodeResponse?>(codeResponse);
        }
    }
}
