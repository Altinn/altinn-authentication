using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Altinn.Platform.Authentication.SystemIntegrationTests.Domain.Authorization;
using Altinn.Platform.Authentication.SystemIntegrationTests.Utils;
using Altinn.Platform.Authentication.SystemIntegrationTests.Utils.ApiEndpoints;
using Xunit;

namespace Altinn.Platform.Authentication.SystemIntegrationTests.Clients;

public class AccessManagementClient
{
    private readonly PlatformAuthenticationClient _platformClient;
    private readonly string _subscriptionKey;

    public AccessManagementClient(PlatformAuthenticationClient platformClient)
    {
        _platformClient = platformClient;

        _subscriptionKey = _platformClient.EnvironmentHelper.Testenvironment == "tt02"
            ? _platformClient.EnvironmentHelper.AuthorizationSubscriptionKeyTT02
            : _platformClient.EnvironmentHelper.AuthorizationSubscriptionKeyAt22;
    }

    public async Task<HttpResponseMessage> PostDecision(string requestBody)
    {
        using var client = new HttpClient();
        // client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _subscriptionKey);
        HttpContent? content = string.IsNullOrEmpty(requestBody)
            ? null
            : new StringContent(requestBody, System.Text.Encoding.UTF8, "application/json");

        var response = await client.PostAsync($"{_platformClient.BaseUrlAuthentication}/{Endpoints.Decision.Url()}",
            content);

        return response;
    }

    public async Task<List<AccessPackagesExport>?> GetAccessPackages()
    {
        using var client = new HttpClient();
        // client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _subscriptionKey);
        List<AccessPackagesExport>? accessPackages = await client.GetFromJsonAsync<List<AccessPackagesExport>>($"{_platformClient.BaseUrlAuthentication}/{Endpoints.AccessPackagesExport.Url()}");

        return accessPackages;
    }
}