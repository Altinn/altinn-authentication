using Altinn.Platform.Authentication.SystemIntegrationTests.Domain;
using Altinn.Platform.Authentication.SystemIntegrationTests.Utils.TestSetup;
using Xunit;
using Xunit.Abstractions;

namespace Altinn.Platform.Authentication.SystemIntegrationTests.Tests.ClientDelegation;

public class EnterpriseDelegationTests : IClassFixture<EnterpriseDelegationFixture>
{
    private readonly ITestOutputHelper _outputHelper;
    private readonly EnterpriseDelegationFixture _fixture;

    public EnterpriseDelegationTests(EnterpriseDelegationFixture fixture, ITestOutputHelper output)
    {
        _outputHelper = output;
        _fixture = fixture;
    }

    [Theory]
    [InlineData("akvakultur", "NotApplicable", "sneha")]
    public async Task CreateSystemUserClientRequestTest(string accessPackage, string expectedDecision, string testCategory)
    {
        var externalRef = Guid.NewGuid().ToString();
        Testuser facilitator = await _fixture.Platform.GetTestUserAndTokenForCategory(testCategory);
        await _fixture.Platform.SystemUserClient.SetupAndApproveSystemUser(facilitator, accessPackage, externalRef, _fixture.SystemId, _fixture.VendorTokenMaskinporten);
        
        _outputHelper.WriteLine($"Access package: {accessPackage}");
        _outputHelper.WriteLine($"SystemId {_fixture.SystemId}");
        _outputHelper.WriteLine($"Facilitator {_fixture.Platform.GetTestUserForVendor().Org}");
    }
}