#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Altinn.Authorization.ServiceDefaults.Authorization.Scopes;
using Altinn.Common.PEP.Authorization;
using Altinn.Platform.Authentication.Filters;
using Altinn.Platform.Authentication.Tests.RepositoryDataAccess;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.DependencyInjection;
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
    private static readonly string[] OperationKeys =
        ["get", "put", "post", "delete", "patch", "head", "options", "trace"];

    [Theory]
    [InlineData("v1")]
    [InlineData("internal")]
    public async Task OperationIds_ArePresent_Unique_AndSafeForClientGeneration(string documentName)
    {
        HttpClient client = CreateClient();

        HttpResponseMessage response = await client.GetAsync($"/authentication/swagger/{documentName}/swagger.json");
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

    /// <summary>
    /// The scopes the document advertises for an endpoint must be the scopes its authorization
    /// policies actually enforce.
    /// </summary>
    /// <remarks>
    /// The mapping from policy to scope is maintained by hand in
    /// <see cref="SecurityRequirementsDocumentFilter"/>, mirroring the policies registered in
    /// AuthenticationHost. This compares the published document against the registered policies -
    /// the real source of truth - so the two cannot drift apart silently. An unmapped policy
    /// degrades quietly to "token required" at runtime, which would otherwise leave an endpoint
    /// under-documented with nothing to notice it.
    /// </remarks>
    [Theory]
    [InlineData("v1")]
    [InlineData("internal")]
    public async Task DocumentedScopes_MatchThePoliciesEndpointsEnforce(string documentName)
    {
        HttpClient client = CreateClient();

        using JsonDocument document =
            JsonDocument.Parse(await client.GetStringAsync($"/authentication/swagger/{documentName}/swagger.json"));

        IAuthorizationPolicyProvider policyProvider = Services.GetRequiredService<IAuthorizationPolicyProvider>();

        Dictionary<string, ApiDescription> byOperationId = [];
        foreach (ApiDescription apiDescription in Services
            .GetRequiredService<IApiDescriptionGroupCollectionProvider>()
            .ApiDescriptionGroups.Items.SelectMany(g => g.Items))
        {
            if (SecurityRequirementsDocumentFilter.GetOperationId(apiDescription) is { } id)
            {
                byOperationId[id] = apiDescription;
            }
        }

        HashSet<string> declaredScopes = ReadDeclaredScopes(document);
        Assert.NotEmpty(declaredScopes);

        List<string> problems = [];

        foreach (JsonProperty path in document.RootElement.GetProperty("paths").EnumerateObject())
        {
            foreach (JsonProperty method in path.Value.EnumerateObject())
            {
                if (!OperationKeys.Contains(method.Name, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                string operationId = method.Value.GetProperty("operationId").GetString()!;
                string where = $"{method.Name.ToUpperInvariant()} {path.Name} ({operationId})";

                if (!byOperationId.TryGetValue(operationId, out ApiDescription? apiDescription))
                {
                    problems.Add($"{where} has no matching endpoint");
                    continue;
                }

                HashSet<string> documented = ReadDocumentedScopes(method.Value);
                HashSet<string> enforced = await EnforcedScopesAsync(apiDescription, policyProvider);

                foreach (string missing in enforced.Except(documented).Order())
                {
                    problems.Add($"{where} enforces scope '{missing}' but the document does not declare it");
                }

                foreach (string extra in documented.Except(enforced).Order())
                {
                    problems.Add($"{where} documents scope '{extra}' but no policy on it requires that scope");
                }

                foreach (string undeclared in documented.Except(declaredScopes).Order())
                {
                    problems.Add($"{where} references scope '{undeclared}', which is not listed in the security scheme");
                }
            }
        }

        string separator = Environment.NewLine + "  ";
        string message = "The documented scopes do not match the policies the endpoints enforce:" + separator + string.Join(separator, problems);

        Assert.True(problems.Count == 0, message);
    }

    private static HashSet<string> ReadDeclaredScopes(JsonDocument document)
    {
        HashSet<string> scopes = new(StringComparer.Ordinal);

        if (document.RootElement.TryGetProperty("components", out JsonElement components)
            && components.TryGetProperty("securitySchemes", out JsonElement schemes)
            && schemes.TryGetProperty(SecurityRequirementsDocumentFilter.ScopeSchemeId, out JsonElement scheme)
            && scheme.TryGetProperty("flows", out JsonElement flows)
            && flows.TryGetProperty("clientCredentials", out JsonElement flow)
            && flow.TryGetProperty("scopes", out JsonElement declared))
        {
            foreach (JsonProperty scope in declared.EnumerateObject())
            {
                scopes.Add(scope.Name);
            }
        }

        return scopes;
    }

    private static HashSet<string> ReadDocumentedScopes(JsonElement operation)
    {
        HashSet<string> scopes = new(StringComparer.Ordinal);

        if (!operation.TryGetProperty("security", out JsonElement security))
        {
            return scopes;
        }

        foreach (JsonElement requirement in security.EnumerateArray())
        {
            if (!requirement.TryGetProperty(SecurityRequirementsDocumentFilter.ScopeSchemeId, out JsonElement listed))
            {
                continue;
            }

            foreach (JsonElement scope in listed.EnumerateArray())
            {
                scopes.Add(scope.GetString()!);
            }
        }

        return scopes;
    }

    private static async Task<HashSet<string>> EnforcedScopesAsync(
        ApiDescription apiDescription,
        IAuthorizationPolicyProvider policyProvider)
    {
        HashSet<string> scopes = new(StringComparer.Ordinal);
        IList<object> metadata = apiDescription.ActionDescriptor.EndpointMetadata;

        // The filter documents no security at all for anonymous endpoints, so no scopes either.
        if (metadata.OfType<IAllowAnonymous>().Any())
        {
            return scopes;
        }

        foreach (string policyName in metadata
            .OfType<IAuthorizeData>()
            .Select(a => a.Policy)
            .Where(p => !string.IsNullOrEmpty(p))
            .Distinct()!)
        {
            AuthorizationPolicy? policy = await policyProvider.GetPolicyAsync(policyName);
            if (policy is null)
            {
                continue;
            }

            // Scopes reach a policy two ways: RequireScopeAnyOf, and the hand-written
            // requirement that accepts either a scope or a platform access token.
            foreach (IAuthorizationRequirement requirement in policy.Requirements)
            {
                IEnumerable<string> required = requirement switch
                {
                    IScopeAnyOfAuthorizationRequirement anyOf => anyOf.AnyOfScopes,
                    IScopeAccessRequirement scoped => scoped.Scope,
                    _ => [],
                };

                foreach (string scope in required)
                {
                    scopes.Add(scope);
                }
            }
        }

        return scopes;
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
