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
}
