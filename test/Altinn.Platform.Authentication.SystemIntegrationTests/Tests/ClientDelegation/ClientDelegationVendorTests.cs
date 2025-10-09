using System.Diagnostics;
using System.Net;
using Altinn.Platform.Authentication.SystemIntegrationTests.Domain;
using Altinn.Platform.Authentication.SystemIntegrationTests.Domain.VendorClientDelegation;
using Altinn.Platform.Authentication.SystemIntegrationTests.Utils.TestSetup;
using Xunit;
using Xunit.Abstractions;

namespace Altinn.Platform.Authentication.SystemIntegrationTests.Tests.ClientDelegation;

public class ClientDelegationVendorTests : IClassFixture<ClientDelegationFixture>
{
    private readonly ITestOutputHelper _outputHelper;
    private readonly ClientDelegationFixture _fixture;

    public ClientDelegationVendorTests(ClientDelegationFixture fixture, ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;
        _fixture = fixture;
    }

    [Theory]
    [InlineData("ansvarlig-revisor", "Permit", "facilitator-regn-og-revisor")]
    [InlineData("regnskapsforer-lonn", "NotApplicable", "facilitator-regn-og-revisor")]
    [InlineData("forretningsforer-eiendom", "NotApplicable", "facilitator-forretningsfoerer")]
    [InlineData("regnskapsforer-med-signeringsrettighet", "NotApplicable", "facilitator-regn-og-revisor")]
    [InlineData("regnskapsforer-uten-signeringsrettighet", "NotApplicable", "facilitator-regn-og-revisor")]
    [InlineData("revisormedarbeider", "NotApplicable", "facilitator-regn-og-revisor")]
    public async Task CreateSystemUserClientRequestTest(string accessPackage, string expectedDecision, string testCategory)
    {
        var externalRef = Guid.NewGuid().ToString();
        _fixture.Facilitator = await _fixture.Platform.GetTestUserAndTokenForCategory(testCategory);

        await _fixture.SetupAndApproveSystemUser(_fixture.Facilitator, accessPackage, externalRef);

        // Verify system user agent is created
        List<SystemUserAgentDto> agent = await _fixture.Platform.SystemUserClient.GetSystemUserAgents(_fixture.Facilitator);
        var systemUserExist = agent.Exists(systemUserAgentDto => systemUserAgentDto.ExternalRef == externalRef);
        Assert.True(systemUserExist);

        var systemUser = await _fixture.Platform.Common.GetSystemUserOnSystemIdForAgenOnOrg(_fixture.SystemId, _fixture.Facilitator, externalRef);
        _fixture.SystemUserId = systemUser?.Id;

        // Get available customers
        var customers = await _fixture.Platform.SystemUserClient.GetAvailableClientsForVendor(_fixture.Facilitator, systemUser?.Id);
        Assert.True(customers.Data is not null);

        List<ClientInfoDto> clients = customers.Data.Take(3).ToList();

        // Delegate all clients to System User
        await _fixture.Platform.SystemUserClient.DelegateAllClientsFromVendorToSystemUser(_fixture.Facilitator, systemUser?.Id, clients);

        //Get delegated clients - works but verify you get clients
        ClientsForDelegationResponseDto? delegatedClients = await _fixture.Platform.SystemUserClient.GetDelegatedClientsFromVendorSystemUser(_fixture.Facilitator, systemUser?.Id);

        // Verify decision end point to verify Rights given
        var decision = await _fixture.PerformDecision(systemUser?.Id, clients.First().ClientOrganizationNumber);
        Assert.Equal(expectedDecision, decision);

        await _fixture.Platform.SystemUserClient.DeleteAllClientsFromVendorSystemUser(_fixture.Facilitator, systemUser?.Id, delegatedClients?.Data);
    }

    [Fact]
    public async Task DecisionFalse()
    {
        // Arrange
        const string accessPackage = "ansvarlig-revisor";
        const string testCategory = "facilitator-regn-og-revisor";
        var externalRef = Guid.NewGuid().ToString();

        _fixture.Facilitator = await _fixture.Platform.GetTestUserAndTokenForCategory(testCategory);

        // Create and approve a system user
        await _fixture.SetupAndApproveSystemUser(_fixture.Facilitator , accessPackage, externalRef);

        var systemUser = await _fixture.Platform.Common
            .GetSystemUserOnSystemIdForAgenOnOrg(_fixture.SystemId, _fixture.Facilitator , externalRef);
        
        Assert.NotNull(systemUser);

        // Get available clients (limit to 1 for simplicity)
        var available = await _fixture.Platform.SystemUserClient.GetAvailableClientsForVendor(_fixture.Facilitator , systemUser.Id);
        Assert.NotNull(available.Data);
        
        //Pick just one client
        var client = available.Data!.First();

        // Delegate one client
        await _fixture.Platform.SystemUserClient.DelegateAllClientsFromVendorToSystemUser(_fixture.Facilitator , systemUser.Id, [client]);

        // Verify decision = Permit
        var decisionBeforeDelete = await _fixture.PerformDecision(systemUser.Id, client.ClientOrganizationNumber);
        Assert.Equal("Permit", decisionBeforeDelete);

        // Get delegated clients
        var delegated = await _fixture.Platform.SystemUserClient
            .GetDelegatedClientsFromVendorSystemUser(_fixture.Facilitator , systemUser.Id);
        Assert.NotNull(delegated?.Data);

        // Act: delete all delegated clients
        await _fixture.Platform.SystemUserClient.DeleteAllClientsFromVendorSystemUser(_fixture.Facilitator , systemUser.Id, delegated.Data);

        // Assert: verify decision = NotApplicable (false permission)
        var decisionAfterDelete = await _fixture.PerformDecision(systemUser.Id, client.ClientOrganizationNumber);
        Assert.Equal("NotApplicable", decisionAfterDelete);
    }
}