using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Altinn.Authentication.Core.Clients.Interfaces;
using Altinn.Common.AccessTokenClient.Services;
using Altinn.Platform.Authentication.Core.Models.Profile;
using Microsoft.Extensions.Logging;

namespace Altinn.Authentication.Integration.Clients;

/// <summary>
/// Proxy to register's permanent self-identified-user provisioning endpoint
/// (<c>POST /register/api/v2/internal/parties/self-identified</c>).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class RegisterUserProvisioningClient(
    HttpClient httpClient,
    ILogger<RegisterUserProvisioningClient> logger,
    IAccessTokenGenerator accessTokenGenerator) : IRegisterUserProvisioningClient
{
    private const string RelativeEndpointPath = "parties/self-identified";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

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

            var accessToken = accessTokenGenerator.GenerateAccessToken("platform", "authentication");
            httpRequest.Headers.Add("PlatformAccessToken", accessToken);

            using var response = await httpClient.SendAsync(httpRequest, cancellationToken);

            if (response.StatusCode == HttpStatusCode.OK)
            {
                return await response.Content.ReadFromJsonAsync<SelfIdentifiedUserProvisioningResponse>(
                    JsonOptions,
                    cancellationToken);
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogError(
                "Register self-identified provisioning failed with statuscode {StatusCode}: {Body}",
                response.StatusCode,
                body);
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Register self-identified provisioning threw an unhandled exception");
            return null;
        }
    }
}
