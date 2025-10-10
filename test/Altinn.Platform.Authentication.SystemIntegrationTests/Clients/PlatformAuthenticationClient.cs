using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Altinn.Platform.Authentication.SystemIntegrationTests.Domain;
using Altinn.Platform.Authentication.SystemIntegrationTests.Utils;
using Altinn.Platform.Authentication.SystemIntegrationTests.Utils.ApiEndpoints;
using Xunit;
using Xunit.Abstractions;

namespace Altinn.Platform.Authentication.SystemIntegrationTests.Clients;

/// <summary>
/// Summary of class
/// </summary>
public class PlatformAuthenticationClient
{
    public EnvironmentHelper EnvironmentHelper { get; set; }
    public MaskinPortenTokenGenerator MaskinPortenTokenGenerator { get; set; }
    public List<Testuser> TestUsers { get; set; }

    /// <summary>
    /// baseUrl for api
    /// </summary>
    public readonly string? BaseUrlAuthentication;
    
    public SystemRegisterClient SystemRegisterClient { get; set; }
    public SystemUserClient SystemUserClient { get; set; }
    public AccessManagementClient AccessManagementClient { get; set; }
    public Common Common { get; set; }

    private static string? _cachedToken;
    private static DateTime _tokenExpiry;

    /// <summary>
    /// Base class for running requests
    /// </summary>
    public PlatformAuthenticationClient()
    {
        EnvironmentHelper = LoadEnvironment();
        BaseUrlAuthentication = GetEnvironment(EnvironmentHelper.Testenvironment);
        MaskinPortenTokenGenerator = new MaskinPortenTokenGenerator(EnvironmentHelper);
        TestUsers = LoadTestUsers(EnvironmentHelper.Testenvironment);
        SystemRegisterClient = new SystemRegisterClient(this);
        SystemUserClient = new SystemUserClient(this);
        AccessManagementClient = new AccessManagementClient(this);
        Common = new Common(this);
    }

    private static List<Testuser> LoadTestUsers(string testenvironment)
    {
        // Determine the file to load based on the environment
        var fileName = testenvironment.Equals("at22")
            ? "Resources/Testusers/testusers.at22.json"
            : "Resources/Testusers/testusers.tt02.json";

        if (!File.Exists(fileName))
        {
            throw new FileNotFoundException($"Test users file not found: {fileName}");
        }

        var json = File.ReadAllText(fileName);
        List<Testuser> testUsers = JsonSerializer.Deserialize<List<Testuser>>(json)
                                   ?? throw new InvalidOperationException("Failed to deserialize test users.");

        if (testUsers.Count <= 2)
        {
            throw new InvalidOperationException(
                $"Expected at least 3 test users in {fileName}, but found {testUsers.Count}."
            );
        }

        return testUsers;
    }

    public static string GetEnvironment(string environmentHelperTestenvironment)
    {
        // Define base URLs for tt02 and all "at" environments
        const string tt02 = "https://platform.tt02.altinn.no/";
        const string atBaseUrl = "https://platform.{env}.altinn.cloud/";

        // Handle case-insensitive input and return the correct URL
        environmentHelperTestenvironment = environmentHelperTestenvironment.ToLower();

        return environmentHelperTestenvironment switch
        {
            "tt02" => tt02,
            "at22" or "at23" or "at24" => atBaseUrl.Replace("{env}", environmentHelperTestenvironment),
            _ => throw new ArgumentException($"Unknown environment: {environmentHelperTestenvironment}")
        };
    }

    /// <summary>
    /// Post a request  
    /// </summary>
    /// <param name="endpoint">Endpoint for api</param>
    /// <param name="body">Request body, see Swagger documentation for reference</param>
    /// <param name="token">Bearer token</param>
    /// <returns></returns>
    public async Task<HttpResponseMessage> PostAsync(string? endpoint, string body, string? token)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        HttpContent? content = string.IsNullOrEmpty(body)
            ? null
            : new StringContent(body, System.Text.Encoding.UTF8, "application/json");

