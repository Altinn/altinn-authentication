using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Altinn.Platform.Authentication.SystemIntegrationTests.Clients;
using Altinn.Platform.Authentication.SystemIntegrationTests.Domain;
using Altinn.Platform.Authentication.SystemIntegrationTests.Utils;
using Xunit;
using Xunit.Abstractions;

namespace Altinn.Platform.Authentication.SystemIntegrationTests.Tests;

// Documentation: https://docs.digdir.no/docs/Maskinporten/maskinporten_func_systembruker
public class SystemuserGetTokenTest
{
    private readonly ITestOutputHelper _outputHelper;
    private readonly SystemRegisterClient _systemRegisterClient;
    private readonly PlatformAuthenticationClient _platformClient;
    private readonly SystemUserClient _systemUserClient;
    private const string SystemId = "312605031_Team-Authentication-SystemuserE2E-User-Do-Not-Delete";

    private static readonly JsonSerializerOptions JsonSerializerOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Testing System user endpoints
    /// To find relevant api endpoints: https://docs.altinn.studio/nb/api/authentication/spec/#/RequestSystemUser
    /// </summary>
    /// 
    public SystemuserGetTokenTest(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;
        _platformClient = new PlatformAuthenticationClient();
        _systemUserClient = new SystemUserClient(_platformClient);
        _systemRegisterClient = new SystemRegisterClient(_platformClient);
    }

    [Fact]
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
        // var externalRef = systemUser?.ExternalRef;
        var externalRef = "51bb1a80-b19a-4222-8fa0-1fa31dd7d2f3";

        var queryString =
            $"?clientId={clientId}" +
            $"&systemProviderOrgNo={systemProviderOrgNo}" +
            $"&systemUserOwnerOrgNo={systemUserOwnerOrgNo}";
        // $"&externalRef={externalRef}";

        var fullEndpoint = $"{UrlConstants.SystemUserGetByExternalRef}{queryString}";

        var resp = await _platformClient.GetAsync(fullEndpoint, altinnEnterpriseToken);
        Assert.NotNull(resp);
        Assert.Equal(System.Net.HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task SystemuserGetToken_ReturnsTokenForOrgWithExternalRef()
    {
        //External Ref is set to Vendor's org for both tt and at
        var externalRef = _platformClient.EnvironmentHelper.Vendor;
        externalRef = "51bb1a80-b19a-4222-8fa0-1fa31dd7d2f3";
        var maskinportenToken = await _platformClient.GetSystemUserToken(externalRef);

        Assert.NotNull(maskinportenToken);
    }

    [Fact]
    public async Task SystemuserGetToken_ReturnsTokenForOrgNoExternalRef()
    {
        // Arrange
        var externalRef = _platformClient.EnvironmentHelper.Vendor;
        externalRef = "51bb1a80-b19a-4222-8fa0-1fa31dd7d2f3"; //This one exists now
        var name = "IntegrationTestNbTeam-Authentication-SystemuserE2E-User-Do-Not-Delete-TT02";

        // Act
        var maskinportenToken = await _platformClient.GetSystemUserToken(externalRef);

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(maskinportenToken), "Token should not be null or empty");

        var tokenParts = maskinportenToken.Split('.');
        Assert.True(3 == tokenParts.Length, "Token should be a valid JWT with three parts");

        // Decode and parse the payload
        var payloadJson = Encoding.UTF8.GetString(Convert.FromBase64String(PadBase64(tokenParts[1])));
        var payload = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(payloadJson);

        Assert.NotNull(payload);
        Assert.True(payload.ContainsKey("exp"), "Token should have an expiration claim");

        var expElement = payload["exp"];
        Assert.True(expElement.ValueKind == JsonValueKind.Number, "Token 'exp' claim should be a number");

        var exp = expElement.GetInt64();
        var expirationTime = DateTimeOffset.FromUnixTimeSeconds(exp);

        Assert.True(expirationTime > DateTimeOffset.UtcNow, "Token should not be expired");

        var remainingTime = expirationTime - DateTimeOffset.UtcNow;
        Assert.True(remainingTime < TimeSpan.FromMinutes(2) && remainingTime > TimeSpan.FromSeconds(118), $"Token should be valid for less than 2 minutes, remaining time is: {remainingTime}");
    }

