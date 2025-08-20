using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Altinn.Platform.Authentication.SystemIntegrationTests.Domain;
using Altinn.Platform.Authentication.SystemIntegrationTests.Domain.Authorization;
using Altinn.Platform.Authentication.SystemIntegrationTests.Utils;
using Altinn.Platform.Authentication.SystemIntegrationTests.Utils.ApiEndpoints;
using Altinn.Platform.Authentication.SystemIntegrationTests.Utils.TestSetup;
using Xunit;
using Xunit.Abstractions;

namespace Altinn.Platform.Authentication.SystemIntegrationTests.Tests.ClientDelegation;

public class ClientDelegationTests : IClassFixture<ClientDelegationFixture>
{
    private readonly ITestOutputHelper _outputHelper;
    private readonly ClientDelegationFixture _fixture;

    public ClientDelegationTests(ClientDelegationFixture fixture, ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;
        _fixture = fixture;
    }

    /// <summary>
    /// Verifies the outcome of delegating a system user with specific access packages
    /// and checks whether the decision endpoint returns the expected result ("Permit" or "NotApplicable").
    ///
    /// This test simulates a scenario where a facilitator (e.g. a person with Klientadministrator role at BDO) is granted
    /// access through different access packages, and a system user is created accordingly.
    /// It creates one common system in Systemregisteret in "ClientDelegationFixture", but creates different System user for the different test scenarios (although some could theoretically be reused) 
    /// 
    /// The expected decision outcome is based on configuration in the resource registry:
    /// - Some packages (e.g. ansvarlig-revisor) result in "Permit"
    /// - Other packages result in "NotApplicable" if access is not valid for the facilitator's relation
    /// </summary>
    [Theory]
    [InlineData("regnskapsforer-lonn", "NotApplicable", "facilitator-regn-og-revisor")]
    [InlineData("ansvarlig-revisor", "Permit", "facilitator-regn-og-revisor")]
    [InlineData("forretningsforer-eiendom", "NotApplicable", "facilitator-forretningsfoerer")]
    [InlineData("regnskapsforer-med-signeringsrettighet", "NotApplicable", "facilitator-regn-og-revisor")]
    [InlineData("regnskapsforer-uten-signeringsrettighet", "NotApplicable", "facilitator-regn-og-revisor")]
    [InlineData("revisormedarbeider", "NotApplicable", "facilitator-regn-og-revisor")]
    public async Task CreateSystemUserClientRequestTest(string accessPackage, string expectedDecision, string testCategory)
    {
        var externalRef = Guid.NewGuid().ToString();
        Testuser facilitator = await _fixture.Platform.GetTestUserAndTokenForCategory(testCategory);

        await SetupAndApproveSystemUser(facilitator, accessPackage, externalRef);

        var systemUser = await _fixture.Platform.Common.GetSystemUserOnSystemIdForAgenOnOrg(_fixture.SystemId, facilitator, externalRef);

        List<CustomerListDto> customers = await GetCustomers(facilitator, systemUser?.Id);

        // Act: Delegate customer
        List<DelegationResponseDto> allDelegations = await DelegateCustomerToSystemUser(facilitator, systemUser?.Id, customers);

        // Verify decision end point to verify Rights given
        var decision = await PerformDecision(systemUser?.Id, customers);
        Assert.True(decision == expectedDecision, $"Decision was not {expectedDecision} but: {decision}");

        // Cleanup: Delete delegation(s)
        await RemoveDelegations(allDelegations, facilitator);

        // Verify maskinporten endpoint, externalRef set to name of access package
        await _fixture.Platform.Common.GetTokenForSystemUser(_fixture.ClientId, facilitator.Org, externalRef);

        // Delete System user and System
        var deleteAgentUserResponse =
            await _fixture.Platform.DeleteAgentSystemUser(systemUser?.Id, facilitator);
        Assert.True(HttpStatusCode.OK == deleteAgentUserResponse.StatusCode,
            "Was unable to delete System User: Error code: " + deleteAgentUserResponse.StatusCode);
    }

    private async Task RemoveDelegations(List<DelegationResponseDto> allDelegations, Testuser facilitator)
    {
        foreach (var delegation in allDelegations)
        {
            var deleteResponse = await _fixture.Platform.DeleteDelegation(facilitator, delegation, _outputHelper);
            Assert.True(deleteResponse.IsSuccessStatusCode, $"Failed to delete delegation {delegation.delegationId}");
        }
    }

    private async Task<string?> PerformDecision(string? systemUserId, List<CustomerListDto> customers)
    {
        //klientdelegeringsressurs med revisorpakke definert i ressursregisteret: "klientdelegeringressurse2e"
        var requestBody = (await Helper.ReadFile("Resources/Testdata/AccessManagement/systemUserDecision.json"))
            .Replace("{customerOrgNo}", customers.First().orgNo)
            .Replace("{subjectSystemUser}", systemUserId)
            .Replace("{ResourceId}", "klientdelegeringressurse2e");

        var response =
            await _fixture.Platform.AccessManagementClient.PostDecision(requestBody);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Decision endpoint failed with: {response.StatusCode}");

        var json = await response.Content.ReadAsStringAsync();
        var dto = JsonSerializer.Deserialize<DecisionResponseDto>(json);
        Assert.True(dto?.Response != null, "Response is null for deserialization of decision");
        return dto.Response.FirstOrDefault()?.Decision;
    }

