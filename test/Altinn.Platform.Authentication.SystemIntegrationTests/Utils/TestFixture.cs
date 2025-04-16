using Altinn.Platform.Authentication.SystemIntegrationTests.Clients;
using Altinn.Platform.Authentication.SystemIntegrationTests.Domain;
using Xunit.Abstractions;

namespace Altinn.Platform.Authentication.SystemIntegrationTests.Utils;

public class TestFixture : IDisposable
{
    public PlatformAuthenticationClient Platform { get; }
    
    public Testuser Facilitator { get; }
    public string? FacilitatorToken { get; }

    public TestFixture()
    {
        Platform = new PlatformAuthenticationClient();
        
        // Load facilitator and token once for all tests
        Facilitator = Platform.GetTestUserWithCategory("facilitator");
        FacilitatorToken = Platform.GetPersonalAltinnToken(Facilitator).Result;
        Facilitator.AltinnToken = FacilitatorToken;
    }

    public void Dispose()
    {
        // Optionally clean up test data (delegations, systemusers, etc.)
    }
}