        // "https://am.ui.tt02.altinn.no/accessmanagement/api/v1/systemuser/request/51571095/5c9580b9-ffd0-4f2e-ba6a-c7d10b65bda3/approve"
        var response = await client.PostAsync($"{BaseUrlAuthentication}/{endpoint}", content);
        return response;
    }

    public async Task<HttpResponseMessage> PostAsyncWithNoBody(string? endpoint, string? token)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsync($"{BaseUrlAuthentication}/{endpoint}", null);
        return response;
    }

    /// <summary>
    /// For general Get requests
    /// </summary>
    /// <param name="endpoint">path to api endpoint</param>
    /// <param name="token">Token used</param>
    /// <returns></returns>
    public async Task<HttpResponseMessage> GetAsync(string? endpoint, string? token)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return await client.GetAsync($"{BaseUrlAuthentication}/{endpoint}");
    }

    public async Task<T?> GetAsyncOnType<T>(string? endpoint, string? token)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return await client.GetFromJsonAsync<T>($"{BaseUrlAuthentication}/{endpoint}");
    }


    public async Task<HttpResponseMessage> GetNextUrl(string? endpoint, string? token)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return await client.GetAsync($"{endpoint}");
    }

    public async Task<HttpResponseMessage> PutAsync(string? path, string requestBody, string? token)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        HttpContent content = new StringContent(requestBody, System.Text.Encoding.UTF8, "application/json");

        return await client.PutAsync($"{BaseUrlAuthentication}/{path}", content);
    }

    /// <summary>
    /// Delete
    /// </summary>
    /// <param name="endpoint"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public async Task<HttpResponseMessage> Delete(string? endpoint, string? token)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return await client.DeleteAsync($"{BaseUrlAuthentication}/{endpoint}");
    }

    /// <summary>
    /// Endpoint that converts a maskinporten token into an Altinn Token
    /// </summary>
    /// <param name="token">Bearer token from Maskinporten</param>
    /// <returns></returns>
    /// <exception cref="Exception">Throws exception if no token is returned</exception>
    public async Task<string> GetExchangeToken(string? token)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.GetAsync(BaseUrlAuthentication + "authentication/api/v1/exchange/maskinporten?test=true");

        if (response.IsSuccessStatusCode)
        {
            var accessToken = await response.Content.ReadAsStringAsync();
            return accessToken;
        }

        throw new Exception(
            $"Failed to retrieve exchange token: {response.StatusCode} {await response.Content.ReadAsStringAsync()}");
    }

    public Testuser? FindTestUserByRole(string role)
    {
        var testUser = TestUsers.LastOrDefault(user => user.Role?.Equals(role, StringComparison.OrdinalIgnoreCase) == true);
        return testUser ?? throw new Exception($"Unable to find test user with role: {role}");
    }

    /// <summary>
    /// Used for fetching an Altinn test token for a specific role
    /// </summary>
    /// <param name="org"></param>
    /// <param name="scopes"></param>
    /// <returns>The Altinn test token as a string</returns>
    public async Task<string?> GetEnterpriseAltinnToken(string? org, string scopes)
    {
        var url =
            $"https://altinn-testtools-token-generator.azurewebsites.net/api/GetEnterpriseToken?env={EnvironmentHelper.Testenvironment}&orgNo={org}" +
            $"&scopes={scopes}" +
            $"&authLvl=3&ttl=3000";

        // Retrieve the token
        var token = await GetAltinnToken(url);
        Assert.True(token != null, "Token retrieval failed for Altinn token");
        return token;
    }

    /// <summary>
    /// Used for fetching an Altinn test token for a specific role
    /// </summary>
    /// <param name="user">User read from test config (testusers.at.json)</param>
    /// <param name="scopes">space separated list of scopes</param>
    /// <returns>The Altinn test token as a string</returns>
    public async Task<string?> GetPersonalAltinnToken(Testuser? user, string scopes = "")
    {
        var url =
            $"https://altinn-testtools-token-generator.azurewebsites.net/api/GetPersonalToken" +
            $"?env={EnvironmentHelper.Testenvironment}" +
            $"&scopes=altinn:portal/enduser " + scopes +
            $"&userid={user.UserId}" +
            $"&partyid={user.AltinnPartyId}" +
            $"&partyuuid={user.AltinnPartyUuid}" +
            "&authLvl=3&ttl=10000";

        var token = await GetAltinnToken(url);
        Assert.True(token != null, "Token retrieval failed for Altinn token");
        return token;
    }

    /// <summary>
    /// Add header values to Http client needed for altinn test api
    /// </summary>
    /// <returns></returns>
    private async Task<string?> GetAltinnToken(string url)
    {
        var client = new HttpClient();
        var username = EnvironmentHelper.testCredentials.username;
        var password = EnvironmentHelper.testCredentials.password;
        var basicAuth = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{username}:{password}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basicAuth);
        try
        {
            var response = await client.GetAsync(url);

            var responseContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine(response.StatusCode);

            if (response.StatusCode != HttpStatusCode.OK)
                throw new Exception(
                    "Unable to get altinn token: " + response.StatusCode + " " +
                    responseContent);
            var content = await response.Content.ReadAsStringAsync();
            return content;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Exception occurred: " + ex.Message);
        }

        return null;
    }

    public static EnvironmentHelper LoadEnvironment()
    {
        const string githubVariable = "SYSTEMINTEGRATIONTEST_JSON";
        const string environmentVariable = "TEST_ENVIRONMENT";

        // Fetch environment JSON and override value
        var envJson = Environment.GetEnvironmentVariable(githubVariable);
        var testEnvironmentOverride = Environment.GetEnvironmentVariable(environmentVariable);

        // Runs on GitHub
        if (!string.IsNullOrEmpty(envJson))
        {
            // Deserialize the environment JSON
            var environmentHelper = JsonSerializer.Deserialize<EnvironmentHelper>(envJson) ?? throw new Exception($"Unable to deserialize environment from {githubVariable}.");

            // Override Testenvironment if TEST_ENVIRONMENT is provided
            if (!string.IsNullOrEmpty(testEnvironmentOverride))
            {
                environmentHelper.Testenvironment = testEnvironmentOverride;
            }

            return environmentHelper;
        }

        //Runs locally
        return LoadEnvironmentFromFile();
    }

    public static EnvironmentHelper LoadEnvironmentFromFile()
    {
        //Todo fix support for dev
        var localFilePath = "Resources/Environment/environment.json";
        if (localFilePath == null) throw new ArgumentNullException(nameof(localFilePath));
        var jsonString = Helper.ReadFile(localFilePath).Result;
        return JsonSerializer.Deserialize<EnvironmentHelper>(jsonString)
               ?? throw new Exception($"Unable to read environment from local file path: {localFilePath}.");
    }

    /// <summary>
    /// </summary>
    /// <returns>IMPORTANT - Returns a bearer token with this org / vendor: "312605031"</returns>
    public async Task<string?> GetMaskinportenTokenForVendor()
    {
        if (_cachedToken != null && DateTime.UtcNow < _tokenExpiry)
        {
            return _cachedToken;
        }

        var token = await MaskinPortenTokenGenerator.GetMaskinportenBearerToken();
        Assert.True(null != token, "Unable to retrieve maskinporten token");
        _cachedToken = token;
        _tokenExpiry = DateTime.UtcNow.AddMinutes(2); // Valid for 2 minutes

        return _cachedToken;
    }

    public async Task<string> GetSystemUserToken(string? externalRef = "", string scopes = "")
    {
        var token = await MaskinPortenTokenGenerator.GetMaskinportenSystemUserToken(externalRef);
        Assert.True(null != token, "Unable to retrieve maskinporten systemuser token");
        return token;
    }

    public async Task<HttpResponseMessage> DeleteRequest(string? endpoint, Testuser? testperson)
    {
        // Get the Altinn token
        var altinnToken = await GetPersonalAltinnToken(testperson);

        // Use the PostAsync method for the approval request
        var response = await Delete(endpoint, altinnToken);
        return response;
    }

    public Testuser GetTestUserForVendor()
    {
        var vendor = EnvironmentHelper.Vendor;

        return TestUsers.Find(testUser => testUser.Org!.Equals(vendor))
               ?? throw new Exception($"Test user not found for organization: {vendor}");
    }

    public async Task<Testuser> GetTestUserAndTokenForCategory(string category)
    {
        var testuser = TestUsers.Find(user => user.Category.Equals(category)) ?? throw new Exception("Unable to find testuser with category");
        testuser.AltinnToken = await GetPersonalAltinnToken(testuser);
        return testuser;
    }

    public async Task<HttpResponseMessage> GetCustomerList(Testuser testuser, string? systemUserUuid)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", testuser.AltinnToken);

        var endpoint = EnvironmentHelper.Testenvironment == "tt02" ? $"https://am.ui.tt02.altinn.no/accessmanagement/api/v1/systemuser/agentdelegation/{testuser.AltinnPartyId}/{systemUserUuid}/customers?partyuuid={testuser.AltinnPartyUuid}" : $"https://am.ui.at22.altinn.cloud/accessmanagement/api/v1/systemuser/agentdelegation/{testuser.AltinnPartyId}/{systemUserUuid}/customers?partyuuid={testuser.AltinnPartyUuid}";
        return await client.GetAsync(endpoint);
    }

    public async Task<HttpResponseMessage> DelegateFromAuthentication(Testuser facilitator, string? systemUserUuid, string requestBodyDelegation)
    {
        var tokenFacilitator = await GetPersonalAltinnToken(facilitator);

        var url = Endpoints.DelegationAuthentication.Url()
            .Replace("{facilitatorPartyId}", facilitator.AltinnPartyId)
            .Replace("{systemUserUuid}", systemUserUuid);

        return await PostAsync(url, requestBodyDelegation, tokenFacilitator);
    }

    public async Task<HttpResponseMessage> DeleteDelegation(Testuser facilitator, DelegationResponseDto selectedCustomer)
    {
        var url = Endpoints.DeleteCustomer.Url().Replace("{party}", facilitator.AltinnPartyId)
            .Replace("{delegationId}", selectedCustomer.delegationId);

        url += $"?facilitatorId={facilitator.AltinnPartyUuid}";

        return await Delete(url, facilitator.AltinnToken);
    }

    public async Task<string> GetSystemUsers(string systemId, string? maskinportenToken)
    {
        var users = await SystemUserClient.GetSystemUserById(systemId, maskinportenToken);
        return await users.Content.ReadAsStringAsync();
    }

    public async Task VerifyPagination(string initialJson, string? maskinportenToken)
    {
        string? nextUrl = ExtractNextUrl(initialJson);

        //Should never be empty first click due to creating 32 system users
        Assert.NotNull(nextUrl);
        Assert.NotEmpty(nextUrl);

        var root = JsonNode.Parse(initialJson);
        var systemUsersList = root?["data"]?.AsArray();

        //Assert all system users are present
        Assert.NotNull(systemUsersList);
        Assert.Equal(20, systemUsersList.Count);

        var resp = await GetNextUrl(nextUrl, maskinportenToken);
        Assert.NotNull(resp);

        var respBody = await resp.Content.ReadAsStringAsync();
        nextUrl = ExtractNextUrl(respBody);

        Assert.Null(nextUrl);

        root = JsonNode.Parse(respBody);
        systemUsersList = root?["data"]?.AsArray();

        // Assert remaining system users are present
        Assert.NotNull(systemUsersList);
        Assert.Equal(12, systemUsersList.Count);
    }

    private static string? ExtractNextUrl(string json)
    {
        using var doc = JsonDocument.Parse(json);

        if (doc.RootElement.TryGetProperty("links", out var linksElement) &&
            linksElement.TryGetProperty("next", out var nextElement))
        {
            return nextElement.GetString();
        }

        return null;
    }
}