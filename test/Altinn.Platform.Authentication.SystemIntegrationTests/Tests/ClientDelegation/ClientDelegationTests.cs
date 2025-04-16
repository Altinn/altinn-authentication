using System.Net;
using System.Text.Json;
using Altinn.Platform.Authentication.SystemIntegrationTests.Clients;
using Altinn.Platform.Authentication.SystemIntegrationTests.Domain;
using Altinn.Platform.Authentication.SystemIntegrationTests.Domain.Authorization;
using Altinn.Platform.Authentication.SystemIntegrationTests.Utils;
using Xunit;
using Xunit.Abstractions;

namespace Altinn.Platform.Authentication.SystemIntegrationTests.Tests.ClientDelegation;

public class ClientDelegationTests : IDisposable
{
    private readonly ITestOutputHelper _outputHelper;
    private readonly PlatformAuthenticationClient _platformClient;
    private readonly SystemRegisterClient _systemRegisterClient;
    private readonly AccessManagementClient _accessManagementClient;
    private readonly SystemUserClient _systemUserClient;
    private readonly Common _common;

    public ClientDelegationTests(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;
        _platformClient = new PlatformAuthenticationClient();
        _accessManagementClient = new AccessManagementClient(_platformClient);
        _systemRegisterClient = new SystemRegisterClient(_platformClient);
        _systemUserClient = new SystemUserClient(_platformClient);
        _common = new Common(_platformClient, outputHelper, _systemRegisterClient, _systemUserClient);
    }

    /// <summary>
    /// Verifies the outcome of delegating a system user with specific access packages
    /// and checks whether the decision endpoint returns the expected result ("Permit" or "NotApplicable").
    ///
    /// This test simulates a scenario where a facilitator (e.g., BDO) is granted
    /// access through different access packages, and a system user is created accordingly.
    ///
    /// The expected decision outcome is based on configuration in the resource registry:
    /// - Some packages (e.g. ansvarlig-revisor) result in "Permit"
    /// - Other packages result in "NotApplicable" if access is not valid for the facilitator's relation
    /// </summary>
    [Theory]
    [InlineData("urn:altinn:accesspackage:regnskapsforer-lonn", "NotApplicable")]
   // [InlineData("urn:altinn:accesspackage:ansvarlig-revisor", "Permit")]
   // [InlineData("urn:altinn:accesspackage:regnskapsforer-med-signeringsrettighet", "NotApplicable")]
   // [InlineData("urn:altinn:accesspackage:regnskapsforer-uten-signeringsrettighet", "NotApplicable")]
   // [InlineData("urn:altinn:accesspackage:revisormedarbeider", "NotApplicable")]
    public async Task CreateSystemUserClientRequestTest(string accessPackage, string expectedDecision)
    {
        var facilitator = _platformClient.GetTestUserWithCategory("facilitator");
        facilitator.AltinnToken = await _platformClient.GetPersonalAltinnToken(facilitator);

        var teststate = await SetupAndApproveSystemUser(facilitator, "TripleTexSuperPackage " + accessPackage, accessPackage);
        var systemUser = await _common.GetSystemUserOnSystemIdForAgenOnOrg(teststate.SystemId, facilitator);
        var customers = await GetCustomers(facilitator, systemUser?.Id, false);

        // Act: Delegate customer
        var allDelegations = await DelegateCustomerToSystemUser(facilitator, systemUser?.Id, customers);

        // Verify decision end point to verify Rights given
        var decision = await PerformDecision(facilitator, systemUser?.Id, customers);
        Assert.True(decision == expectedDecision, $"Decision was not permit but: {decision}");

        // Cleanup: Delete delegation(s)
        await RemoveDelegations(allDelegations, facilitator);

        await _common.GetTokenForSystemUser(teststate.ClientId, facilitator.Org, teststate.ExternalRef);

        // Delete System user
        var deleteAgentUserResponse = await _platformClient.DeleteAgentSystemUser(systemUser?.Id, facilitator);
        Assert.True(HttpStatusCode.OK == deleteAgentUserResponse.StatusCode, "Was unable to delete System User: Error code: " + deleteAgentUserResponse.StatusCode);
        await _common.DeleteSystem(teststate.SystemId, teststate.Token);
    }

    private async Task RemoveDelegations(List<DelegationResponseDto> allDelegations, Testuser facilitator)
    {
        foreach (var delegation in allDelegations)
        {
            var deleteResponse = await _platformClient.DeleteDelegation(facilitator, delegation);
            Assert.True(deleteResponse.IsSuccessStatusCode, $"Failed to delete delegation {delegation.delegationId}");
        }
    }

