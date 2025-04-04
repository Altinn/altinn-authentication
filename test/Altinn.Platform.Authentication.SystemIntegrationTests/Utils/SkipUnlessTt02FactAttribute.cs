using Altinn.Platform.Authentication.SystemIntegrationTests.Clients;
using Xunit;

namespace Altinn.Platform.Authentication.SystemIntegrationTests.Utils;

[Trait("Category", "IntegrationTest")]
public sealed class SkipUnlessTt02FactAttribute : FactAttribute
{
    private readonly PlatformAuthenticationClient _platformClient;
    public SkipUnlessTt02FactAttribute()
    {
        _platformClient = new PlatformAuthenticationClient();
        if (!IsTt02())
        {
            Skip = "Skipping test: this test will only run in TT02 due to Maskinporten's configuration using our TT02 environment";
        }
    }

    private bool IsTt02()
    {
        return _platformClient.EnvironmentHelper.Testenvironment.Contains("tt02", StringComparison.InvariantCultureIgnoreCase);
    }
    
    
}