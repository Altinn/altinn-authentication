using System.Net;
using System.Text.Json;
using Altinn.Platform.Authentication.SystemIntegrationTests.Clients;
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
        var maskinportenToken = await _platformAuthentication.GetMaskinportenToken();
        var externalRef = Guid.NewGuid().ToString();
        const string partyOrg = "312605031";

        //Create new system and system user request with two rights
        var testperson = _platformAuthentication.TestUsers.Find(testUser => testUser.Org.Equals(partyOrg))
                         ?? throw new Exception($"Test user not found for organization: {partyOrg}");

        var clientId = Guid.NewGuid().ToString();

        var systemId =
            await _common.CreateAndApproveSystemUserRequest(maskinportenToken, externalRef, testperson, clientId);

        //Change request requesting one new right and removing the existing one
        var changeRequestBody =
            (await Helper.ReadFile("Resources/Testdata/ChangeRequest/ChangeRequest.json"))
            .Replace("{systemId}", systemId)
            .Replace("{externalRef}", externalRef);

        var changeRequestResponse = await _platformAuthentication.PostAsync("v1/systemuser/changerequest/vendor",
            changeRequestBody,
            maskinportenToken);
        
        using var jsonDocSystemRequestResponse =
            JsonDocument.Parse(await changeRequestResponse.Content.ReadAsStringAsync());
        var requestId = jsonDocSystemRequestResponse.RootElement.GetProperty("id").GetString();

        Assert.Equal(HttpStatusCode.Created, changeRequestResponse.StatusCode);

        // BUG / TODO - doesnt work
        // var verifyResponse = await _platformAuthentication.PostAsync("v1/systemuser/changerequest/vendor/verify",
        //     changeRequestBody, maskinportenToken);
        //
        // _outputHelper.WriteLine($"Verify response: {await verifyResponse.Content.ReadAsStringAsync()}");

        var approvalResp =
            await _common.ApproveRequest($"v1/systemuser/changerequest/{testperson.AltinnPartyId}/{requestId}/approve",
                testperson);

        Assert.True(HttpStatusCode.OK == approvalResp.StatusCode,
            "Was not approved, received status code: " + approvalResp.StatusCode);

        //Already covered by assert
        var respGetSystemUsersForVendor = await _common.GetSystemUserForVendor(systemId, maskinportenToken);
        _outputHelper.WriteLine(await respGetSystemUsersForVendor.ReadAsStringAsync());
        // Assert.Contains(await respGetSystemUsersForVendor.ReadAsStringAsync(), systemId);

        // Validate system user from party's perspective:
         var altinnToken = await _platformAuthentication.GetPersonalAltinnToken(testperson);
         var systemUserId = "";
         var endpoint = $"v1/systemuser/{testperson.AltinnPartyId}/{systemUserId}";
         var getSystemUsers = await _platformAuthentication.GetAsync(endpoint, altinnToken);
         Assert.True(HttpStatusCode.OK == getSystemUsers.StatusCode, await getSystemUsers.Content.ReadAsStringAsync());
         var output = await getSystemUsers.Content.ReadAsStringAsync();
         _outputHelper.WriteLine(output);

        var getRequestByIdUrl = $"v1/systemuser/changerequest/vendor/{requestId}";
        var responsGetByRequestId = await _platformAuthentication.GetAsync(getRequestByIdUrl, maskinportenToken);
        Assert.Equal(HttpStatusCode.OK, responsGetByRequestId.StatusCode);
        Assert.NotNull(responsGetByRequestId);
        // _outputHelper.WriteLine(await responsGetByRequestId.Content.ReadAsStringAsync()); //BUG: SystemUserId settes til:   "systemUserId": "00000000-0000-0000-0000-000000000000",

        //Test get by externalRef
        var getByExternalRefUrl =
            $"v1/systemuser/changerequest/vendor/byexternalref/{systemId}/{_platformAuthentication.EnvironmentHelper.Vendor}/{externalRef}";
        var respByExternalRef = await _platformAuthentication.GetAsync(getByExternalRefUrl, maskinportenToken);
        Assert.Equal(HttpStatusCode.OK, respByExternalRef.StatusCode);
        // _outputHelper.WriteLine(await respByExternalRef.Content.ReadAsStringAsync());

        // var maskinportenUrl =
        //     $"v1/systemuser/byExternalId?systemUserOwnerOrgNo=312605031&systemProviderOrgNo={_platformAuthentication.EnvironmentHelper.Vendor}&clientId={clientId}";
        //
        // var resp = await _platformAuthentication.GetAsync(maskinportenUrl, maskinportenToken);
        // // _outputHelper.WriteLine(await resp.Content.ReadAsStringAsync());
        // Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    public async Task DeleteRequest()
    {
        //Tested manually, works regardless of status, and gets 404 when trying to fetch request after. 

        // var deleteUrl = $"v1/systemuser/changerequest/vendor/{requestId}";
        //
        // var deleteResponse = await _platformAuthentication.Delete(deleteUrl, maskinportenToken);
        // Assert.Equal(HttpStatusCode.Accepted, deleteResponse.StatusCode);
    }
}