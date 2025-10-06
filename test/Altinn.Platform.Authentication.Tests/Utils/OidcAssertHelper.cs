using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Policy;
using Altinn.Platform.Authentication.Core.Models.Oidc;
using Altinn.Platform.Authentication.Tests.Helpers;
using Altinn.Platform.Authentication.Tests.Models;
using Docker.DotNet.Models;
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

        public static void AssertCallbackResponse(HttpResponseMessage callbackResp, OidcTestScenario testScenario, DateTimeOffset now)
        {
            Assert.Equal(HttpStatusCode.Found, callbackResp.StatusCode);
            Assert.NotNull(callbackResp.Headers.Location);
            var finalLocation = callbackResp.Headers.Location!;
            Assert.Equal("https", finalLocation.Scheme);
            Assert.Equal("af.altinn.no", finalLocation.Host);
            Assert.Equal("/api/cb", finalLocation.AbsolutePath);

            System.Collections.Specialized.NameValueCollection finalQuery = System.Web.HttpUtility.ParseQueryString(finalLocation.Query);
            Assert.False(string.IsNullOrWhiteSpace(finalQuery["code"]), "Downstream code must be present.");
            Assert.Equal(testScenario.DownstreamState, finalQuery["state"]); // original downstream state echoed back

            AssertHasAltinnStudioRuntimeCookie(callbackResp, out var runtimeCookieValue, testScenario, now);
        }

        public static void AssertLogingTransaction(LoginTransaction loginTransaction, OidcTestScenario scenario)
        {
            Assert.NotNull(loginTransaction);
            Assert.Equal(scenario.DownstreamClientId, loginTransaction.ClientId);
            Assert.Equal(scenario.DownstreamNonce, loginTransaction.Nonce);
            Assert.Equal(scenario.DownstreamState, loginTransaction.State);
            Assert.Equal(scenario.DownstreamClientCallbackUrl, loginTransaction.RedirectUri.ToString());
            Assert.Equal(string.Join(" ", scenario.Scopes), string.Join(" ", loginTransaction.Scopes));
            Assert.Equal("S256", loginTransaction.CodeChallengeMethod);
            Assert.Equal(scenario.DownstreamCodeChallenge, loginTransaction.CodeChallenge);
            Assert.Equal("pending", loginTransaction.Status);
            Assert.Equal(scenario.DownstreamCodeChallenge, loginTransaction.CodeChallenge);
        }

        public static void AssertUpstreamLogingTransaction(UpstreamLoginTransaction createdUpstreamLogingTransaction, OidcTestScenario testScenario)
        {
            Assert.NotNull(createdUpstreamLogingTransaction);
        }

        public static void AssertHasAltinnStudioRuntimeCookie(HttpResponseMessage resp, out string value, OidcTestScenario testScenario,   DateTimeOffset now)
        {
            Assert.True(resp.Headers.TryGetValues("Set-Cookie", out var setCookies), "Response missing Set-Cookie headers.");

            string? raw = setCookies.FirstOrDefault(h =>
                h.StartsWith("AltinnStudioRuntime=", StringComparison.OrdinalIgnoreCase));
            Assert.False(string.IsNullOrEmpty(raw), "AltinnStudioRuntime cookie was not set.");

            // Split into name/value + attributes
            var parts = raw!.Split(';').Select(p => p.Trim()).ToArray();

            // name=value
            var kv = parts[0].Split('=', 2);
            Assert.Equal("AltinnStudioRuntime", kv[0]);
            value = kv.Length > 1 ? kv[1] : string.Empty;
            Assert.False(string.IsNullOrEmpty(value), "AltinnStudioRuntime cookie has empty value.");

            // ❌ Must NOT set Expires or Domain (host-only, session cookie)
            Assert.DoesNotContain(parts, p => p.StartsWith("Expires=", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(parts, p => p.StartsWith("Domain=", StringComparison.OrdinalIgnoreCase));

            // (Optional but recommended) also forbid Max-Age to ensure session-only
            // Assert.DoesNotContain(parts, p => p.StartsWith("Max-Age=", StringComparison.OrdinalIgnoreCase));

            // Assert token from cookie is valid and contains the expected claims
            TokenAssertsHelper.AssertCookieAccessToken(value, testScenario, now);
        }
    }
}
