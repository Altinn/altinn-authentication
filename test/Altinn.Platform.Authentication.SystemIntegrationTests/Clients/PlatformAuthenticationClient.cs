using System.Net.Http.Headers;
using System.Text.Json;
using Altinn.Platform.Authentication.SystemIntegrationTests.Domain;
using Altinn.Platform.Authentication.SystemIntegrationTests.Tests;
using Altinn.Platform.Authentication.SystemIntegrationTests.Utils;
using Xunit;

namespace Altinn.Platform.Authentication.SystemIntegrationTests.Clients;

/// <summary>
/// Summary of class
/// </summary>
public class PlatformAuthenticationClient
{
    public EnvironmentHelper EnvironmentHelper { get; set; }
    public MaskinPortenTokenGenerator _maskinPortenTokenGenerator { get; set; }

    /// <summary>
    /// baseUrl for api
    /// </summary>
    public readonly string BaseUrl;

    /// <summary>
    /// Base class for running requests
    /// </summary>
    public PlatformAuthenticationClient()
    {
        EnvironmentHelper = LoadEnvironment();
        BaseUrl = $"https://platform.{EnvironmentHelper.Testenvironment}.altinn.cloud";
        _maskinPortenTokenGenerator = new MaskinPortenTokenGenerator(EnvironmentHelper);
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

        HttpContent content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");

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

    public async Task<HttpResponseMessage> GetAsync(UriBuilder uriBuilder, string? token)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return await client.GetAsync(uriBuilder.ToString());
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
        var response = await client.GetAsync(BaseUrl + "/authentication/api/v1/exchange/maskinporten?test=true");

        if (response.IsSuccessStatusCode)
        {
            var accessToken = await response.Content.ReadAsStringAsync();
            return accessToken;
        }

        throw new Exception(
            $"Failed to retrieve exchange token: {response.StatusCode} {await response.Content.ReadAsStringAsync()}");
    }

    /// <summary>
    /// Used for fetching an Altinn test token
    /// </summary>
    /// <param name="user"></param>
    /// <returns></returns>
    public async Task<string> GetPersonalAltinnToken(AltinnUser user)
    {
        var url =
            $"https://altinn-testtools-token-generator.azurewebsites.net/api/GetPersonalToken?env={EnvironmentHelper.Testenvironment}" +
            $"&scopes={user.scopes}" +
            $"&pid={user.pid}" +
            $"&userid={user.userId}" +
            $"&partyid={user.partyId}" +
            $"&authLvl=3&ttl=3000";

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

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return content;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Exception occurred: " + ex.Message);
        }

        throw new InvalidOperationException("Unable to get Altinn token");
    }

    private static EnvironmentHelper LoadEnvironment()
    {
        const string githubVariable = "SYSTEMINTEGRATIONTEST_JSON";
        var envJson = Environment.GetEnvironmentVariable(githubVariable);

        //Runs on Github
        if (!string.IsNullOrEmpty(envJson))
        {
            return JsonSerializer.Deserialize<EnvironmentHelper>(envJson)
                   ?? throw new Exception($"Unable to deserialize environment from {githubVariable}.");
        }

        //Runs locally
        return LoadEnvironmentFromFile();
    }

    private static EnvironmentHelper LoadEnvironmentFromFile()
    {
        const string localFilePath = "Resources/Environment/environment.json";
        var jsonString = Helper.ReadFile(localFilePath).Result;
        return JsonSerializer.Deserialize<EnvironmentHelper>(jsonString)
               ?? throw new Exception($"Unable to read environment from local file path: {localFilePath}.");
    }

    public async Task<string> GetToken()
    {
        var token = await _maskinPortenTokenGenerator.GetMaskinportenBearerToken();
        Assert.True(null != token, "Unable to retrieve maskinporten token");
        return token;
    }
}