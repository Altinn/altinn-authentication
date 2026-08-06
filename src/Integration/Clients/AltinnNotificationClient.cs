#nullable enable
using System;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Authentication.Core.Clients.Interfaces;
using Altinn.Authentication.Integration.Notification.Models;
using Altinn.Common.AccessTokenClient.Services;
using Microsoft.Extensions.Logging;

namespace Altinn.Authentication.Integration.Clients
{
    /// <summary>
    /// Client for the Altinn Notifications platform API (issue #2035). Sends a direct email via the
    /// order-chain endpoint with a <c>PlatformAccessToken</c>, mirroring how access-management sends
    /// its notifications.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed class AltinnNotificationClient : IAltinnNotificationClient
    {
        private const string RelativeEndpointPath = "future/orders";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        private readonly HttpClient _httpClient;
        private readonly ILogger<AltinnNotificationClient> _logger;
        private readonly IAccessTokenGenerator _accessTokenGenerator;

        /// <summary>
        /// Initializes a new instance of the <see cref="AltinnNotificationClient"/> class.
        /// </summary>
        public AltinnNotificationClient(
            HttpClient httpClient,
            ILogger<AltinnNotificationClient> logger,
            IAccessTokenGenerator accessTokenGenerator)
        {
            _httpClient = httpClient;
            _logger = logger;
            _accessTokenGenerator = accessTokenGenerator;
        }

        /// <inheritdoc/>
        public async Task<bool> SendEmailAsync(
            string emailAddress,
            string subject,
            string htmlBody,
            string idempotencyId,
            CancellationToken cancellationToken = default)
        {
            NotificationOrderChainRequest request = new()
            {
                IdempotencyId = idempotencyId,
                SendersReference = idempotencyId,

                // RequestedSendTime left unset -> the platform defaults to "now" (immediate delivery).
                Recipient = new NotificationRecipient
                {
                    RecipientEmail = new RecipientEmail
                    {
                        EmailAddress = emailAddress,
                        Settings = new EmailSendingOptions
                        {
                            Subject = subject,
                            Body = htmlBody,
                            ContentType = EmailContentType.Html,
                            SendingTimePolicy = SendingTimePolicy.Anytime,
                        },
                    },
                },
            };

            try
            {
                using HttpRequestMessage httpRequest = new(HttpMethod.Post, RelativeEndpointPath)
                {
                    Content = JsonContent.Create(request, options: JsonOptions),
                };

                string accessToken = _accessTokenGenerator.GenerateAccessToken("platform", "authentication");
                httpRequest.Headers.Add("PlatformAccessToken", accessToken);

                using HttpResponseMessage response = await _httpClient.SendAsync(httpRequest, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    return true;
                }

                string body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError(
                    "Altinn Notifications send failed with statuscode {StatusCode}: {Body}",
                    response.StatusCode,
                    body);
                return false;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Altinn Notifications send threw an unhandled exception");
                return false;
            }
        }
    }
}