    [Fact]
    public async Task Systemusertoken_Denied()
    {
        await GetSystemUserOnSystemId(SystemId);
        var systemUserToken = await _platformClient.GetSystemUserToken();

        const string baseUrl = "https://systemuserapi.azurewebsites.net/api";
        const string logistics = "Logistics";
        const string salary = "Salary";

        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", systemUserToken);
        var response = await client.GetAsync($"{baseUrl}/{logistics}/{_platformClient.EnvironmentHelper.Vendor}");

        var responseContent = await response.Content.ReadAsStringAsync();

        Assert.NotNull(responseContent);
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateNewSystemUSer()
    {
        const string systemId = "312605031_Team-Authentication-SystemuserE2E-User-Do-Not-Delete-TT02";
        var systemUser = await CreateSystemUserWithProperClient();
        Assert.NotNull(systemUser);
    }

    [Fact]
    public async Task GetSystemUserOnExternalRef()
    {
        var testuser = _platformClient.TestUsers.Find(testUser => testUser.Org.Equals(_platformClient.EnvironmentHelper.Vendor))
                       ?? throw new Exception($"Test user not found for organization: {_platformClient.EnvironmentHelper.Vendor}");

        var systemUsers = await GetSystemUsers(testuser);

        const string externalRef = "51bb1a80-b19a-4222-8fa0-1fa31dd7d2f3";

        var user = systemUsers.Find(user => user.ExternalRef.Equals(externalRef));
        Assert.NotNull(user);
        
        //Sjekk ulike varianter av denne.
    }

    private async Task<SystemUser?> CreateSystemUserWithProperClient()
    {
        var testuser = _platformClient.TestUsers.Find(testUser => testUser.Org.Equals(_platformClient.EnvironmentHelper.Vendor))
                       ?? throw new Exception($"Test user not found for organization: {_platformClient.EnvironmentHelper.Vendor}");

        var maskinportenToken = await _platformClient.GetMaskinportenTokenForVendor();

        var teststate = new SystemRegisterHelper("Resources/Testdata/Systemregister/CreateNewSystem.json")
            .WithClientId(_platformClient.EnvironmentHelper.maskinportenClientId) //Creates System User With MaskinportenClientId
            .WithVendor(_platformClient.EnvironmentHelper.Vendor)
            .WithResource(value: "vegardtestressurs", id: "urn:altinn:resource")
            .WithResource(value: "authentication-e2e-test", id: "urn:altinn:resource")
            .WithResource(value: "app_ttd_endring-av-navn-v2", id: "urn:altinn:resource")
            .WithName("Team-Authentication-SystemuserE2E-User-Do-Not-Delete-TT02")
            .WithToken(maskinportenToken);

        // var requestBody = teststate.GenerateRequestBody();

        // Create system in System Register
        // await _systemRegisterClient.PostSystem(requestBody, maskinportenToken);


        // Create system user with same created rights mentioned above
        var postSystemUserResponse = await _systemUserClient.CreateSystemUserRequestWithExternalRef(teststate, maskinportenToken);

        //Approve system user
        var id = Common.ExtractPropertyFromJson(postSystemUserResponse, "id");
        var systemId = Common.ExtractPropertyFromJson(postSystemUserResponse, "systemId");

        await _systemUserClient.ApproveSystemUserRequest(testuser, id);

        //Return system user and make sure it was created
        return await GetSystemUserOnSystemId(systemId);
    }

    private async Task<SystemUser?> GetSystemUserOnSystemId(string systemId)
    {
        var testuser = _platformClient.TestUsers.Find(testUser => testUser.Org.Equals(_platformClient.EnvironmentHelper.Vendor))
                       ?? throw new Exception($"Test user not found for organization: {_platformClient.EnvironmentHelper.Vendor}");

        var systemUsers = await GetSystemUsers(testuser);

        return systemUsers.Find(user => user.SystemId == systemId);
    }

    private async Task<List<SystemUser>> GetSystemUsers(Testuser testuser)
    {
        var altinnToken = await _platformClient.GetPersonalAltinnToken(testuser);
        var resp = await _systemUserClient.GetSystemuserForParty(testuser.AltinnPartyId, altinnToken);

        var content = await resp.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<SystemUser>>(content, JsonSerializerOptions) ?? [];
    }

    // Utility function to properly pad Base64 strings before decoding
    private static string PadBase64(string base64)
    {
        base64 = base64.Replace('-', '+').Replace('_', '/'); // Convert URL-safe Base64 to standard Base64
        return base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '='); // Ensure proper padding
    }
}