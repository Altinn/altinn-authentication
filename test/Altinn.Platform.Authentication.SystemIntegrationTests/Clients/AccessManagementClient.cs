using System.Net;
using System.Net.Http.Headers;
using Altinn.Platform.Authentication.SystemIntegrationTests.Utils;
using Xunit;

namespace Altinn.Platform.Authentication.SystemIntegrationTests.Clients;

public class AccessManagementClient
{
    private readonly PlatformAuthenticationClient _platformClient;

    public AccessManagementClient(PlatformAuthenticationClient platformClient)
    {
        _platformClient = platformClient;
    }

    public async Task<HttpResponseMessage> PostDecision(string requestBody, string? token)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token); 
        client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _platformClient.EnvironmentHelper.AuthorizationSubscriptionKey);
        HttpContent? content = string.IsNullOrEmpty(requestBody)
            ? null
            : new StringContent(requestBody, System.Text.Encoding.UTF8, "application/json");

        var response = await client.PostAsync($"{_platformClient.BaseUrlAuthentication}/authorization/api/v1/decision", content);
        return response;
    }
}