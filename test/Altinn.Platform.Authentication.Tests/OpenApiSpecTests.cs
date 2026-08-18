#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Tests.RepositoryDataAccess;
using Xunit;

namespace Altinn.Platform.Authentication.Tests;

/// <summary>
/// Guards the qualities of the generated OpenAPI document that customers depend on when they
/// generate API clients from it. These are contract concerns rather than behaviour: once an
/// operationId is published, generated client code binds to it, so a regression here is a
/// breaking change for every consumer.
/// </summary>
public class OpenApiSpecTests(DbFixture dbFixture, WebApplicationFixture webApplicationFixture)
    : WebApplicationTests(dbFixture, webApplicationFixture)
{
    private const string SwaggerDocumentUrl = "/authentication/swagger/v1/swagger.json";

    private static readonly string[] OperationKeys =
        ["get", "put", "post", "delete", "patch", "head", "options", "trace"];

    [Fact]
    public async Task OperationIds_ArePresent_Unique_AndSafeForClientGeneration()
    {
        HttpClient client = CreateClient();

        HttpResponseMessage response = await client.GetAsync(SwaggerDocumentUrl);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        List<(string Operation, string? OperationId)> operations = ReadOperations(document);

        // Sanity check: an empty document would make every assertion below vacuously true.
        Assert.NotEmpty(operations);

        List<string> problems = [];

        foreach ((string operation, string? operationId) in operations)
        {
            if (string.IsNullOrEmpty(operationId))
            {
                // Generators fall back to synthesising a name from the path, which collides for
                // route pairs such as /systemuser/{party} and /systemuser/{party}/{systemUserId}.
                problems.Add($"{operation} has no operationId");
                continue;
            }

            if (!IsValidIdentifier(operationId))
            {
                // Route names are reused as operationIds by default and may contain '/'.
                problems.Add($"{operation} has operationId '{operationId}', which is not a valid method name");
            }

            if (operationId.EndsWith("Async", StringComparison.Ordinal))
            {
                problems.Add($"{operation} has operationId '{operationId}'; the Async suffix is a C# convention and must not leak into the published API surface");
            }
        }

        problems.AddRange(operations
            .Where(o => !string.IsNullOrEmpty(o.OperationId))
            .GroupBy(o => o.OperationId, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => $"operationId '{g.Key}' is used by {g.Count()} operations: {string.Join(", ", g.Select(o => o.Operation))}"));

        string separator = Environment.NewLine + "  ";
        string message = "The generated OpenAPI document is not safe for client generation:" + separator + string.Join(separator, problems);

        Assert.True(problems.Count == 0, message);
    }

    private static List<(string Operation, string? OperationId)> ReadOperations(JsonDocument document)
    {
        List<(string Operation, string? OperationId)> operations = [];

        foreach (JsonProperty path in document.RootElement.GetProperty("paths").EnumerateObject())
        {
            foreach (JsonProperty method in path.Value.EnumerateObject())
            {
                // Path items also carry non-operation keys such as "parameters" and "summary".
                if (!OperationKeys.Contains(method.Name, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                string? operationId = method.Value.TryGetProperty("operationId", out JsonElement id)
                    ? id.GetString()
                    : null;

                operations.Add(($"{method.Name.ToUpperInvariant()} {path.Name}", operationId));
            }
        }

        return operations;
    }

    private static bool IsValidIdentifier(string value)
    {
        if (value.Length == 0 || (!char.IsLetter(value[0]) && value[0] != '_'))
        {
            return false;
        }

        foreach (char character in value)
        {
            if (!char.IsLetterOrDigit(character) && character != '_')
            {
                return false;
            }
        }

        return true;
    }
}
