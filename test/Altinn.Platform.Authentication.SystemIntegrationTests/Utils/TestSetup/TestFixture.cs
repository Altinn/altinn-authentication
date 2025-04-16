using Altinn.Platform.Authentication.SystemIntegrationTests.Clients;
using Altinn.Platform.Authentication.SystemIntegrationTests.Domain;

namespace Altinn.Platform.Authentication.SystemIntegrationTests.Utils.TestSetup;

public class TestFixture : IDisposable
{
    public PlatformAuthenticationClient Platform { get; }
    public Testuser Facilitator { get; }

    public TestFixture()
    {
        Platform = new PlatformAuthenticationClient();
        
        // Load facilitator and token once for all tests
        Facilitator = Platform.GetTestUserWithCategory("facilitator");
        Facilitator.AltinnToken = Platform.GetPersonalAltinnToken(Facilitator).Result;;
    }

    public void Dispose()
    {
        // Optionally clean up test data (delegations, systemusers, etc.)
    }
}