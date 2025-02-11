using System.Net;
using Altinn.Platform.Authentication.SystemIntegrationTests.Clients;
using Altinn.Platform.Authentication.SystemIntegrationTests.Domain;
using Altinn.Platform.Authentication.SystemIntegrationTests.Utils;
using Xunit;
using Xunit.Abstractions;

namespace Altinn.Platform.Authentication.SystemIntegrationTests.Tests;

/// <summary>
/// https://github.com/Altinn/altinn-authentication/issues/582
/// Make a request for changing rights on an existing system user
/// </summary>
///
[Trait("Category", "IntegrationTest")]
public class ChangeRequestTests
{
    private readonly ITestOutputHelper _outputHelper;
    private readonly Common _common;
    private readonly PlatformAuthenticationClient _platformAuthentication;

    public ChangeRequestTests(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;
        _platformAuthentication = new PlatformAuthenticationClient();
        _common = new Common(_platformAuthentication, outputHelper);
    }

    [Fact]
    public async Task MakeChangeRequestAsVendorTest()
    {
        // Prepare
        var maskinportenToken = await _platformAuthentication.GetMaskinportenTokenForVendor();
        var externalRef = Guid.NewGuid().ToString();
        var clientId = Guid.NewGuid().ToString();
        var testperson = GetTestUser();
        var systemId =
            await _common.CreateAndApproveSystemUserRequest(maskinportenToken, externalRef, testperson, clientId);

        // Act
        var changeRequestResponse = await SubmitChangeRequest(systemId, externalRef, maskinportenToken);

        Common.AssertSuccess(changeRequestResponse, "Change request submission failed");

        // Assert change request response
        Assert.Equal(HttpStatusCode.Created, changeRequestResponse.StatusCode);
        var requestId = Common.ExtractPropertyFromJson(await changeRequestResponse.Content.ReadAsStringAsync(), "id");
        await AssertStatusChangeRequest(requestId, "New", maskinportenToken);

        var approvalResponse = await ApproveChangeRequest(requestId, testperson);
        Common.AssertSuccess(approvalResponse, "Change request approval failed");

        // Assert relevant change request endpoints
        await AssertStatusChangeRequest(requestId, "Accepted", maskinportenToken);
        await AssertSystemUserUpdated(systemId, externalRef, maskinportenToken);
        await AssertRequestRetrievalById(requestId, systemId, externalRef, maskinportenToken);
        await AssertRequestRetrievalByExternalRef(systemId, externalRef, maskinportenToken);
    }

    private async Task AssertStatusChangeRequest(string requestId, string expectedStatus, string maskinportenToken)
    {
        var getRequestByIdUrl = UrlConstants.GetChangeRequestByRequestIdUrlTemplate.Replace("{requestId}", requestId);
        var responsGetByRequestId = await _platformAuthentication.GetAsync(getRequestByIdUrl, maskinportenToken);
        Assert.Equal(HttpStatusCode.OK, responsGetByRequestId.StatusCode);
        Assert.Contains(requestId, await responsGetByRequestId.Content.ReadAsStringAsync());

        var status = Common.ExtractPropertyFromJson(await responsGetByRequestId.Content.ReadAsStringAsync(), "status");
        Assert.True(expectedStatus.Equals(status), $"Status is not {expectedStatus} but: " + status);
    }

    private async Task AssertRequestRetrievalByExternalRef(string systemId, string externalRef,
        string maskinportenToken)
    {
        var getByExternalRefUrl = UrlConstants.GetByExternalRefUrlTemplate
            .Replace("{systemId}", systemId)
            .Replace("{vendor}", _platformAuthentication.EnvironmentHelper.Vendor)
            .Replace("{externalRef}", externalRef);

        var respByExternalRef = await _platformAuthentication.GetAsync(getByExternalRefUrl, maskinportenToken);
        Assert.Equal(HttpStatusCode.OK, respByExternalRef.StatusCode);
        Assert.Contains(externalRef, await respByExternalRef.Content.ReadAsStringAsync());
        Assert.Contains(systemId, await respByExternalRef.Content.ReadAsStringAsync());
    }

    private async Task AssertRequestRetrievalById(string requestId, string systemId, string externalRef,
        string maskinportenToken)
    {
        var getRequestByIdUrl = UrlConstants.GetRequestByIdUrlTemplate
            .Replace("{requestId}", requestId);
        var responsGetByRequestId = await _platformAuthentication.GetAsync(getRequestByIdUrl, maskinportenToken);
        Assert.Equal(HttpStatusCode.OK, responsGetByRequestId.StatusCode);
        Assert.Contains(systemId, await responsGetByRequestId.Content.ReadAsStringAsync());
        Assert.Contains(externalRef, await responsGetByRequestId.Content.ReadAsStringAsync());
    }

    private async Task AssertSystemUserUpdated(string systemId, string externalRef, string maskinportenToken)
    {
        // Verify system user was updated // created (Does in fact not verify anything was updated, but easier to add in the future
        var respGetSystemUsersForVendor = await _common.GetSystemUserForVendor(systemId, maskinportenToken);
        var systemusersRespons = await respGetSystemUsersForVendor.ReadAsStringAsync();

        // Assert systemId
        Assert.Contains(systemId, systemusersRespons);
        Assert.Contains(externalRef, systemusersRespons);
    }

    private async Task<HttpResponseMessage> ApproveChangeRequest(string requestId, Testuser testperson)
    {
        var approveUrl = UrlConstants.ApproveChangeRequestUrlTemplate
            .Replace("{partyId}", testperson.AltinnPartyId)
            .Replace("{requestId}", requestId);

        var approvalResp =
            await _common.ApproveRequest(approveUrl, testperson);

        return approvalResp;
    }

    private async Task<HttpResponseMessage> SubmitChangeRequest(string systemId, string externalRef,
        string maskinportenToken)
    {
        var changeRequestBody =
            (await Helper.ReadFile("Resources/Testdata/ChangeRequest/ChangeRequest.json"))
            .Replace("{systemId}", systemId)
            .Replace("{externalRef}", externalRef);

        var changeRequestResponse = await _platformAuthentication.PostAsync(UrlConstants.ChangeRequestVendorUrl,
            changeRequestBody,
            maskinportenToken);

        return changeRequestResponse;
    }

    private Testuser GetTestUser()
    {
        return _platformAuthentication.TestUsers.Find(testUser => testUser.Org.Equals(_platformAuthentication.EnvironmentHelper.Vendor))
               ?? throw new Exception($"Test user not found for organization: {_platformAuthentication.EnvironmentHelper.Vendor}");
    }
}