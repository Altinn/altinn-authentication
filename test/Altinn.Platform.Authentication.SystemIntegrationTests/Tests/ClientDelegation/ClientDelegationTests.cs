using System.Net;
using System.Text.Json;
using Altinn.Platform.Authentication.SystemIntegrationTests.Clients;
using Altinn.Platform.Authentication.SystemIntegrationTests.Domain;
using Altinn.Platform.Authentication.SystemIntegrationTests.Utils;
using Xunit;
using Xunit.Abstractions;

namespace Altinn.Platform.Authentication.SystemIntegrationTests.Tests.ClientDelegation;

// Tilgangspakker: https://platform.at22.altinn.cloud/accessmanagement/api/v1/meta/info/accesspackages/export

// Delegation request
// customerId : kundens party UUID
// facilitatorId: party UUID til party som eier systembrukeren (samme som party i url query parameter)

// For å slå opp i kundelista til eieren, bruk dette oppslaget:
//https://platform.at22.altinn.cloud/register/api/v1/internal/parties/cc90e65a-1fa9-4631-8d6e-384cd144317d/customers/ccr/revisor
// party-uuid er uuid til organisasjonen som approver systembrukeren (agent)

// https://github.com/Altinn/altinn-authentication/issues/548
public class ClientDelegationTests
{
    //private const string AccessPackage = "urn:altinn:accesspackage:regnskapsforer-uten-signeringsrettighet";
    // private const string AccessPackage = "urn:altinn:accesspackage:skattegrunnlag";
    //private const string AccessPackage = "urn:altinn:accesspackage:ansvarlig-revisor";
    private const string AccessPackage = "urn:altinn:accesspackage:revisormedarbeider";


    private readonly ITestOutputHelper _outputHelper;

