using System.Net;
using System.Text.Json;
using Altinn.Platform.Authentication.SystemIntegrationTests.Clients;
using Altinn.Platform.Authentication.SystemIntegrationTests.Domain;
using Altinn.Platform.Authentication.SystemIntegrationTests.Utils;
using Xunit;
using Xunit.Abstractions;

namespace Altinn.Platform.Authentication.SystemIntegrationTests.Tests.ClientDelegation;

// Fasilitatoren = partyid til partyorg for client (agent) request. 
// org til system id = kan f.eks være Visma
// Tilgangspakker foreløpig: https://github.com/Altinn/altinn-authentication/blob/main/src/Integration/MockData/packages.json

// Delegation request
// customerId : kundens party UUID
// facilitatorId: party UUID til party som eier systembrukeren (samme som party i url query parameter)

// For å slå opp i kundelista til eieren, bruk dette oppslaget:
//https://platform.at22.altinn.cloud/register/api/v1/internal/parties/cc90e65a-1fa9-4631-8d6e-384cd144317d/customers/ccr/revisor
// party-uuid er uuid til organisasjonen som approver systembrukeren (agent)

public class ClientDelegationTests
{
    private const string AccesspackageRevisor = "urn:altinn:accesspackage:revisormedarbeider";
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
        // Prepare
        var maskinportenToken = await _platformClient.GetMaskinportenTokenForVendor();
        var externalRef = Guid.NewGuid().ToString();
        var clientId = Guid.NewGuid().ToString();

        var systemOwner = _platformClient.GetTestUserForVendor();

        var testState = new TestState("Resources/Testdata/ClientDelegation/AccessPackageSystemRegister.json")
            .WithClientId(clientId)
            .WithVendor(systemOwner.Org)
            .WithAccessPackage("skattnaering")
            .WithToken(maskinportenToken);

        var requestBodySystemRegister = testState.GenerateRequestBody();
        var response = await _systemRegisterClient.PostSystem(requestBodySystemRegister, maskinportenToken);
        Assert.True(response.IsSuccessStatusCode, response.ReasonPhrase);

        // Prepare system user request for Vendor / Visma
        var requestBody = (await Helper.ReadFile("Resources/Testdata/ClientDelegation/CreateRequest.json"))
            .Replace("{systemId}", testState.SystemId)
            .Replace("{accessPackage}", AccesspackageRevisor)
            .Replace("{externalRef}", externalRef)
            .Replace("{partyOrgNo}", systemOwner.Org);

        // Act
        var userResponse = await _platformClient.PostAsync(ApiEndpoints.PostAgentClientRequest.Url(), requestBody, maskinportenToken);

        // Assert
        var content = await userResponse.Content.ReadAsStringAsync();

        Assert.True(userResponse.StatusCode == HttpStatusCode.Created, $"Unexpected status code: {userResponse.StatusCode} - {content}");

        var requestId = Common.ExtractPropertyFromJson(content, "id");

        //Assert status is new
        await AssertStatusSystemUserRequest(requestId, "New", maskinportenToken);

        var agentUser = await _common.GetSystemUserForVendorAgent(testState.SystemId, maskinportenToken);
        Assert.NotNull(agentUser);
        Assert.Contains(testState.SystemId, await agentUser.ReadAsStringAsync());

        var agentUserResponse = await agentUser.ReadAsStringAsync();

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

        //Kjør delegation
        // customerId
        // Her må customerId være en faktisk kunde, som ideelt sett må / bør hentes ut fra Register sitt API, i fremtiden kanskje Authentication.
        var customer = _platformClient.FindTestUserByRole("Customer");

        var requestBodyDelegation = JsonSerializer.Serialize(new
        {
            customerId = customer.AltinnPartyUuid,
            facilitatorId = systemOwner.AltinnPartyUuid
        });

        var systemUserId = Common.ExtractPropertyFromJson(agentUserResponse, "id");
        await PerformDelegation(requestBodyDelegation, customer, systemUserId);
    }

    
    // Fasilitatoren = partyid til partyorg for client (agent) request. 
    // org til system id = kan f.eks være Visma
    // Tilgangspakker foreløpig: https://github.com/Altinn/altinn-authentication/blob/main/src/Integration/MockData/packages.json

    // Delegation request
    // customerId : kundens party UUID
    // facilitatorId: party UUID til party som eier systembrukeren (samme som party i url query parameter)

    // For å slå opp i kundelista til eieren, bruk dette oppslaget:
    //https://platform.at22.altinn.cloud/register/api/v1/internal/parties/cc90e65a-1fa9-4631-8d6e-384cd144317d/customers/ccr/revisor
    // party-uuid er uuid til organisasjonen som approver systembrukeren (agent)
    private async Task PerformDelegation(string requestBodyDelegation, Testuser customer, string systemUserId)
    {
        // [EndpointInfo("/authentication/api/v1/systemuser/agent/{customerPartyId}/{systemUserId}/delegation", "POST")]
        var token = await _platformClient.GetPersonalAltinnToken(customer);
        var url = ApiEndpoints.DelegationAgentRequest.Url()
                .Replace("{customerPartyId}", customer.AltinnPartyUuid)
                .Replace("{systemUserId}", systemUserId);
            ;
        
        var responsDelegation = await _platformClient.PostAsync(url, requestBodyDelegation, token);
        _outputHelper.WriteLine($"Delegation response: {await responsDelegation.Content.ReadAsStringAsync()}");
        Assert.Equal(HttpStatusCode.OK, responsDelegation.StatusCode);
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