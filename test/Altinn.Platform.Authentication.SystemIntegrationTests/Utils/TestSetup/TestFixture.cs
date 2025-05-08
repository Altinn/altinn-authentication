using Altinn.Platform.Authentication.SystemIntegrationTests.Clients;
using Altinn.Platform.Authentication.SystemIntegrationTests.Domain;

namespace Altinn.Platform.Authentication.SystemIntegrationTests.Utils.TestSetup;

public class TestFixture : IDisposable
{
    public PlatformAuthenticationClient Platform { get; }
    public Testuser Facilitator { get; set; }

    public TestFixture()
    {
        Platform = new PlatformAuthenticationClient();
        // Load facilitator and token once for all tests
    }

    public void Dispose()
    {
    }
}