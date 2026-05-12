using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Altinn.Authentication.Core.Clients.Interfaces;
using Altinn.Authentication.Integration.Configuration;
using Altinn.Common.AccessTokenClient.Services;
using Altinn.Platform.Authentication.Core.Models.Profile;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Authentication.Integration.Clients;

/// <summary>
/// Proxy to register's permanent self-identified-user provisioning endpoint
/// (<c>POST /register/api/v2/internal/users/self-identified</c>).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class RegisterUserProvisioningClient : IRegisterUserProvisioningClient
{
    private const string RelativeEndpointPath = "users/self-identified";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _client;
    private readonly ILogger<RegisterUserProvisioningClient> _logger;
    private readonly IAccessTokenGenerator _accessTokenGenerator;

    /// <summary>
    /// Initializes a new instance of the <see cref="RegisterUserProvisioningClient"/> class.
    /// </summary>
    public RegisterUserProvisioningClient(
        HttpClient httpClient,
        ILogger<RegisterUserProvisioningClient> logger,
        IOptions<PlatformSettings> platformSettings,
        IAccessTokenGenerator accessTokenGenerator)
    {
        var baseAddress = platformSettings.Value.ApiRegisterInternalEndpoint
            ?? throw new InvalidOperationException("PlatformSettings.ApiRegisterInternalEndpoint is not configured.");
        httpClient.BaseAddress = new Uri(baseAddress);

        if (!string.IsNullOrEmpty(platformSettings.Value.SubscriptionKeyHeaderName)
            && !string.IsNullOrEmpty(platformSettings.Value.SubscriptionKey))
        {
            httpClient.DefaultRequestHeaders.Add(
                platformSettings.Value.SubscriptionKeyHeaderName,
                platformSettings.Value.SubscriptionKey);
        }

        _client = httpClient;
        _logger = logger;
        _accessTokenGenerator = accessTokenGenerator;
    }

    /// <inheritdoc/>
    public async Task<SelfIdentifiedUserProvisioningResponse?> GetOrCreateUser(
        SelfIdentifiedUserProvisioningRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, RelativeEndpointPath)
            {
                Content = JsonContent.Create(request, options: JsonOptions),
            };

            var accessToken = _accessTokenGenerator.GenerateAccessToken("platform", "authentication");
            httpRequest.Headers.Add("PlatformAccessToken", accessToken);

            using var response = await _client.SendAsync(httpRequest, cancellationToken);

            if (response.StatusCode == HttpStatusCode.OK)
            {
                return await response.Content.ReadFromJsonAsync<SelfIdentifiedUserProvisioningResponse>(
                    JsonOptions,
                    cancellationToken);
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "Register self-identified provisioning failed with statuscode {StatusCode}: {Body}",
                response.StatusCode,
                body);
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Register self-identified provisioning threw an unhandled exception");
            return null;
        }
    }
}
