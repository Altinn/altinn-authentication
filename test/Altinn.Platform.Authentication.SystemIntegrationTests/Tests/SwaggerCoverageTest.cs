using System.Text.Json;
using System.Text.RegularExpressions;
using Altinn.Platform.Authentication.SystemIntegrationTests.Utils;
using Xunit;
using Xunit.Abstractions;

namespace Altinn.Platform.Authentication.SystemIntegrationTests.Tests;

public class SwaggercoverageTest
{
    // This class will be used to cover future holes in e2e-test "coverage". For now just log it. 
    private const string SwaggerUrl = "https://docs.altinn.studio/swagger/altinn-platform-authentication-v1.json";
    private readonly ITestOutputHelper _outputHelper;

    // List of endpoints that should be ignored from coverage checks
    private static readonly HashSet<string> IgnoreEndpoints =
    [
        "authentication",
        "refresh",
        "exchange/{param}",
        "introspection",
        "logout",
        "frontchannel_logout",
        "openid/.well-known/openid-configuration",
        "openid/.well-known/openid-configuration/jwks",
        "exchange/{tokenProvider}"
    ];

    public SwaggercoverageTest(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;
    }

    [Fact]
    public async Task VerifyApiCoverage()
    {
        try
        {
            _outputHelper.WriteLine("🔍 Fetching Swagger API endpoints...");
            var apiEndpoints = await FetchApiEndpoints();
            _outputHelper.WriteLine($"✅ Found {apiEndpoints.Count} total endpoints from Swagger.");

            // Remove ignored endpoints BEFORE normalizing
            apiEndpoints.RemoveWhere(endpoint => IgnoreEndpoints.Any(ignore => endpoint.Url.Contains(ignore)));

            _outputHelper.WriteLine($"✅ After filtering, {apiEndpoints.Count} relevant API endpoints remain.");

            _outputHelper.WriteLine("🔍 Checking test coverage against defined API endpoints...");
            var testedEndpoints = ApiEndpointHelper.GetEndpoints();
            _outputHelper.WriteLine($"✅ Found {testedEndpoints.Count} tested endpoints.");

            // 🚀 Normalize both Swagger and tested URLs before comparison
            apiEndpoints = new HashSet<ApiEndpoint>(apiEndpoints.Select(e => new ApiEndpoint(e.Url, new HttpMethod(e.Method.Method.ToUpper()))));
            testedEndpoints = new List<ApiEndpoint>(testedEndpoints.Select(e => new ApiEndpoint(e.Url, new HttpMethod(e.Method.Method.ToUpper()))));

            // 🚀 Compute matched and missing endpoints
            var matchedEndpoints = apiEndpoints.Intersect(testedEndpoints).ToList();
            var missingEndpoints = apiEndpoints.Except(testedEndpoints).ToList();
            var extraTestedEndpoints = testedEndpoints.Except(apiEndpoints).ToList(); // 🔥 Extra tested endpoints

            _outputHelper.WriteLine("\n📊 API Coverage Report:");
            _outputHelper.WriteLine($"✅ Covered Endpoints: {matchedEndpoints.Count}/{apiEndpoints.Count}");
            _outputHelper.WriteLine($"❌ Missing Endpoints: {missingEndpoints.Count}");

            // ✅ Print Covered Endpoints
            if (matchedEndpoints.Any())
            {
                _outputHelper.WriteLine("\n✅ The following API endpoints **are covered** in tests:");
                foreach (var endpoint in matchedEndpoints)
                {
                    _outputHelper.WriteLine($"- `{endpoint.Method} {endpoint.Url}`");
                }
            }

            // ❌ Print Missing Endpoints
            if (missingEndpoints.Any())
            {
                _outputHelper.WriteLine("\n❌ The following API endpoints **are not covered** in end to end tests:");
                foreach (var endpoint in missingEndpoints)
                {
                    _outputHelper.WriteLine($"- `{endpoint.Method} {endpoint.Url}`");
                }
            }
            
            if (extraTestedEndpoints.Any())
            {
                _outputHelper.WriteLine($"\n⚠️ **Extra Tested Endpoints (Not in Swagger)**: +{extraTestedEndpoints.Count}");
                foreach (var extra in extraTestedEndpoints)
                {
                    _outputHelper.WriteLine($"- `{extra.Method} {extra.Url}`");
                }
            }
            
            else
            {
                _outputHelper.WriteLine("\n✅ All API endpoints are covered in end to end tests!");
            }
        }
        catch (Exception ex)
        {
            _outputHelper.WriteLine($"🚨 Error: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Fetches API endpoints from Swagger JSON.
    /// </summary>
    private static async Task<HashSet<ApiEndpoint>> FetchApiEndpoints()
    {
        using var client = new HttpClient();
        var response = await client.GetStringAsync(SwaggerUrl);
        using var doc = JsonDocument.Parse(response);

        var paths = doc.RootElement.GetProperty("paths");
        var endpoints = new HashSet<ApiEndpoint>();

        foreach (var path in paths.EnumerateObject())
        {
            string url = path.Name.TrimStart('/'); // Remove leading slash

            foreach (var method in path.Value.EnumerateObject()) // GET, POST, etc.
            {
                var httpMethod = new HttpMethod(method.Name.ToUpper());
                endpoints.Add(new ApiEndpoint(url, httpMethod));
            }
        }

        return endpoints;
    }

    /// <summary>
    /// Normalizes API endpoints:
    /// - Ensures "v1/" is prefixed if missing.
    /// - Converts "{variable}" placeholders to "{param}".
    /// </summary>
    private static string NormalizeEndpoint(string endpoint)
    {
        if (!endpoint.StartsWith("v1/"))
        {
            endpoint = "v1/" + endpoint;
        }

        return Regex.Replace(endpoint, @"\{[^}]+\}", "{param}");
    }
}