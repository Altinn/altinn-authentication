using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.SystemIntegrationTests.Utils;
using Xunit;
using Xunit.Abstractions;

public class ApiCoverageTest
{
    private const string SwaggerUrl = "https://docs.altinn.studio/swagger/altinn-platform-authentication-v1.json";
    private readonly ITestOutputHelper _outputHelper;

    // List of endpoints that should be ignored from coverage checks
    private static readonly HashSet<string> IgnoreEndpoints = new()
    {
        "authentication",
        "refresh",
        "exchange/{param}",
        "introspection",
        "logout",
        "frontchannel_logout",
        "openid/.well-known/openid-configuration",
        "openid/.well-known/openid-configuration/jwks"
    };

    public ApiCoverageTest(ITestOutputHelper outputHelper)
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
            apiEndpoints.RemoveWhere(endpoint => IgnoreEndpoints.Any(ignore => endpoint.Contains(ignore)));

            _outputHelper.WriteLine($"✅ After filtering, {apiEndpoints.Count} relevant API endpoints remain.");

            _outputHelper.WriteLine("🔍 Checking test coverage against UrlConstants...");
            var testedEndpoints = GetTestedEndpoints();
            _outputHelper.WriteLine($"✅ Found {testedEndpoints.Count} tested endpoints.");

            // Create mapping of original Swagger API paths -> normalized format
            var normalizedApiMap = apiEndpoints.ToDictionary(
                key => key, 
                value => NormalizeEndpoint(value)
            );

            var normalizedApiEndpoints = normalizedApiMap.Values.ToHashSet();
            var normalizedTestedEndpoints = testedEndpoints.Select(NormalizeEndpoint).ToHashSet();

            var matchedEndpoints = normalizedTestedEndpoints.Intersect(normalizedApiEndpoints).ToList();
            var missingNormalizedEndpoints = normalizedApiEndpoints.Except(normalizedTestedEndpoints).ToList();

            // Reverse map missing normalized endpoints back to original Swagger format
            var missingOriginalEndpoints = normalizedApiMap
                .Where(kvp => missingNormalizedEndpoints.Contains(kvp.Value))
                .Select(kvp => kvp.Key)
                .ToList();

            _outputHelper.WriteLine("\n📊 API Coverage Report:");
            _outputHelper.WriteLine($"✅ Covered Endpoints: {matchedEndpoints.Count}/{normalizedApiEndpoints.Count}");
            _outputHelper.WriteLine($"❌ Missing Endpoints: {missingOriginalEndpoints.Count}");

            if (missingOriginalEndpoints.Any())
            {
                _outputHelper.WriteLine("\n❌ The following API endpoints are **not covered** in tests:");
                foreach (var endpoint in missingOriginalEndpoints)
                {
                    _outputHelper.WriteLine($"- `{endpoint}`");
                }
            }
            else
            {
                _outputHelper.WriteLine("\n✅ All API endpoints are covered in tests!");
            }
        }
        catch (Exception ex)
        {
            _outputHelper.WriteLine($"🚨 Error: {ex.Message}");
            throw; // Let xUnit handle unexpected failures
        }
    }

    /// <summary>
    /// Fetches API endpoints from Swagger JSON.
    /// </summary>
    private static async Task<HashSet<string>> FetchApiEndpoints()
    {
        using var client = new HttpClient();
        var response = await client.GetStringAsync(SwaggerUrl);
        using var doc = JsonDocument.Parse(response);

        var paths = doc.RootElement.GetProperty("paths");
        return paths.EnumerateObject()
            .Select(p => p.Name.TrimStart('/')) // Remove leading slash for consistency
            .ToHashSet();
    }

    /// <summary>
    /// Gets all tested API endpoints from UrlConstants.
    /// </summary>
    private static HashSet<string> GetTestedEndpoints()
    {
        return typeof(UrlConstants)
            .GetFields()
            .Where(f => f.IsLiteral && !f.IsInitOnly) // Get all public const fields
            .Select(f => f.GetValue(null)?.ToString().TrimStart('/')) // Remove leading slash
            .Where(url => !string.IsNullOrEmpty(url))
            .ToHashSet();
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