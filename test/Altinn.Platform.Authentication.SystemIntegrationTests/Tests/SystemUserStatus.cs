using System.Net;
using System.Text.Json;
using Altinn.Platform.Authentication.SystemIntegrationTests.Clients;
using Xunit;
using Xunit.Abstractions;
using static Xunit.Assert;

namespace Altinn.Platform.Authentication.SystemIntegrationTests.Tests;

/// <summary>
/// Documentation:
/// Github issue: https://github.com/Altinn/altinn-authentication/issues/576
/// To find relevant api endpoints: https://docs.altinn.studio/nb/api/authentication/spec/#/RequestSystemUser
/// </summary>
///
[Trait("Category", "IntegrationTest")]
public class SystemUserStatus
{
    private readonly ITestOutputHelper _outputHelper;
    private readonly PlatformAuthenticationClient _platformClient;
    private readonly EndToEndClient _endToEndClient;

    // /Default systemId to avoid creating new systems all the time
    private const string SYSTEM_ID = "312605031_9cfc4cac-1776-4052-882a-d0874fc6b548";

    //Default system_request_id instead of creating new every time in test environment
    private const string REQUEST_ID = "83775428-9693-4b77-bd25-f0d55bfc4a5b";

    /// <summary>
    /// Testing System user endpoints
    /// </summary>
    /// 
    public SystemUserStatus(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;
        _platformClient = new PlatformAuthenticationClient();
        _endToEndClient = new EndToEndClient(_platformClient);
    }

    [Fact]
    public async Task Should_GetSystemUserRequest_ByVendorId()
    {
        var token = await _platformClient.GetToken();

        // Create system request and retrieve ID
        var createResponse = await _endToEndClient.CreateSystemRequest(SYSTEM_ID, token);
        var systemRequest = JsonSerializer.Deserialize<SystemUserRequestResponse>(createResponse);

        var endpointByVendor = $"authentication/api/v1/systemuser/request/vendor/{systemRequest?.id}";
        var responseByVendor = await _platformClient.GetAsync(endpointByVendor, token);

        // Assert status code and verify status is "New"
        True(responseByVendor.StatusCode == HttpStatusCode.OK,
            $"Response code wasn't 200, but: {responseByVendor.StatusCode}");

        var responseContent = await responseByVendor.Content.ReadAsStringAsync();
        Contains("\"status\": \"New\",", responseContent);
    }

    [Fact]
    public async Task Should_GetSystemUserRequest_ByExternalRef_And_BySystemId()
    {
        var token = await _platformClient.GetToken();

        // Create system request and deserialize response
        var createResponse = await _endToEndClient.CreateSystemRequest(SYSTEM_ID, token);
        var systemRequest = JsonSerializer.Deserialize<SystemUserRequestResponse>(createResponse);

        // Test endpoint by external reference
        var endpointByExternalRef =
            $"authentication/api/v1/systemuser/request/vendor/byexternalref/{systemRequest?.systemId}/{systemRequest?.partyOrgNo}/{systemRequest?.externalRef}";
        var responseByExternalRef = await _platformClient.GetAsync(endpointByExternalRef, token);

        True(responseByExternalRef.StatusCode == HttpStatusCode.OK,
            $"Response code wasn't 200, but: {responseByExternalRef.StatusCode}");

        // Test endpoint by system ID
        var endpointBySystem = $"authentication/api/v1/systemuser/request/vendor/bysystem/{systemRequest?.systemId}";
        var responseBySystem = await _platformClient.GetAsync(endpointBySystem, token);
        var paginatedResponseContent = await responseBySystem.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(paginatedResponseContent);

        //Find the next url that should be the next list elements
        var nextUrl = document.RootElement
            .GetProperty("links")
            .GetProperty("next")
            .GetString();

        var uri = new Uri(nextUrl);

        // Combine the AbsolutePath and Query to get the desired substring
        nextUrl = $"{uri.AbsolutePath}{uri.Query}";
        _outputHelper.WriteLine($"nextUrl: {nextUrl}");

        True(responseBySystem.StatusCode == HttpStatusCode.OK,
            $"Response code wasn't 200, but: {responseBySystem.StatusCode}");

        // Verify status is "New"
        var responseContent = await responseByExternalRef.Content.ReadAsStringAsync();
        Contains("\"status\": \"New\",", responseContent);

        //Verify paginated result
        var paginering = await _platformClient.GetAsync(nextUrl, token);
        True(paginering.StatusCode == HttpStatusCode.OK, $"Did not receive OK, but {paginering.StatusCode}");
    }

    public class SystemUserRequestResponse
    {
        public string? id { get; set; }
        public string? externalRef { get; set; }
        public string? systemId { get; set; }
        public string? status { get; set; }
        public string? partyOrgNo { get; set; }
    }
}