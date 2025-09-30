using Altinn.Platform.Authentication.Core.Models.Oidc;
using Altinn.Platform.Authentication.Tests.Models;
using System;
using System.Net;
using System.Net.Http;
using Xunit;

namespace Altinn.Platform.Authentication.Tests.Utils
{
    public static class OidcAssertHelper
    {
        public static void AssertAuthorizeResponse(HttpResponseMessage authorizationRequestResponse)
        {
            // Assert: HTTP redirect to upstream authorize
            Assert.Equal(HttpStatusCode.Found, authorizationRequestResponse.StatusCode);
            Assert.NotNull(authorizationRequestResponse.Headers.Location);

            // We don't assert the exact upstream URL (it includes a generated state/nonce),
            // but we require it's absolute and points to an /authorize endpoint.
            Uri loc = authorizationRequestResponse.Headers.Location!;
            Assert.True(loc.IsAbsoluteUri, "Redirect Location must be absolute.");
            Assert.Contains("/authorize", loc.AbsolutePath, StringComparison.OrdinalIgnoreCase);
            Assert.True(authorizationRequestResponse.Headers.CacheControl?.NoStore ?? false, "Cache-Control must include no-store");
            Assert.Contains("no-cache", authorizationRequestResponse.Headers.Pragma.ToString(), StringComparison.OrdinalIgnoreCase);

            // Parse upstream Location query to ensure key params are present
            System.Collections.Specialized.NameValueCollection upstreamQuery = System.Web.HttpUtility.ParseQueryString(loc.Query);
            Assert.False(string.IsNullOrEmpty(upstreamQuery["state"]));
        }

        public static void AssertLogingTransaction(LoginTransaction loginTransaction, OidcTestScenario scenario)
        {
            Assert.NotNull(loginTransaction);
            Assert.Equal(scenario.DownstreamClientId, loginTransaction.ClientId);
            Assert.Equal(scenario.DownstreamNonce, loginTransaction.Nonce);
            Assert.Equal(scenario.DownstreamState, loginTransaction.State);
            Assert.Equal(scenario.RedirectUri, loginTransaction.RedirectUri.ToString());
            Assert.Equal("openid altinn:portal/enduser", string.Join(" ", loginTransaction.Scopes));
            Assert.Equal("S256", loginTransaction.CodeChallengeMethod);
            Assert.Equal(scenario.DownstreamCodeChallenge, loginTransaction.CodeChallenge);
        }

        public static void AssertUpstreamLogingTransaction(UpstreamLoginTransaction createdUpstreamLogingTransaction, OidcTestScenario testScenario)
        {
            Assert.NotNull(createdUpstreamLogingTransaction);
        }
    }
}