    private async Task<List<CustomerListDto>> GetCustomers(Testuser facilitator, string? systemUserId,
        bool allCustomers = false)
    {
        var customerListResp = await _fixture.Platform.GetCustomerList(facilitator, systemUserId, _outputHelper);

        Assert.True(customerListResp.StatusCode == HttpStatusCode.OK,
            $"Unable to get customer list, returned status code: {customerListResp.StatusCode} for system: {systemUserId}");

        var customerContent = await customerListResp.Content.ReadAsStringAsync();
        var customers = JsonSerializer.Deserialize<List<CustomerListDto>>(customerContent);

        Assert.NotNull(customers);
        Assert.True(customers.Count > 0, $"Found no customers for systemuser with Id {systemUserId}");

        Assert.True(customerListResp?.StatusCode == HttpStatusCode.OK,
            $"Unable to get customer list, returned status code: {customerListResp?.StatusCode} for system: {systemUserId}");

        Assert.NotNull(customers);
        Assert.True(customers.Count > 0, $"Found no customers for systemuser with Id {systemUserId}");

        List<CustomerListDto> customersToDelegate = allCustomers ? customers : customers.Take(1).ToList();
        return customersToDelegate;
    }

    private async Task AssertStatusSystemUserRequest(string requestId, string expectedStatus, string? maskinportenToken)
    {
        var getRequestByIdUrl = Endpoints.GetVendorAgentRequestById.Url().Replace("{requestId}", requestId);
        var responseGetByRequestId = await _fixture.Platform.GetAsync(getRequestByIdUrl, maskinportenToken);

        Assert.True(HttpStatusCode.OK == responseGetByRequestId.StatusCode);
        Assert.Contains(requestId, await responseGetByRequestId.Content.ReadAsStringAsync());

        var status = Common.ExtractPropertyFromJson(await responseGetByRequestId.Content.ReadAsStringAsync(), "status");
        Assert.True(expectedStatus.Equals(status), $"Status is not {expectedStatus} but: {status}");
    }

    private async Task AssertSystemUserAgentCreated(string systemId, string externalRef, string? maskinportenToken)
    {
        // Verify system user was updated // created (Does in fact not verify anything was updated, but easier to add in the future
        var respGetSystemUsersForVendor =
            await _fixture.Platform.Common.GetSystemUserForVendor(systemId, maskinportenToken);
        var systemusersRespons = await respGetSystemUsersForVendor.ReadAsStringAsync();

        // Assert systemId
        Assert.Contains(systemId, systemusersRespons);
        Assert.Contains(externalRef, systemusersRespons);
    }

    public async Task<HttpResponseMessage> GetVendorAgentRequestByExternalRef(string systemId, string orgNo,
        string externalRef, string maskinportenToken)
    {
        var url = Endpoints.GetVendorAgentRequestByExternalRef.Url()
            ?.Replace("{systemId}", systemId)
            .Replace("{orgNo}", orgNo)
            .Replace("{externalRef}", externalRef);

        return await _fixture.Platform.GetAsync(url, maskinportenToken);
    }


    private async Task SetupAndApproveSystemUser(Testuser facilitator, string accessPackage, string externalRef)
    {
        var clientRequestBody = (await Helper.ReadFile("Resources/Testdata/ClientDelegation/CreateRequest.json"))
            .Replace("{systemId}", _fixture.SystemId)
            .Replace("{externalRef}", externalRef)
            .Replace("{accessPackage}", accessPackage)
            .Replace("{facilitatorPartyOrgNo}", facilitator.Org);

        var userResponse = await _fixture.Platform.PostAsync(Endpoints.PostAgentClientRequest.Url(),
            clientRequestBody, _fixture.VendorTokenMaskinporten);

        var userResponseContent = await userResponse.Content.ReadAsStringAsync();
        Assert.True(userResponse.StatusCode == HttpStatusCode.Created,
            $"Unexpected status: {userResponse.StatusCode} - {userResponseContent}");

        var requestId = Common.ExtractPropertyFromJson(userResponseContent, "id");
        await AssertStatusSystemUserRequest(requestId, "New", _fixture.VendorTokenMaskinporten);

        var systemUserResponse =
            await _fixture.Platform.Common.GetSystemUserForVendorAgent(_fixture.SystemId,
                _fixture.VendorTokenMaskinporten);

        Assert.NotNull(systemUserResponse);
        Assert.Contains(_fixture.SystemId, await systemUserResponse.ReadAsStringAsync());

        var approveUrl = Endpoints.ApproveAgentRequest.Url()
            .Replace("{facilitatorPartyId}", facilitator.AltinnPartyId)
            .Replace("{requestId}", requestId);

        var approveResponse = await _fixture.Platform.Common.ApproveRequest(approveUrl, facilitator);
        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);

        await AssertStatusSystemUserRequest(requestId, "Accepted", _fixture.VendorTokenMaskinporten);
    }

    private async Task<List<DelegationResponseDto>> DelegateCustomerToSystemUser(Testuser facilitator, string? systemUserId, List<CustomerListDto> customersToDelegate)
    {
        List<DelegationResponseDto> responses = [];

        foreach (var customer in customersToDelegate)
        {
            var requestBody = JsonSerializer.Serialize(new
            {
                customerId = customer.id,
                facilitatorId = facilitator.AltinnPartyUuid,
                access = customer.access ?? []
            });

            var delegationResponse =
                await _fixture.Platform.DelegateFromAuthentication(facilitator, systemUserId, requestBody,
                    _outputHelper);
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
}