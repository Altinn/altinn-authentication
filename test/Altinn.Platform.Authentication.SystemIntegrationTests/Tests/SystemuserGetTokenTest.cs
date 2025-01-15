using Altinn.Platform.Authentication.SystemIntegrationTests.Clients;
using Xunit;
using Xunit.Abstractions;

namespace Altinn.Platform.Authentication.SystemIntegrationTests.Tests;

// Documentation: https://docs.digdir.no/docs/Maskinporten/maskinporten_func_systembruker
public class SystemuserGetTokenTest
{
    private readonly ITestOutputHelper _outputHelper;
    private readonly SystemRegisterClient _systemRegisterClient;
    private readonly PlatformAuthenticationClient _platformClient;

    /// <summary>
    /// Testing System user endpoints
    /// To find relevant api endpoints: https://docs.altinn.studio/nb/api/authentication/spec/#/RequestSystemUser
    /// </summary>
    /// 
    public SystemuserGetTokenTest(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;
        _platformClient = new PlatformAuthenticationClient();
        _systemRegisterClient = new SystemRegisterClient(_platformClient);
    }

    [Fact]
    public async Task SystemuserGetToken_ReturnsTokenForOrg()
    {
        var maskinportenToken = await _platformClient.GetSystemUserToken();
        _outputHelper.WriteLine($"maskinportenToken: {maskinportenToken}");

    }
}