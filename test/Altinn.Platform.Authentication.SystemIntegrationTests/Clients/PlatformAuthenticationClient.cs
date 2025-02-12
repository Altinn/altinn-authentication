using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Altinn.Platform.Authentication.SystemIntegrationTests.Domain;
using Altinn.Platform.Authentication.SystemIntegrationTests.Utils;
using Xunit;

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
    public readonly string? BaseUrl;

    /// <summary>
    /// Base class for running requests
    /// </summary>
    public PlatformAuthenticationClient()
    {
        EnvironmentHelper = LoadEnvironment();
        BaseUrl = GetEnvironment(EnvironmentHelper.Testenvironment);
        MaskinPortenTokenGenerator = new MaskinPortenTokenGenerator(EnvironmentHelper);
        TestUsers = LoadTestUsers(EnvironmentHelper.Testenvironment);
    }

    private static List<Testuser> LoadTestUsers(string testenvironment)
    {
        // Determine the file to load based on the environment
        var fileName = testenvironment.StartsWith("at")
            ? "Resources/Testusers/testusers.at.json"
            : "Resources/Testusers/testusers.tt02.json";

        if (!File.Exists(fileName))
        {
            throw new FileNotFoundException($"Test users file not found: {fileName}");
        }

        // Read and deserialize the JSON file into a list of Testuser objects
        var json = File.ReadAllText(fileName);
        return JsonSerializer.Deserialize<List<Testuser>>(json)
               ?? throw new InvalidOperationException("Failed to deserialize test users.");
    }

    private string GetEnvironment(string environmentHelperTestenvironment)
    {
        // Define base URLs for tt02 and all "at" environments
        const string tt02 = "https://platform.tt02.altinn.no/authentication/api/";
        const string atBaseUrl = "https://platform.{env}.altinn.cloud/authentication/api/";

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
    public async Task<HttpResponseMessage> PostAsync(string endpoint, string body, string token)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        HttpContent? content = string.IsNullOrEmpty(body)
            ? null
            : new StringContent(body, System.Text.Encoding.UTF8, "application/json");

        var response = await client.PostAsync($"{BaseUrl}/{endpoint}", content);
        return response;
    }

    /// <summary>
    /// For general Get requests
    /// </summary>
    /// <param name="endpoint">path to api endpoint</param>
    /// <param name="token">Token used</param>
    /// <returns></returns>
    public async Task<HttpResponseMessage> GetAsync(string endpoint, string? token)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return await client.GetAsync($"{BaseUrl}/{endpoint}");
    }

    public async Task<HttpResponseMessage> PutAsync(string path, string requestBody, string? token)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        HttpContent content = new StringContent(requestBody, System.Text.Encoding.UTF8, "application/json");

        return await client.PutAsync($"{BaseUrl}/{path}", content);
    }

    /// <summary>
    /// Delete
    /// </summary>
    /// <param name="endpoint"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public async Task<HttpResponseMessage> Delete(string endpoint, string? token)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return await client.DeleteAsync($"{BaseUrl}/{endpoint}");
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
        var response = await client.GetAsync(BaseUrl + "v1/exchange/maskinporten?test=true");

        if (response.IsSuccessStatusCode)
        {
            var accessToken = await response.Content.ReadAsStringAsync();
            return accessToken;
        }

        throw new Exception(
            $"Failed to retrieve exchange token: {response.StatusCode} {await response.Content.ReadAsStringAsync()}");
    }

    public Testuser FindTestUserByRole(string role)
    {
        var testUser = TestUsers.LastOrDefault(user => user.Role?.Equals(role, StringComparison.OrdinalIgnoreCase) == true);
        return testUser ?? throw new Exception($"Unable to find test user by role: {role}");
    }

    /// <summary>
    /// Used for fetching an Altinn test token for a specific role
    /// </summary>
    /// <param name="org"></param>
    /// <param name="scopes"></param>
    /// <returns>The Altinn test token as a string</returns>
    public async Task<string> GetEnterpriseAltinnToken(string? org, string scopes)
    {
        // GetEnterpriseToken?org=skatteetaten&env=tt02&orgNo=974761076&ttl=86400";

        // Construct the URL for fetching the Altinn test token
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
    /// <returns>The Altinn test token as a string</returns>
    public async Task<string> GetPersonalAltinnToken(Testuser user)
    {
        // Construct the URL for fetching the Altinn test token
        var url =
            $"https://altinn-testtools-token-generator.azurewebsites.net/api/GetPersonalToken" +
            $"?env={EnvironmentHelper.Testenvironment}" +
            $"&scopes=altinn:portal/enduser" +
            $"&pid={user.Pid}" +
            $"&userid={user.UserId}" +
            $"&partyid={user.AltinnPartyId}" +
            $"&authLvl=3&ttl=3000";

        // Retrieve the token
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

    private EnvironmentHelper LoadEnvironment()
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

    private EnvironmentHelper LoadEnvironmentFromFile()
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
    public async Task<string> GetMaskinportenTokenForVendor()
    {
        var token = await MaskinPortenTokenGenerator.GetMaskinportenBearerToken();
        Assert.True(null != token, "Unable to retrieve maskinporten token");
        return token;
    }

    public async Task<string> GetSystemUserToken(string? externalRef = "", string scopes="")
    {
        var token = await MaskinPortenTokenGenerator.GetMaskinportenSystemUserToken(externalRef);
        Assert.True(null != token, "Unable to retrieve maskinporten systemuser token");
        return token;
    }
    
    public async Task<HttpResponseMessage> DeleteRequest(string endpoint, Testuser testperson)
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
}