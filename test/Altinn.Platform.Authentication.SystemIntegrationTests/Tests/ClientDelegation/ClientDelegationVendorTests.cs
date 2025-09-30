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
    private static readonly string[] value = new[] { "string" };
    
    public ClientDelegationVendorTests(ClientDelegationFixture fixture, ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;
        _fixture = fixture;
    }
    
    [Theory]
    // [InlineData("regnskapsforer-lonn", "NotApplicable", "facilitator-regn-og-revisor")]
    [InlineData("ansvarlig-revisor", "Permit", "facilitator-regn-og-revisor")]
    public async Task CreateSystemUserClientRequestTest(string accessPackage, string expectedDecision, string testCategory)
    {
        var externalRef = Guid.NewGuid().ToString();
        Testuser facilitator = await _fixture.Platform.GetTestUserAndTokenForCategory(testCategory);

        await _fixture.SetupAndApproveSystemUser(facilitator, accessPackage, externalRef);

        var systemUser =
            await _fixture.Platform.Common.GetSystemUserOnSystemIdForAgenOnOrg(_fixture.SystemId, facilitator,
                externalRef);

        var customers = await _fixture.Platform.SystemUserClient.GetAvailableClientsForVendor(facilitator, systemUser?.Id);
        _outputHelper.WriteLine(customers.Data.Count.ToString());;
        _outputHelper.WriteLine(customers.SystemUserInformation.SystemUserOwnerOrg);

        // Act: Delegate customer
        // List<DelegationResponseDto> allDelegations = await _fixture.Platform.SystemUserClient.DelegateCustomerToSystemUserAsVendor(facilitator, systemUser?.Id, customers);
        //
        // // Verify decision end point to verify Rights given
        // var decision = await _fixture.Platform.AccessManagementClient.PerformDecision(systemUser?.Id, customers);
        // Assert.True(decision == expectedDecision, $"Decision was not {expectedDecision} but: {decision}");
        //
        // // Cleanup: Delete delegation(s)
        // await _fixture.Platform.SystemUserClient.RemoveDelegations(allDelegations, facilitator);
        //
        // // Verify maskinporten endpoint, externalRef set to name of access package
        // await _fixture.Platform.Common.GetTokenForSystemUser(_fixture.ClientId, facilitator.Org, externalRef);
        //
        // // Delete System user and System
        // var deleteAgentUserResponse =
        //     await _fixture.Platform.SystemUserClient.DeleteAgentSystemUser(systemUser?.Id, facilitator);
        // Assert.True(HttpStatusCode.OK == deleteAgentUserResponse.StatusCode,
        //     "Was unable to delete System User: Error code: " + deleteAgentUserResponse.StatusCode);
    }
}