using Altinn.Platform.Authentication.SystemIntegrationTests.Clients;
using Altinn.Platform.Authentication.SystemIntegrationTests.Domain;

namespace Altinn.Platform.Authentication.SystemIntegrationTests.Utils.TestSetup;

public class TestFixture : IDisposable
{
    public PlatformAuthenticationClient Platform { get; }

    protected TestFixture()
    {
        Platform = new PlatformAuthenticationClient();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}