using System.Net;
using Altinn.Platform.Authentication.SystemIntegrationTests.Tests;
using Altinn.Platform.Authentication.SystemIntegrationTests.Utils;
using Xunit;

namespace Altinn.Platform.Authentication.SystemIntegrationTests.Clients;

/// <summary>
/// The purpose is that you only have to create an instance of this class and
/// Platform client to use any other request and common methods when creating new tests
/// </summary>
public class EndToEndClient
{
    private readonly PlatformAuthenticationClient _platformClient;
    public readonly SystemRegisterClient _systemRegisterClient;

    public EndToEndClient(PlatformAuthenticationClient platformClient)
    {
        _platformClient = platformClient;
        _systemRegisterClient = new SystemRegisterClient(_platformClient);
    }

    public async Task<bool> DoesSystemExist(string systemId)
    {
        var maskinportenToken = await _platformClient.GetToken();

        var path = $"/authentication/api/v1/systemregister/vendor/{systemId}";
        var uri = new UriBuilder(_platformClient.BaseUrl + path)
        {
            Query = $"systemId={systemId}"
        };

        var test = await _platformClient.GetAsync(uri, maskinportenToken);
        
        

        return test.StatusCode != HttpStatusCode.OK;
    }

    public async Task<string> CreateSystemRequest(string systemId, string token)
    {
        var doesExist = await DoesSystemExist(systemId);
        
        if (!doesExist)
        {
            var teststate = new SystemRegisterState()
                .WithClientId(Guid.NewGuid()
                    .ToString()) //For a real case it should use a maskinporten client id, but that means you cant post the same system again
                .WithVendor("312605031")
                .WithResource(value: "kravogbetaling", id: "urn:altinn:resource")
                .WithRedirectUrl("https://altinn.no")
                .WithToken(token);

            var response = await _systemRegisterClient.PostSystem(teststate);

            //Return request guid
            Assert.True(response.IsSuccessStatusCode, response.ReasonPhrase);
        }

        // Prepare
        var body = await Helper.ReadFile("Resources/Testdata/SystemUser/CreateRequestEndToEnd.json");

        body = body
                .Replace("{systemId}", systemId)
                .Replace("{redirectUrl}", "https://altinn.no")
                .Replace("{externalRef}", Guid.NewGuid().ToString());

        // Act
        var respons =
            await _platformClient.PostAsync("authentication/api/v1/systemuser/request/vendor", body,
                token);

        var content = await respons.Content.ReadAsStringAsync();

        Assert.True(HttpStatusCode.Created == respons.StatusCode,
            $"Status code was not Created, but: {respons.StatusCode} -  {content}  body:{body}");

        return content;
    }
}