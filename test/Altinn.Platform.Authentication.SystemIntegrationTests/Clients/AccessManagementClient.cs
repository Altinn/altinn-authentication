using Altinn.Platform.Authentication.SystemIntegrationTests.Utils.ApiEndpoints;

namespace Altinn.Platform.Authentication.SystemIntegrationTests.Clients;

public class AccessManagementClient
{
    private readonly PlatformAuthenticationClient _platformClient;

    public AccessManagementClient(PlatformAuthenticationClient platformClient)
    {
        _platformClient = platformClient;
    }

    public async Task<HttpResponseMessage> PostDecision(string requestBody)
    {
        var subscriptionKey = _platformClient.EnvironmentHelper.Testenvironment == "tt02"
            ? _platformClient.EnvironmentHelper.AuthorizationSubscriptionKeyTT02
            : _platformClient.EnvironmentHelper.AuthorizationSubscriptionKeyAt22;

        using var client = new HttpClient();
        // client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", subscriptionKey);
        HttpContent? content = string.IsNullOrEmpty(requestBody)
            ? null
            : new StringContent(requestBody, System.Text.Encoding.UTF8, "application/json");

        var response = await client.PostAsync($"{_platformClient.BaseUrlAuthentication}/{Endpoints.Decision.Url()}",
            content);

        return response;
    }
}