    private readonly PlatformAuthenticationClient _platformClient;
    private readonly SystemRegisterClient _systemRegisterClient;
    private readonly SystemUserClient _systemUserClient;
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
        _systemUserClient = new SystemUserClient(_platformClient);
        _common = new Common(_platformClient, outputHelper);
    }

    [Fact]
    public async Task CreateClientRequest()
    {
        // Fasilitator - Virksomhet som utfører tjenester på vegne av annen virksomhet (tidligere omtalt som hjelper).
        // Når fasilitator går inn på en systembruker skal han kunne videredelegere kunder som har delegert samme tilgangspakke som systembruker er satt opp med
        var facilitator = _platformClient.GetTestUserWithCategory("facilitator");
        var maskinportenToken = await _platformClient.GetMaskinportenTokenForVendor();
        var clientId = Guid.NewGuid().ToString();

        _outputHelper.WriteLine($"Client Id: {clientId}");

        var systemOwner = _platformClient.GetTestUserForVendor();

        var testState = new TestState("Resources/Testdata/ClientDelegation/AccessPackageSystemRegister.json")
            .WithClientId(clientId)
            .WithVendor(systemOwner.Org)
            .WithName("MoreDebug " + Guid.NewGuid())
            .WithToken(maskinportenToken);

        //Post system med én pakke for Bedrift "BDO". 
        var requestBodySystemRegister = testState.GenerateRequestBody();
        await _systemRegisterClient.PostSystem(requestBodySystemRegister, maskinportenToken);

        // Prepare system user request
        var externalRef = Guid.NewGuid().ToString();

        var requestBody = (await Helper.ReadFile("Resources/Testdata/ClientDelegation/CreateRequest.json"))
            .Replace("{systemId}", testState.SystemId)
            .Replace("{externalRef}", externalRef)
            .Replace("{accessPackage}", AccessPackage)
            .Replace("{facilitatorPartyOrgNo}", facilitator.Org);

        // Act
        var userResponse = await _platformClient.PostAsync(ApiEndpoints.PostAgentClientRequest.Url(), requestBody, maskinportenToken);

        // Assert
        var content = await userResponse.Content.ReadAsStringAsync();
        Assert.True(userResponse.StatusCode == HttpStatusCode.Created, $"Unexpected status code: {userResponse.StatusCode} - " +
                                                                       $"{content} for attempted request body:" + requestBody);

        var requestId = Common.ExtractPropertyFromJson(content, "id");

        //Assert status is new
        await AssertStatusSystemUserRequest(requestId, "New", maskinportenToken);

        var agentUser = await _common.GetSystemUserForVendorAgent(testState.SystemId, maskinportenToken);
        Assert.NotNull(agentUser);
        Assert.Contains(testState.SystemId, await agentUser.ReadAsStringAsync());
        
        var approveUrl = ApiEndpoints.ApproveAgentRequest.Url()
            .Replace("{facilitatorPartyId}", facilitator.AltinnPartyId)
            .Replace("{requestId}", requestId);

        var approveResp =
            await _common.ApproveRequest(approveUrl, facilitator);

        Assert.True(HttpStatusCode.OK == approveResp.StatusCode,
            "Received status code " + approveResp.StatusCode + "when attempting to approve");

        await AssertStatusSystemUserRequest(requestId, "Accepted", maskinportenToken);

        var systemuser = await _common.GetSystemUserOnSystemIdForOrg(testState.SystemId, facilitator);

        var customerList = await _platformClient.GetCustomerList(facilitator, systemuser?.Id,_outputHelper);
        var customers = JsonSerializer.Deserialize<List<CustomerListDto>>(await customerList.Content.ReadAsStringAsync());

        var requestBodyDelegation = JsonSerializer.Serialize(new
        {
            customerId = customers?.Last().id,
            facilitatorId = facilitator.AltinnPartyUuid
        });
        
        var resp = await _platformClient.DelegateFromAuthentication(facilitator, systemuser?.Id, requestBodyDelegation, _outputHelper);
        Assert.NotNull(resp);
        Assert.True(resp.StatusCode == HttpStatusCode.OK);
    }

    [Fact]
    public async Task DelegateClientTest()
    {
        var facilitator = _platformClient.GetTestUserWithCategory("debug");

        const string? systemUserId = "78622673-e991-4337-b81e-4005dae8707b";

        var requestBodyDelegation = JsonSerializer.Serialize(new
        {
            customerId = "02cc9fee-6bf7-4dd1-8d37-a8af2045ee19",
            facilitatorId = facilitator.AltinnPartyUuid
        });

        //await PerformDelegation(requestBodyDelegation, systemUserId, facilitator);
        var resp = await _platformClient.DelegateFromAuthentication(facilitator, systemUserId, requestBodyDelegation, _outputHelper);

        Assert.True(resp.StatusCode == HttpStatusCode.OK, "Resp was: " + await resp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task GetTokenForFacilitatorReturnsOkTest()
    {
        //Only way to use this token is by using the "fake" altinn token service, not allowed to configure this in samarbeidsportalen
        const string scopes = "altinn:maskinporten/systemuser.read";
        const string clientId = "ebfa9b1f-ac36-4479-af1d-17d915c59fba"; // Stored in System register
        const string facilitatorOrgNo = "313588270";
        const string externalRef = "7ccd82c8-da69-4632-b0b8-850daf835262";

        var systemProviderOrgNo = _platformClient.EnvironmentHelper.Vendor;

        var altinnEnterpriseToken =
            await _platformClient.GetEnterpriseAltinnToken(systemProviderOrgNo, scopes);

        var queryString =
            $"?clientId={clientId}" +
            $"&systemProviderOrgNo={systemProviderOrgNo}" +
            $"&systemUserOwnerOrgNo={facilitatorOrgNo}" +
            $"&externalRef={externalRef}";

        var fullEndpoint = $"{ApiEndpoints.GetSystemUserByExternalId.Url()}{queryString}";

        var resp = await _platformClient.GetAsync(fullEndpoint, altinnEnterpriseToken);
        Assert.NotNull(resp);
        Assert.Equal(System.Net.HttpStatusCode.OK, resp.StatusCode);
    }

    private async Task AssertStatusSystemUserRequest(string requestId, string expectedStatus, string maskinportenToken)
    {
        var getRequestByIdUrl = ApiEndpoints.GetVendorAgentRequestById.Url().Replace("{requestId}", requestId);
        var responseGetByRequestId = await _platformClient.GetAsync(getRequestByIdUrl, maskinportenToken);

        Assert.True(HttpStatusCode.OK == responseGetByRequestId.StatusCode);
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