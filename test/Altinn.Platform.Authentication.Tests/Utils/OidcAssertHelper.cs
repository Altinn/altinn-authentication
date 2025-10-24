using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using Altinn.Platform.Authentication.Core.Models.Oidc;
using Altinn.Platform.Authentication.Tests.Helpers;
using Altinn.Platform.Authentication.Tests.Models;
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
            Assert.Equal(testScenario.GetDownstreamState(), finalQuery["state"]); // original downstream state echoed back

            AssertHasAltinnStudioRuntimeCookie(callbackResp, out var runtimeCookieValue, testScenario, now);
            AssertHasAltinnSessionCookie(callbackResp, out var sessionCookieValue, testScenario, now);
        }

        public static void AssertLoginTransaction(LoginTransaction loginTransaction, OidcTestScenario scenario, DateTimeOffset now)
        {
            Assert.NotNull(loginTransaction);
            Assert.Equal(now, loginTransaction.CreatedAt);
            Assert.Equal(scenario.DownstreamClientId, loginTransaction.ClientId);
            Assert.Equal(scenario.GetDownstreamNonce(), loginTransaction.Nonce);
            Assert.Equal(scenario.GetDownstreamState(), loginTransaction.State);
            Assert.Equal(scenario.DownstreamClientCallbackUrl, loginTransaction.RedirectUri.ToString());
            Assert.Equal(string.Join(" ", scenario.Scopes), string.Join(" ", loginTransaction.Scopes));
            Assert.Equal("S256", loginTransaction.CodeChallengeMethod);
            Assert.Equal(scenario.GetDownstreamCodeChallenge(), loginTransaction.CodeChallenge);
            Assert.Equal("pending", loginTransaction.Status);
        }

        public static void AssertUpstreamLoginTransaction(UpstreamLoginTransaction createdTx, OidcTestScenario testScenario, DateTimeOffset now)
        {
            Assert.NotNull(createdTx);
            Assert.NotNull(createdTx.Status);
            Assert.Equal("pending", createdTx.Status);
            Assert.NotNull(createdTx.Nonce);
            Assert.Equal(now, createdTx.CreatedAt);
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

            // Assert that domain is set to localhost (test env) (will be altinn.no for production)
            Assert.Contains(parts, p => p.StartsWith("domain=localhost", StringComparison.OrdinalIgnoreCase));

            // ❌ Must NOT set Expires (session cookie)
            Assert.DoesNotContain(parts, p => p.StartsWith("Expires=", StringComparison.OrdinalIgnoreCase));

            // (Optional but recommended) also forbid Max-Age to ensure session-only
            // Assert.DoesNotContain(parts, p => p.StartsWith("Max-Age=", StringComparison.OrdinalIgnoreCase));

            // Assert token from cookie is valid and contains the expected claims
            TokenAssertsHelper.AssertCookieAccessToken(value, testScenario, now);
        }

        public static void AssertDeleteAltinnStudioRuntimeCookie(HttpResponseMessage resp, out string value, OidcTestScenario testScenario, DateTimeOffset now)
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
            Assert.True(string.IsNullOrEmpty(value), "AltinnStudioRuntime cookie did not have a empty value.");

            // Assert that domain is set to localhost (test env) (will be altinn.no for production)
            Assert.Contains(parts, p => p.StartsWith("domain=localhost", StringComparison.OrdinalIgnoreCase));

            // ❌ Must NOT set Expires
            Assert.Contains(parts, p => p.StartsWith("expires=Thu, 01 Jan 1970 00:00:00 GMT", StringComparison.OrdinalIgnoreCase));
        }

        public static void AssertHasAltinnSessionCookie(HttpResponseMessage resp, out string value, OidcTestScenario testScenario, DateTimeOffset now)
        {
            Assert.True(resp.Headers.TryGetValues("Set-Cookie", out var setCookies), "Response missing Set-Cookie headers.");

            string? raw = setCookies.FirstOrDefault(h =>
                h.StartsWith("altinnsession=", StringComparison.OrdinalIgnoreCase));
            Assert.False(string.IsNullOrEmpty(raw), "altinnsession cookie was not set.");

            // Split into name/value + attributes
            var parts = raw!.Split(';').Select(p => p.Trim()).ToArray();

            // name=value
            var kv = parts[0].Split('=', 2);
            Assert.Equal("altinnsession", kv[0]);
            value = kv.Length > 1 ? kv[1] : string.Empty;
            Assert.False(string.IsNullOrEmpty(value), "AltinnStudioRuntime cookie has empty value.");

            // ❌ Must NOT set Expires or Domain (host-only, session cookie)
            Assert.DoesNotContain(parts, p => p.StartsWith("expires=", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(parts, p => p.StartsWith("domain=", StringComparison.OrdinalIgnoreCase));
        }

        public static void AssertValidSession(OidcSession oidcSession, OidcTestScenario testScenario, DateTimeOffset now)
        {
            Assert.NotNull(oidcSession);
            Assert.False(string.IsNullOrEmpty(oidcSession.Sid));

            Assert.Equal(testScenario.Amr?.OrderBy(s => s), oidcSession.Amr?.OrderBy(s => s));
    
            foreach (string scope in testScenario.Scopes)
            { 
                Assert.Contains(scope, oidcSession.Scopes);
            }

            Assert.True(oidcSession.CreatedAt <= now);
            Assert.True(oidcSession.UpdatedAt <= now);
            Assert.Equal(now, oidcSession.LastSeenAt); // not updated yet
            Assert.True(oidcSession.ExpiresAt > now);
        }

        public static void AssertAuthorizedRedirect(HttpResponseMessage authorizedRedirectResponse, OidcTestScenario testScenario, DateTimeOffset dateTimeOffset)
        {
            AssertHasAltinnStudioRuntimeCookie(authorizedRedirectResponse, out string runtimeValue, testScenario, dateTimeOffset);
            AssertHasAltinnSessionCookie(authorizedRedirectResponse, out string value, testScenario, dateTimeOffset);
        }

        internal static void AssertLogoutRedirect(HttpResponseMessage logoutResp, OidcTestScenario testScenario)
        {
            AssertDeleteAltinnStudioRuntimeCookie(logoutResp, out string runtimeValue, testScenario, DateTimeOffset.UtcNow);
        }
    }
}
