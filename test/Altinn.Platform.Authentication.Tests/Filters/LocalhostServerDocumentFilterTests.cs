#nullable enable
using Altinn.Platform.Authentication.Filters;
using Microsoft.OpenApi;
using Xunit;

namespace Altinn.Platform.Authentication.Tests.Filters;

/// <summary>
/// The internal document points at the running instance so that "Try it out" reaches it. The
/// shared Altinn servers filter throws when the host reports no listening address, which is the
/// case under the in-memory test server, so this must treat that as "no local server" instead.
/// </summary>
public class LocalhostServerDocumentFilterTests
{
    [Fact]
    public void PrefersHttps_WhenListeningOnBoth()
    {
        OpenApiServer? server = LocalhostServerDocumentFilter.TryCreateLocalServer(
            ["http://localhost:5000", "https://localhost:44377"]);

        Assert.NotNull(server);
        Assert.Equal("https://localhost:44377/authentication/api/v1", server.Url);
        Assert.Equal("Local development", server.Description);
    }

    [Fact]
    public void UsesTheOnlyAddress_WhenThereIsNoHttps()
    {
        OpenApiServer? server = LocalhostServerDocumentFilter.TryCreateLocalServer(["http://localhost:5000"]);

        Assert.NotNull(server);
        Assert.Equal("http://localhost:5000/authentication/api/v1", server.Url);
    }

    [Fact]
    public void DoesNotDuplicateTheSeparator_WhenTheAddressHasATrailingSlash()
    {
        OpenApiServer? server = LocalhostServerDocumentFilter.TryCreateLocalServer(["https://localhost:44377/"]);

        Assert.NotNull(server);
        Assert.Equal("https://localhost:44377/authentication/api/v1", server.Url);
    }

    [Theory]
    [InlineData]
    [InlineData((object[]?)null)]
    public void ReturnsNull_WhenThereIsNoAddressToPointAt(params string[]? addresses)
    {
        Assert.Null(LocalhostServerDocumentFilter.TryCreateLocalServer(addresses));
    }

    [Fact]
    public void ApplyTo_InsertsTheLocalServerFirst_OnTheInternalDocument()
    {
        // First in the list is what the UI selects by default, so the local address wins over
        // the deployed environments when you are running the service yourself.
        OpenApiDocument document = new()
        {
            Servers = [new OpenApiServer { Url = "https://platform.tt02.altinn.no/authentication/api/v1" }],
        };

        LocalhostServerDocumentFilter.ApplyTo(document, ApiDocuments.Internal, ["https://localhost:44377"]);

        Assert.Equal(2, document.Servers!.Count);
        Assert.Equal("https://localhost:44377/authentication/api/v1", document.Servers[0].Url);
        Assert.Equal("Local development", document.Servers[0].Description);
    }

    [Fact]
    public void ApplyTo_CreatesTheServerList_WhenTheDocumentHasNone()
    {
        OpenApiDocument document = new() { Servers = null };

        LocalhostServerDocumentFilter.ApplyTo(document, ApiDocuments.Internal, ["https://localhost:44377"]);

        Assert.Equal("https://localhost:44377/authentication/api/v1", Assert.Single(document.Servers!).Url);
    }

    [Fact]
    public void ApplyTo_DoesNothing_OnTheExternalDocument()
    {
        // A vendor has no use for an address only reachable on the developer's machine.
        OpenApiDocument document = new() { Servers = [] };

        LocalhostServerDocumentFilter.ApplyTo(document, ApiDocuments.External, ["https://localhost:44377"]);

        Assert.Empty(document.Servers!);
    }

    [Fact]
    public void ApplyTo_DoesNothing_WhenTheHostReportsNoAddresses()
    {
        // The case that makes the shared Altinn servers filter throw: no addresses registered,
        // as under the in-memory test server.
        OpenApiDocument document = new() { Servers = [] };

        LocalhostServerDocumentFilter.ApplyTo(document, ApiDocuments.Internal, []);

        Assert.Empty(document.Servers!);
    }
}
