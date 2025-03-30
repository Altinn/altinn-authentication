using System.Net;
using System.Text.Json;
using Altinn.Platform.Authentication.SystemIntegrationTests.Clients;
using Altinn.Platform.Authentication.SystemIntegrationTests.Domain;
using Altinn.Platform.Authentication.SystemIntegrationTests.Utils;
using Xunit;
using Xunit.Abstractions;

namespace Altinn.Platform.Authentication.SystemIntegrationTests.Tests.ClientDelegation;

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
    public async Task CreateSystemUserClientRequestTest()
    {
        // Formål:
        // Denne testen dekker brukstilfeller der en sluttbruker (f.eks. et selskap) engasjerer en "fasilitator" – regnskapsfører eller revisor –
        // til å utføre handlinger i Altinn på deres vegne. Det er behov for å opprette en systembruker og knytte riktige kunder til denne.
        //
        // I Altinn 2 skjer dette via GUI/Excel-opplasting. I Altinn 3 skal det gjøres via API.
        // Regnskapsfører og revisor hentes som roller fra Enhetsregisteret (REGN/REVI),
        // og daglig leder i slike virksomheter har rettigheter til å gjøre dette på vegne av kundene sine.
        
        const string accessPackage = "urn:altinn:accesspackage:revisormedarbeider";

        // Arrange
        var facilitator = _platformClient.GetTestUserWithCategory("facilitator");
        var systemId = await SetupAndApproveSystemUser(facilitator, "NothingToSeeHere", accessPackage);

        // Act: Delegate customer
        var allDelegations = await DelegateCustomerToSystemUser(facilitator, systemId, false);

        // Cleanup: Delete delegation(s)
        foreach (var delegation in allDelegations)
        {
            var deleteResponse = await _platformClient.DeleteDelegation(facilitator, delegation);
            Assert.True(deleteResponse.IsSuccessStatusCode, $"Failed to delete delegation {delegation.delegationId}");
        }

        var systemUser = await _common.GetSystemUserOnSystemIdForOrg(systemId, facilitator);

        // Delete System user
        var deleteAgentUserResponse = await _platformClient.DeleteAgentSystemUser(systemUser?.Id, facilitator);
        Assert.Equal(HttpStatusCode.OK, deleteAgentUserResponse.StatusCode);
    }
    

    [Fact(Skip = "Skip until you have some safe data that's never deleted, store for now until later (runs in AT22)")]
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


    private async Task<string> SetupAndApproveSystemUser(Testuser facilitator, string systemNamePrefix, string accessPackage)
    {
        var systemOwner = _platformClient.GetTestUserForVendor();
        var maskinportenToken = await _platformClient.GetMaskinportenTokenForVendor();
        var externalRef = Guid.NewGuid().ToString();

        var testState = new TestState("Resources/Testdata/ClientDelegation/AccessPackageSystemRegister.json")
            .WithVendor(systemOwner.Org)
            .WithName($"{systemNamePrefix} {Guid.NewGuid()}");

        var systemPayload = testState.GenerateRequestBody();
        await _systemRegisterClient.PostSystem(systemPayload, maskinportenToken);

        var clientRequestBody = (await Helper.ReadFile("Resources/Testdata/ClientDelegation/CreateRequest.json"))
            .Replace("{systemId}", testState.SystemId)
            .Replace("{externalRef}", externalRef)
            .Replace("{accessPackage}", accessPackage)
            .Replace("{facilitatorPartyOrgNo}", facilitator.Org);

        var userResponse = await _platformClient.PostAsync(ApiEndpoints.PostAgentClientRequest.Url(), clientRequestBody, maskinportenToken);
        var userResponseContent = await userResponse.Content.ReadAsStringAsync();
        Assert.True(userResponse.StatusCode == HttpStatusCode.Created, $"Unexpected status: {userResponse.StatusCode} - {userResponseContent}");

        var requestId = Common.ExtractPropertyFromJson(userResponseContent, "id");
        await AssertStatusSystemUserRequest(requestId, "New", maskinportenToken);

        var systemUserResponse = await _common.GetSystemUserForVendorAgent(testState.SystemId, maskinportenToken);
        Assert.NotNull(systemUserResponse);
        Assert.Contains(testState.SystemId, await systemUserResponse.ReadAsStringAsync());

        var approveUrl = ApiEndpoints.ApproveAgentRequest.Url()
            .Replace("{facilitatorPartyId}", facilitator.AltinnPartyId)
            .Replace("{requestId}", requestId);

        var approveResponse = await _common.ApproveRequest(approveUrl, facilitator);
        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);

        await AssertStatusSystemUserRequest(requestId, "Accepted", maskinportenToken);

        return testState.SystemId;
    }

    private async Task<List<DelegationResponseDto>> DelegateCustomerToSystemUser(Testuser facilitator, string systemId, bool allCustomers = false)
    {
        var systemUser = await _common.GetSystemUserOnSystemIdForOrg(systemId, facilitator);
        var customerListResp = await _platformClient.GetCustomerList(facilitator, systemUser?.Id, _outputHelper);
        var customerContent = await customerListResp.Content.ReadAsStringAsync();

        var customers = JsonSerializer.Deserialize<List<CustomerListDto>>(customerContent);
        Assert.NotNull(customers);
        Assert.NotEmpty(customers);

        var responses = new List<DelegationResponseDto>();

        var customersToDelegate = allCustomers ? customers : customers.Take(1);
        _outputHelper.WriteLine($"Found {customers.Count} customers");

        foreach (var customer in customersToDelegate)
        {
            _outputHelper.WriteLine($"Attempting to delegate to customer {customer.id}");
            var requestBody = JsonSerializer.Serialize(new
            {
                customerId = customer.id,
                facilitatorId = facilitator.AltinnPartyUuid
            });

            var delegationResponse = await _platformClient.DelegateFromAuthentication(facilitator, systemUser?.Id, requestBody, _outputHelper);
            Assert.NotNull(delegationResponse);
            Assert.Equal(HttpStatusCode.OK, delegationResponse.StatusCode);

            var delegationContent = await delegationResponse.Content.ReadAsStringAsync();
            var parsedList = JsonSerializer.Deserialize<List<DelegationResponseDto>>(delegationContent);
            var parsedDelegation = parsedList?.FirstOrDefault();

            Assert.NotNull(parsedDelegation);
            Assert.False(string.IsNullOrEmpty(parsedDelegation.agentSystemUserId));
            Assert.False(string.IsNullOrEmpty(parsedDelegation.delegationId));
            Assert.False(string.IsNullOrEmpty(parsedDelegation.customerId));
            Assert.False(string.IsNullOrEmpty(parsedDelegation.assignmentId));

            responses.Add(parsedDelegation);
        }

        return responses;
    }
}