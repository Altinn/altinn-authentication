using System.Net;
using Altinn.Platform.Authentication.SystemIntegrationTests.Domain;
using Altinn.Platform.Authentication.SystemIntegrationTests.Utils.TestSetup;
using Xunit;
using Xunit.Abstractions;

namespace Altinn.Platform.Authentication.SystemIntegrationTests.Tests.ClientDelegation;

public class ClientDelegationTests : IClassFixture<ClientDelegationFixture>
{
    private readonly ITestOutputHelper _outputHelper;
    private readonly ClientDelegationFixture _fixture;
    private static readonly string[] value = new[] { "string" };

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

        await _fixture.SetupAndApproveSystemUser(facilitator, accessPackage, externalRef);

        var systemUser =
            await _fixture.Platform.Common.GetSystemUserOnSystemIdForAgenOnOrg(_fixture.SystemId, facilitator,
                externalRef);

        List<CustomerListDto> customers = await _fixture.GetCustomers(facilitator, systemUser?.Id);

        // Act: Delegate customer
        List<DelegationResponseDto> allDelegations = await _fixture.DelegateCustomerToSystemUser(facilitator, systemUser?.Id, customers);

        // Verify decision end point to verify Rights given
        var decision = await _fixture.PerformDecision(systemUser?.Id, customers);
        Assert.True(decision == expectedDecision, $"Decision was not {expectedDecision} but: {decision}");

        // Cleanup: Delete delegation(s)
        await _fixture.RemoveDelegations(allDelegations, facilitator);

        // Verify maskinporten endpoint, externalRef set to name of access package
        await _fixture.Platform.Common.GetTokenForSystemUser(_fixture.ClientId, facilitator.Org, externalRef);

        // Delete System user and System
        var deleteAgentUserResponse =
            await _fixture.Platform.DeleteAgentSystemUser(systemUser?.Id, facilitator);
        Assert.True(HttpStatusCode.OK == deleteAgentUserResponse.StatusCode,
            "Was unable to delete System User: Error code: " + deleteAgentUserResponse.StatusCode);
    }

   
}