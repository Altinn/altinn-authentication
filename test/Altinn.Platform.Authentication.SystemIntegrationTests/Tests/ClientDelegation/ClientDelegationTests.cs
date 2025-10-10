using System.Net;
using Altinn.Platform.Authentication.SystemIntegrationTests.Domain;
using Altinn.Platform.Authentication.SystemIntegrationTests.Utils.TestSetup;
using Xunit;

namespace Altinn.Platform.Authentication.SystemIntegrationTests.Tests.ClientDelegation;

public class ClientDelegationTests : IClassFixture<ClientDelegationFixture>
{
    private readonly ClientDelegationFixture _fixture;

    public ClientDelegationTests(ClientDelegationFixture fixture)
    {
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

        var systemUser =
            await _fixture.Platform.Common.GetSystemUserOnSystemIdForAgenOnOrg(_fixture.SystemId, _fixture.Facilitator, externalRef);
        
        // Assert that system user was found
        Assert.True(systemUser is not null);
        
        List<CustomerListDto> customers = await _fixture.GetCustomers(_fixture.Facilitator, systemUser.Id);

        // Act: Delegate customer
        List<DelegationResponseDto> allDelegations = await _fixture.DelegateCustomerToSystemUser(_fixture.Facilitator, systemUser.Id, customers);
        
        // Perform decisiojn for each customer
        foreach (CustomerListDto customerListDto in customers)
        {
            var decision = await _fixture.PerformDecision(systemUser.Id, customerListDto.orgNo);
            Assert.True(decision == expectedDecision, $"Decision was not {expectedDecision} but: {decision}");
        }

        // Cleanup: Delete delegation(s)
        await _fixture.RemoveDelegations(allDelegations, _fixture.Facilitator);

        // Verify maskinporten endpoint, externalRef set to name of access package
        await _fixture.Platform.Common.GetTokenForSystemUser(_fixture.ClientId, _fixture.Facilitator?.Org, externalRef);
    }

   
}