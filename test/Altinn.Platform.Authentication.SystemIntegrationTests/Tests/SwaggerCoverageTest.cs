using System.Reflection;
using System.Text.Json;
using Altinn.Platform.Authentication.SystemIntegrationTests.Utils;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;
using Xunit.Abstractions;

namespace Altinn.Platform.Authentication.SystemIntegrationTests.Tests;

public class SwaggerCoverageTest
{
    private const string SwaggerUrl = "https://docs.altinn.studio/swagger/altinn-platform-authentication-v1.json";
    private readonly ITestOutputHelper _outputHelper;

    private static readonly HashSet<string> IgnoreEndpoints =
    [
        "party",
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

    public SwaggerCoverageTest(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;
    }

    [Fact]
    public async Task VerifyApiCoverage()
    {
        var apiEndpoints = await FetchApiEndpointsFromSwagger();
        var allTestedEndpoints = ApiEndpointHelper.GetEndpoints();
        var usedEndpoints = FindUsedEndpoints();
        var testedEndpoints = allTestedEndpoints.Intersect(usedEndpoints).ToHashSet();

        apiEndpoints = apiEndpoints.Select(NormalizeEndpoint).ToHashSet();
        testedEndpoints = testedEndpoints.Select(NormalizeEndpoint).ToHashSet();

        var matchedEndpoints = apiEndpoints.Intersect(testedEndpoints).ToList();
        var missingEndpoints = apiEndpoints.Except(testedEndpoints)
            .Where(e => !ShouldIgnore(e))
            .ToList();

        var extraTestedEndpoints = testedEndpoints.Except(apiEndpoints)
            .Where(e => !ShouldIgnore(e))
            .ToList();
        
        var totalEndpoints = apiEndpoints.Count(e => !ShouldIgnore(e));
        var coveredEndpoints = matchedEndpoints.Count(e => !ShouldIgnore(e));

        // 📊 Generate Report
        _outputHelper.WriteLine("\n📊 API Coverage Report");
        _outputHelper.WriteLine($"✅ Covered Endpoints: {coveredEndpoints}/{totalEndpoints}");

        if (missingEndpoints.Any())
        {
            _outputHelper.WriteLine("\n❌ The following API endpoints **are not covered** in end-to-end tests:");
            missingEndpoints.ForEach(endpoint => _outputHelper.WriteLine($"- `{endpoint.Method} {endpoint.Url}`"));
        }

        if (extraTestedEndpoints.Any())
        {
            _outputHelper.WriteLine($"\n⚠️ Tested Endpoints that are NOT in Swagger in TT02");
            extraTestedEndpoints.ForEach(endpoint => _outputHelper.WriteLine($"- `{endpoint.Method} {endpoint.Url}`"));
        }
    }

    private static bool ShouldIgnore(ApiEndpoint endpoint) =>
        IgnoreEndpoints.Any(ignore => endpoint.Url.Contains(ignore, StringComparison.OrdinalIgnoreCase));

    private static ApiEndpoint NormalizeEndpoint(ApiEndpoint endpoint) =>
        new(endpoint.Url.ToLower(), new HttpMethod(endpoint.Method.Method.ToUpper()));

    private static HashSet<ApiEndpoint> FindUsedEndpoints()
    {
        var usedEndpoints = new HashSet<ApiEndpoint>();
        var repoRoot = Path.Combine(AppContext.BaseDirectory, "../../../../../../altinn-authentication/");
        var testProjectRoot = Path.Combine(repoRoot, "test/Altinn.Platform.Authentication.SystemIntegrationTests/");
        var testFiles = Directory.EnumerateFiles(testProjectRoot, "*.cs", SearchOption.AllDirectories)
            .Where(f => f.Contains("Tests"));

        foreach (var file in testFiles)
        {
            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file));
            var root = tree.GetRoot();

            var enumReferences = root.DescendantNodes()
                .OfType<MemberAccessExpressionSyntax>()
                .Where(m => m.Expression is IdentifierNameSyntax id && id.Identifier.Text == "ApiEndpoints")
                .Select(m => m.Name.Identifier.Text);

            foreach (var reference in enumReferences)
            {
                if (GetApiEndpointByName(reference) is { } endpoint)
                {
                    usedEndpoints.Add(endpoint);
                }
            }
        }
        return usedEndpoints;
    }

    private static ApiEndpoint? GetApiEndpointByName(string name)
    {
        var field = typeof(ApiEndpoints).GetField(name, BindingFlags.Public | BindingFlags.Static);
        var attribute = field?.GetCustomAttribute<EndpointInfoAttribute>();
        return attribute != null ? new ApiEndpoint(attribute.Url, attribute.Method) : null;
    }

    private static async Task<HashSet<ApiEndpoint>> FetchApiEndpointsFromSwagger()
    {
        using var client = new HttpClient();
        var response = await client.GetStringAsync(SwaggerUrl);
        using var doc = JsonDocument.Parse(response);

        return doc.RootElement.GetProperty("paths")
            .EnumerateObject()
            .SelectMany(path =>
                path.Value.EnumerateObject()
                    .Select(method => new ApiEndpoint(path.Name.Trim('/'), new HttpMethod(method.Name.ToUpper()))))
            .ToHashSet();
    }
}