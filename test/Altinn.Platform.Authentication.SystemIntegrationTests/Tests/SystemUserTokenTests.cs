using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Altinn.Platform.Authentication.SystemIntegrationTests.Clients;
using Altinn.Platform.Authentication.SystemIntegrationTests.Domain;
using Altinn.Platform.Authentication.SystemIntegrationTests.Utils;
using Altinn.Platform.Authentication.SystemIntegrationTests.Utils.ApiEndpoints;
using Altinn.Platform.Authentication.SystemIntegrationTests.Utils.TestSetup;
using Xunit;
using Xunit.Abstractions;

namespace Altinn.Platform.Authentication.SystemIntegrationTests.Tests;

// Documentation: https://docs.digdir.no/docs/Maskinporten/maskinporten_func_systembruker
/* This won't work in AT22 due to maskinporten is only configured to use TT02 */

[Trait("Category", "IntegrationTest")]
public class SystemUserTokenTests : TestFixture
{
    private readonly ITestOutputHelper _outputHelper;

    private readonly PlatformAuthenticationClient _platformClient;

    // private const string SystemId = "312605031_Team-Authentication-SystemuserE2E-User-Do-Not-Delete";
    private const string SystemId = "312605031_Team-Authentication-SystemuserE2E-User-Do-Not-Delete"; //This works
    // private const string SystemId = "312605031_SystemRegister e2e Testsc6edcad1-1106-496d-8cc7-b626b16ac22c";
        
    // IntegrationTestNbTeam-Authentication-SystemuserE2E-User-Do-Not-Delete-TT02    

    /// <summary>
    /// Testing System user endpoints
    /// To find relevant api endpoints: https://docs.altinn.studio/nb/api/authentication/spec/#/RequestSystemUser
    /// </summary>
    ///
    public SystemUserTokenTests(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;
        _platformClient = new PlatformAuthenticationClient();
    }

    [SkipUnlessTt02Fact]
    public async Task GetByExternalIdMaskinporten()
    {
        // Setup
        var systemUser = await GetSystemUserOnSystemId(SystemId);

        //Only way to use this token is by using the "fake" altinn token service, not allowed to configure this in samarbeidsportalen
        const string scopes = "altinn:maskinporten/systemuser.read";

        var altinnEnterpriseToken =
            await _platformClient.GetEnterpriseAltinnToken(_platformClient.EnvironmentHelper.Vendor, scopes);

        var clientId = _platformClient.EnvironmentHelper.maskinportenClientId;
        var systemProviderOrgNo = _platformClient.EnvironmentHelper.Vendor;
        var systemUserOwnerOrgNo = _platformClient.EnvironmentHelper.Vendor;
        var externalRef = systemUser?.ExternalRef;

        var queryString =
            $"?clientId={clientId}" +
            $"&systemProviderOrgNo={systemProviderOrgNo}" +
            $"&systemUserOwnerOrgNo={systemUserOwnerOrgNo}" +
            $"&externalRef={externalRef}";

        var fullEndpoint = $"{Endpoints.GetSystemUserByExternalId.Url()}{queryString}";

        var resp = await _platformClient.GetAsync(fullEndpoint, altinnEnterpriseToken);
        Assert.NotNull(resp);
        Assert.Equal(System.Net.HttpStatusCode.OK, resp.StatusCode);
    }

    [SkipUnlessTt02Fact]
    public async Task SystemuserGetToken_ReturnsTokenForOrgWithExternalRef()
    {
        var systemUser = await GetSystemUserOnSystemId(SystemId);
        Assert.NotNull(systemUser);
        Assert.NotNull(systemUser.ExternalRef);

        var maskinportenToken = await _platformClient.GetSystemUserToken(systemUser.ExternalRef);
        Assert.NotNull(maskinportenToken);
    }

    [SkipUnlessTt02Fact]
    public async Task SystemuserGetToken_ReturnsTokenForOrgNoExternalRef()
    {
        // Act
        var maskinportenToken = await _platformClient.GetSystemUserToken();

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(maskinportenToken), "Token should not be null or empty");

        var tokenParts = maskinportenToken.Split('.');
        Assert.True(3 == tokenParts.Length, "Token should be a valid JWT with three parts");

        // Decode and parse the payload
        var payloadJson = Encoding.UTF8.GetString(Convert.FromBase64String(PadBase64(tokenParts[1])));
        var payload = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(payloadJson);
        Assert.NotNull(payload);
        Assert.True(payload.ContainsKey("exp"), "Token should have an expiration claim");

