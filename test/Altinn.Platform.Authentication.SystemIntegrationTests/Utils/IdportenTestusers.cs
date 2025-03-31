using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;

namespace Altinn.Platform.Authentication.SystemIntegrationTests.Utils;

public class IdportenTestusers
{
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly HttpClient _client;

    public IdportenTestusers(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
        _client = new HttpClient(new HttpClientHandler { UseCookies = true, AllowAutoRedirect = true });
    }

    [Fact]
    public async Task GenerateToken()
    {
        // 🔥 Step 1: Authenticate and fetch CSRF Token + Session Cookie
        var (csrfToken, sessionCookie) = await AuthenticateAndGetCsrfToken();
        Assert.NotNull(csrfToken);
        Assert.NotNull(sessionCookie);

        // 🔥 Step 2: Make the actual API request using the extracted credentials
        await MakeAuthenticatedPostRequest(csrfToken, sessionCookie);
    }

    // ✅ Step 1: Authenticate and fetch CSRF Token + Session Cookie
    private async Task<(string?, string?)> AuthenticateAndGetCsrfToken()
    {
        const string authUrl = "https://testid.test.idporten.no/authorize?client_id=idporten&request_uri=urn%3Atestid%3AxthvIcZmdFMshVLksyc7FnDqUnT0hbPMNulGP6p59Vw";

        var request = new HttpRequestMessage(HttpMethod.Get, authUrl);
        request.Headers.Add("User-Agent", "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.3 Safari/605.1.15");

        var response = await _client.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();
        
        if (!response.IsSuccessStatusCode)
        {
            _testOutputHelper.WriteLine($"❌ Failed to authenticate: {response.StatusCode}");
            _testOutputHelper.WriteLine($"🔍 Authentication Page Response:\n{responseBody}");
            return (null, null);
        }

        // Extract CSRF token from HTML (Adjust regex if necessary)
        var csrfToken = ExtractCsrfToken(responseBody);
        var sessionCookie = ExtractSessionCookie(response.Headers);

        _testOutputHelper.WriteLine($"✅ CSRF Token: {csrfToken}");
        _testOutputHelper.WriteLine($"✅ Session Cookie: {sessionCookie}");

        return (csrfToken, sessionCookie);
    }

    // ✅ Extract CSRF Token from HTML response
    private string? ExtractCsrfToken(string html)
    {
        var match = Regex.Match(html, """<meta\s+name=['\"]_csrf_token['\"]\s+content=['\"]([^'\"]+)['\"]""", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : null;
    }

    // Extract Session Cookie from response headers
    private string? ExtractSessionCookie(HttpResponseHeaders headers)
    {
        return (from header in headers.GetValues("Set-Cookie") where header.StartsWith("SESSION=") select header.Split(';')[0]).FirstOrDefault();
    }

    // ✅ Step 2: Make an authenticated POST request using extracted credentials
    private async Task MakeAuthenticatedPostRequest(string csrfToken, string sessionCookie)
    {
        const string url = "https://testid.test.idporten.no/pid/manager";
        const string jsonPayload = "{}";

        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(jsonPayload, Encoding.UTF8, "text/plain")
        };

        // Add headers
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
        request.Headers.Add("Origin", "https://testid.test.idporten.no");
        request.Headers.Add("Referer", "https://testid.test.idporten.no/authorize?client_id=idporten&request_uri=urn%3Atestid%3AxthvIcZmdFMshVLksyc7FnDqUnT0hbPMNulGP6p59Vw");
        request.Headers.Add("X-CSRF-TOKEN", csrfToken); // ✅ Use the dynamically fetched CSRF token

        // Add the session cookie
        request.Headers.Add("Cookie", sessionCookie);

        // Send request
        var response = await _client.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        _testOutputHelper.WriteLine($"Response Status: {response.StatusCode}");
        _testOutputHelper.WriteLine($"Response Body: {responseBody}");

        Assert.True(response.IsSuccessStatusCode, "❌ Expected a successful response.");
    }
}