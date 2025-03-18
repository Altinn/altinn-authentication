using System.Net;
using Altinn.Platform.Authentication.SystemIntegrationTests.Clients;
using Altinn.Platform.Authentication.SystemIntegrationTests.Utils;
using Xunit;
using Xunit.Abstractions;

namespace Altinn.Platform.Authentication.SystemIntegrationTests.Tests.ClientDelegation;


// Fasilitatoren = partyid til partyorg for client (agent) request. 
// org til system id = kan f.eks være Visma
// Tilgangspakker foreløpig: https://github.com/Altinn/altinn-authentication/blob/main/src/Integration/MockData/packages.json
public class ClientDelegationTests
{
    private string _accessPackageName = "urn:altinn:accesspackage:skattnaering";
    private readonly ITestOutputHelper _outputHelper;
    private readonly PlatformAuthenticationClient _platformClient;
    private readonly SystemRegisterClient _systemRegisterClient;
    private readonly Common _common;

    /// <summary>
    /// Systemregister tests
    /// </summary>
    /// <param name="outputHelper">For test logging purposes</param>
    public ClientDelegationTests(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;
        _platformClient = new PlatformAuthenticationClient();
        _systemRegisterClient = new SystemRegisterClient(_platformClient);
        _common = new Common(_platformClient, outputHelper);
    }
    
    
    [Fact]
    public async Task CreateClientRequest()
    {
        _accessPackageName = "urn:altinn:accesspackage:skattnaering";
        
        // Prepare
        var maskinportenToken = await _platformClient.GetMaskinportenTokenForVendor();
        var externalRef = Guid.NewGuid().ToString();
        var clientId = Guid.NewGuid().ToString();
        var testperson = _platformClient.GetTestUserForVendor();
        var testState = new TestState("Resources/Testdata/ClientDelegation/AccessPackage.json")
            .WithClientId(clientId)
            .WithVendor(testperson.Org)
            .WithAccessPackage("skattnaering")
            .WithToken(maskinportenToken);

        var requestBodySystemRegister = testState.GenerateRequestBody();
        var response = await _systemRegisterClient.PostSystem(requestBodySystemRegister, maskinportenToken);
        Assert.True(response.IsSuccessStatusCode, response.ReasonPhrase);

        // Prepare system user request
        var requestBody = (await Helper.ReadFile("Resources/Testdata/ClientDelegation/CreateRequest.json"))
            .Replace("{systemId}", testState.SystemId)
            .Replace("{accessPackage}", _accessPackageName)
            .Replace("{externalRef}", externalRef)
            .Replace("{partyOrgNo}", testperson.Org);

        // Act
        var userResponse = await _platformClient.PostAsync(ApiEndpoints.PostAgentClientRequest.Url(), requestBody, maskinportenToken);
        
        // Assert
        var content = await userResponse.Content.ReadAsStringAsync();

        Assert.True(userResponse.StatusCode == HttpStatusCode.Created,
            $"Unexpected status code: {userResponse.StatusCode} - {content}");

        var requestId = Common.ExtractPropertyFromJson(content, "id");

        //Assert status is new
        await AssertStatusSystemUserRequest(requestId, "New", maskinportenToken);

        var agentUser = await _common.GetSystemUserForVendor(testState.SystemId, maskinportenToken);
        _outputHelper.WriteLine($"Agent user response {await agentUser.ReadAsStringAsync()}");

        var user = _platformClient.GetTestUserForVendor();

        //Approve system user agent request
        var approveResp =
            await _common.ApproveRequest(ApiEndpoints.ApproveAgentRequest.Url()
                    .Replace("{party}", user.AltinnPartyId)
                    .Replace("{requestId}", requestId), 
                user);

        Assert.True(HttpStatusCode.OK == approveResp.StatusCode,
            "Received status code " + approveResp.StatusCode + "when attempting to approve");
        
        await AssertStatusSystemUserRequest(requestId, "Accepted", maskinportenToken);
    }

    private async Task AssertStatusSystemUserRequest(string requestId, string expectedStatus, string maskinportenToken)
    {
        var getRequestByIdUrl = ApiEndpoints.GetVendorAgentRequestById.Url().Replace("{requestId}", requestId);
        var responseGetByRequestId = await _platformClient.GetAsync(getRequestByIdUrl, maskinportenToken);

        Assert.Equal(HttpStatusCode.OK, responseGetByRequestId.StatusCode);
        Assert.Contains(requestId, await responseGetByRequestId.Content.ReadAsStringAsync());

        var status = Common.ExtractPropertyFromJson(await responseGetByRequestId.Content.ReadAsStringAsync(), "status");
        Assert.True(expectedStatus.Equals(status), $"Status is not {expectedStatus} but: {status}");
    }
    
    private async Task AssertSystemUserAgentCreated(string systemId, string externalRef, string maskinportenToken)
    {
        // Verify system user was updated // created (Does in fact not verify anything was updated, but easier to add in the future
        var respGetSystemUsersForVendor = await _common.GetSystemUserForVendor(systemId, maskinportenToken);
        var systemusersRespons = await respGetSystemUsersForVendor.ReadAsStringAsync();

        // Assert systemId
        Assert.Contains(systemId, systemusersRespons);
        Assert.Contains(externalRef, systemusersRespons);
    }
    
    public async Task<HttpResponseMessage> GetVendorAgentRequestByExternalRef(string systemId, string orgNo, string externalRef, string maskinportenToken)
    {
        var url = ApiEndpoints.GetVendorAgentRequestByExternalRef.Url()
            .Replace("{systemId}", systemId)
            .Replace("{orgNo}", orgNo)
            .Replace("{externalRef}", externalRef);

        return await _platformClient.GetAsync(url, maskinportenToken);
    }
}