        Assert.True(payload.ContainsKey("authorization_details"), "Missing 'authorization_details'");
        var authDetailsArray = payload["authorization_details"].Deserialize<JsonElement[]>();
        Assert.NotNull(authDetailsArray);
        Assert.NotEmpty(authDetailsArray);

        var authDetails = authDetailsArray[0];

        Assert.Equal("urn:altinn:systemuser", authDetails.GetProperty("type").GetString());

        var systemUserOrg = authDetails.GetProperty("systemuser_org");
        Assert.Equal("iso6523-actorid-upis", systemUserOrg.GetProperty("authority").GetString());
        Assert.Equal($"0192:{_platformClient.EnvironmentHelper.Vendor}", systemUserOrg.GetProperty("ID").GetString());

        Assert.Equal(SystemId, authDetails.GetProperty("system_id").GetString());

        var expElement = payload["exp"];
        Assert.True(expElement.ValueKind == JsonValueKind.Number, "Token 'exp' claim should be a number");

        var exp = expElement.GetInt64();
        var expirationTime = DateTimeOffset.FromUnixTimeSeconds(exp);

        Assert.True(expirationTime > DateTimeOffset.UtcNow, "Token should not be expired");

        var remainingTime = expirationTime - DateTimeOffset.UtcNow;
        Assert.True(remainingTime < TimeSpan.FromSeconds(122) && remainingTime > TimeSpan.FromSeconds(118), $"Token should be valid for less than 2 minutes, remaining time is: {remainingTime}");
    }

    [SkipUnlessTt02Fact]
    public async Task Systemusertoken_Denied()
    {
        await GetSystemUserOnSystemId(SystemId);
        var systemUserToken = await _platformClient.GetSystemUserToken();

        const string baseUrl = "https://systemuserapi.azurewebsites.net/api";
        const string logistics = "Logistics";
        // const string salary = "Salary"; //The other api

        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", systemUserToken);
        var response = await client.GetAsync($"{baseUrl}/{logistics}/{_platformClient.EnvironmentHelper.Vendor}");

        var responseContent = await response.Content.ReadAsStringAsync();

        Assert.NotNull(responseContent);
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
    }
    
    [SkipUnlessTt02Fact]
    public async Task Consent_Demo_Testmoete_Expired()
    {
        var from = "urn:altinn:person:identifier-no:17866298211";
        
        var maskinportenToken = await _platformClient.GetConsentToken("cdb9e6d1-4a01-4aec-b17d-7da21277618f", from);
        
        _outputHelper.WriteLine(maskinportenToken);
        Assert.NotNull(maskinportenToken);
    }
    
    [SkipUnlessTt02Fact]
    public async Task Consent_Demo_Testmoete()
    {
        // To: 312605031 (Maskinportenklient org, som skal lese data, leverandør)
        var from = "urn:altinn:person:identifier-no:17866298211";
        
        var maskinportenToken = await _platformClient.GetConsentToken("ecaf59cc-5460-4c69-8ad0-616a054d29d4", from);
        
        _outputHelper.WriteLine(maskinportenToken);
        Assert.NotNull(maskinportenToken);
    }



    [SkipUnlessTt02Fact]
    public async Task SystemuserGetToken_Consent()
    {
       // var from = "urn:altinn:person:identifier-no:17866298211";

       var from = "urn:altinn:organization:identifier-no:214246732";
        // This works, but should expire 13.08.2025 - 16:30: https://am.ui.tt02.altinn.no/accessmanagement/ui/consent/request?id=cdb9e6d1-4a01-4aec-b17d-7da21277618f
        var maskinportenToken = await _platformClient.GetConsentToken("cdb9e6d1-4a01-4aec-b17d-7da21277618f", from);
        
        // var maskinportenToken = await _platformClient.GetConsentToken("1542da16-dda8-47ff-8eda-16954d1d1a62", "03848999848");
        
        _outputHelper.WriteLine(maskinportenToken);
        Assert.NotNull(maskinportenToken);
    }

    private async Task<SystemUser?> GetSystemUserOnSystemId(string systemId)
    {
        var testuser = _platformClient.TestUsers.Find(testUser => testUser.Org!.Equals(_platformClient.EnvironmentHelper.Vendor))
                       ?? throw new Exception($"Test user not found for organization: {_platformClient.EnvironmentHelper.Vendor}");

        var systemUsers = await _platformClient.SystemUserClient.GetSystemUsersForTestUser(testuser);

        return systemUsers.Find(user => user.SystemId == systemId);
    }


    // Utility function to properly pad Base64 strings before decoding
    private static string PadBase64(string base64)
    {
        base64 = base64.Replace('-', '+').Replace('_', '/'); // Convert URL-safe Base64 to standard Base64
        return base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '='); // Ensure proper padding
    }
}