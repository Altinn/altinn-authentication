#nullable enable
using System.Linq;
using Altinn.Platform.Authentication.Filters;
using Microsoft.OpenApi;
using Xunit;

namespace Altinn.Platform.Authentication.Tests.Filters;

/// <summary>
/// The shared API base path lives in the server URLs, so it must not also appear in the paths -
/// a server URL and a path concatenate to the real endpoint. These cover the shapes the
/// generated document does not currently produce, so that a malformed or unusual document
/// degrades instead of throwing.
/// </summary>
/// <remarks>
/// The filter never reads its DocumentFilterContext argument, which is why these can call it
/// directly - that type cannot be constructed, since its DocumentName has no setter.
/// </remarks>
public class ApiBasePathDocumentFilterTests
{
    private static readonly ApiBasePathDocumentFilter Filter = new();

    [Fact]
    public void StripsTheBasePath_FromEveryPath()
    {
        OpenApiDocument document = new()
        {
            Paths = new OpenApiPaths
            {
                ["/authentication/api/v1/enduser/systemuser/clients"] = new OpenApiPathItem(),
                ["/authentication/api/v1/systemregister"] = new OpenApiPathItem(),
            },
        };

        Filter.Apply(document, null!);

        Assert.Equal(
            ["/enduser/systemuser/clients", "/systemregister"],
            document.Paths!.Keys.OrderBy(k => k, System.StringComparer.Ordinal));
    }

    [Fact]
    public void MapsTheBasePathItself_ToRoot()
    {
        // A path must never be empty, and must start with '/'.
        OpenApiDocument document = new()
        {
            Paths = new OpenApiPaths { ["/authentication/api/v1"] = new OpenApiPathItem() },
        };

        Filter.Apply(document, null!);

        Assert.Equal("/", Assert.Single(document.Paths!.Keys));
    }

    [Fact]
    public void LeavesPathsAlone_WhenTheyDoNotCarryTheBasePath()
    {
        OpenApiDocument document = new()
        {
            Paths = new OpenApiPaths { ["/health"] = new OpenApiPathItem() },
        };

        Filter.Apply(document, null!);

        Assert.Equal("/health", Assert.Single(document.Paths!.Keys));
    }

    [Fact]
    public void DoesNothing_WhenTheDocumentHasNoPaths()
    {
        // Paths is non-nullable, so this can only happen if another filter clears it - the guard
        // exists so that would degrade instead of throwing on the Clear() below.
        OpenApiDocument document = new() { Paths = null! };

        Filter.Apply(document, null!);

        Assert.Null(document.Paths);
    }

    [Fact]
    public void CollapsesTheDoubleSlash_LeftByTheSharedServersFilter()
    {
        OpenApiDocument document = new()
        {
            Servers = [new OpenApiServer { Url = "https://platform.altinn.no//authentication/api/v1" }],
        };

        Filter.Apply(document, null!);

        Assert.Equal("https://platform.altinn.no/authentication/api/v1", document.Servers![0].Url);
    }

    [Fact]
    public void DoesNothing_WhenTheDocumentHasNoServers()
    {
        OpenApiDocument document = new() { Servers = null };

        Filter.Apply(document, null!);

        Assert.Null(document.Servers);
    }

    [Fact]
    public void SkipsAServer_WithNoUrl()
    {
        OpenApiDocument document = new()
        {
            Servers = [new OpenApiServer { Url = null }, new OpenApiServer { Url = "https://x//y" }],
        };

        Filter.Apply(document, null!);

        Assert.Null(document.Servers![0].Url);
        Assert.Equal("https://x/y", document.Servers[1].Url);
    }

    [Fact]
    public void LeavesARelativeServerUrlAlone()
    {
        // No scheme means there is no host to separate from the path, so there is nothing to fix.
        OpenApiDocument document = new()
        {
            Servers = [new OpenApiServer { Url = "//authentication/api/v1" }],
        };

        Filter.Apply(document, null!);

        Assert.Equal("//authentication/api/v1", document.Servers![0].Url);
    }
}
