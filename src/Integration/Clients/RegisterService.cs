using Altinn.Authentication.Core.Clients.Interfaces;
using Altinn.Authentication.Integration.Configuration;
using Altinn.Platform.Authentication.Core.Models;
using Altinn.Platform.Authentication.Core.Models.Pagination;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;

namespace Altinn.Authentication.Integration.Clients;

public class RegisterService(HttpClient httpClient, IOptions<PlatformSettings> platformSettings) : IRegisterService
{
    private readonly PlatformSettings _platformSettings = platformSettings.Value;

    public async Task<(bool Success, PartyInfo? Party)> GetParty(string pid, CancellationToken cancellationToken)
    {
        string requestUri = $"{_platformSettings.ApiRegisterEndpoint}access-management/parties/query?fields=party,user";

        ListObject<string> body = ListObject.Create([$"urn:altinn:person:identifier-no:{pid}"]);
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = JsonContent.Create(body)
        };

        var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            ListObject<PartyInfo>? result = await response.Content.ReadFromJsonAsync<ListObject<PartyInfo>>(cancellationToken: cancellationToken);
            if (result != null && result.Items.Any())
            {
                return (true, result.Items.First());
            }
        }
        else
        {
            string error = await response.Content.ReadAsStringAsync(cancellationToken);
            Console.WriteLine($"Error getting party from register: {response.StatusCode}, {error}");
        }

        return (false, null);
    }
}