    private async Task<string?> PerformDecision(Testuser facilitator, string? systemUserId, List<CustomerListDto> customers)
    {
        //klientdelegeringsressurs med revisorpakke definert i ressursregisteret: "klientdelegeringressurse2e"
        var requestBody = (await Helper.ReadFile("Resources/Testdata/AccessManagement/systemUserDecision.json"))
            .Replace("{customerOrgNo}", customers.First().orgNo)
            .Replace("{subjectSystemUser}", systemUserId)
            .Replace("{ResourceId}", "klientdelegeringressurse2e");

        var response = await _accessManagementClient.PostDecision(requestBody, facilitator.AltinnToken);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Decision endpoint failed with: {response.StatusCode}");

        var json = await response.Content.ReadAsStringAsync();
        var dto = JsonSerializer.Deserialize<DecisionResponseDto>(json);
        return dto?.Response.FirstOrDefault()?.Decision;
    }

    private async Task<List<CustomerListDto>> GetCustomers(Testuser facilitator, string? systemUserId, bool allCustomers = false)
    {
        var customerListResp = await _platformClient.GetCustomerList(facilitator, systemUserId, _outputHelper);

        Assert.True(customerListResp.StatusCode == HttpStatusCode.OK, $"Unable to get customer list, returned status code: {customerListResp.StatusCode} for system: {systemUserId}");

        var customerContent = await customerListResp.Content.ReadAsStringAsync();
        var customers = JsonSerializer.Deserialize<List<CustomerListDto>>(customerContent);
        Assert.NotNull(customers);
        Assert.True(customers.Count > 0, $"Found no customers for systemuser with Id {systemUserId}");

        var customersToDelegate = allCustomers ? customers : customers.Take(1).ToList();
        return customersToDelegate;
    }

    private async Task AssertStatusSystemUserRequest(string requestId, string expectedStatus, string? maskinportenToken)
    {
        var getRequestByIdUrl = ApiEndpoints.GetVendorAgentRequestById.Url().Replace("{requestId}", requestId);
        var responseGetByRequestId = await _platformClient.GetAsync(getRequestByIdUrl, maskinportenToken);

        Assert.True(HttpStatusCode.OK == responseGetByRequestId.StatusCode);
        Assert.Contains(requestId, await responseGetByRequestId.Content.ReadAsStringAsync());

        var status = Common.ExtractPropertyFromJson(await responseGetByRequestId.Content.ReadAsStringAsync(), "status");
        Assert.True(expectedStatus.Equals(status), $"Status is not {expectedStatus} but: {status}");
    }

    private async Task AssertSystemUserAgentCreated(string systemId, string externalRef, string? maskinportenToken)
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


    private async Task<TestState> SetupAndApproveSystemUser(Testuser facilitator, string systemNamePrefix, string accessPackage)
    {
        var systemOwner = _platformClient.GetTestUserForVendor();
        var maskinportenToken = await _platformClient.GetMaskinportenTokenForVendor();
        var externalRef = Guid.NewGuid().ToString();

        var testState = new TestState("Resources/Testdata/ClientDelegation/AccessPackageSystemRegister.json")
            .WithVendor(systemOwner.Org)
            .WithClientId(externalRef)
            .WithExternalRef(externalRef)
            .WithToken(maskinportenToken)
            .WithName($"{systemNamePrefix}-{Guid.NewGuid()}");

        var systemPayload = testState.GenerateRequestBody();
        await _common.PostSystem(systemPayload, maskinportenToken);

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

        return testState;
    }

    private async Task<List<DelegationResponseDto>> DelegateCustomerToSystemUser(Testuser facilitator, string? systemUserId, List<CustomerListDto> customersToDelegate)
    {
        var responses = new List<DelegationResponseDto>();

        foreach (var customer in customersToDelegate)
        {
            var requestBody = JsonSerializer.Serialize(new
            {
                customerId = customer.id,
                facilitatorId = facilitator.AltinnPartyUuid
            });

            var delegationResponse = await _platformClient.DelegateFromAuthentication(facilitator, systemUserId, requestBody, _outputHelper);
            Assert.NotNull(delegationResponse);
            Assert.Equal(HttpStatusCode.OK, delegationResponse.StatusCode);

            var delegationContent = await delegationResponse.Content.ReadAsStringAsync();
            var parsedList = JsonSerializer.Deserialize<List<DelegationResponseDto>>(delegationContent);
            var parsedDelegation = parsedList?.FirstOrDefault();
            Assert.NotNull(parsedDelegation);
            responses.Add(parsedDelegation);
        }

        return responses;
    }

    public void Dispose()
    {
        //clean up even if test fails. Delete System User and System in System Register
    }
}