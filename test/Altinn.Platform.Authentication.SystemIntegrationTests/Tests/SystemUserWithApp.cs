using Altinn.Platform.Authentication.SystemIntegrationTests.Clients;
using Altinn.Platform.Authentication.SystemIntegrationTests.Utils;
using Xunit;
using Xunit.Abstractions;

namespace Altinn.Platform.Authentication.SystemIntegrationTests.Tests;

public class SystemUserWithApp
{
    private readonly ITestOutputHelper _outputHelper;
    private readonly SystemRegisterClient _systemRegisterClient;
    private readonly PlatformAuthenticationClient _platformClient;

    /// <summary>
    /// Testing System user endpoints
    /// To find relevant api endpoints: https://docs.altinn.studio/nb/api/authentication/spec/#/RequestSystemUser
    /// </summary>
    /// 
    public SystemUserWithApp(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;
        _platformClient = new PlatformAuthenticationClient();
        _systemRegisterClient = new SystemRegisterClient(_platformClient);
    }

    /// <summary>
    /// https://github.com/Altinn/altinn-authentication/issues/586
    /// API for creating request for System User
    /// </summary>
    [Fact]
    public async Task PostRequestSystemUserTest_WithApp()
    {
        // Arrange
        var maskinportenToken = await _platformClient.GetMaskinportenToken();

        // Selecting which data to be posted to System Register
        var registerState = new SystemRegisterHelper("Resources/Testdata/Systemregister/CreateNewSystem.json")
            .WithClientId(Guid.NewGuid().ToString())
            .WithVendor(_platformClient.EnvironmentHelper.Vendor)
            .WithResource(value: "app_ttd_endring-av-navn-v2", id: "urn:altinn:resource") //APP
            .WithResource(value: "vegardtestressurs", id: "urn:altinn:resource")
            .WithRedirectUrl("https://altinn.no");

        await _systemRegisterClient.RegisterSystem(registerState, maskinportenToken);

        // Creating system user request with the same resources posted to System register
        var requestBody = await SystemUserTests.PrepareSystemUserRequest(registerState);

        // Act
        var systemUserRequestResponse = await _platformClient.PostAsync(UrlConstants.CreateSystemUserRequestBaseUrl, requestBody, maskinportenToken);

        var systemUserResponse = await systemUserRequestResponse.Content.ReadAsStringAsync();
        var id = Common.ExtractPropertyFromJson(systemUserResponse, "id");
        var systemId = Common.ExtractPropertyFromJson(systemUserResponse, "systemId");
        var testperson = _platformClient.TestUsers.Find(testUser => testUser.Org.Equals(_platformClient.EnvironmentHelper.Vendor))
                         ?? throw new Exception($"Test user not found for organization: {_platformClient.EnvironmentHelper}");

        // // Act
        await _platformClient.ApproveSystemUserRequest(testperson.AltinnPartyId, id);
        
        //Cleanup
        var systemUserResponseContent = await _platformClient.GetSystemUserBySystemIdForVendor(systemId, maskinportenToken);
        var content = await systemUserResponseContent.Content.ReadAsStringAsync();
        var systemUserId = Common.ExtractSystemUserId(content);
        await _platformClient.DeleteSystemUser(testperson.AltinnPartyId, systemUserId);
    }
    
}