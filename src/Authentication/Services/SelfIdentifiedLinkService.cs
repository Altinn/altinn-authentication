#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Authentication.Core.Clients.Interfaces;
using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Core.Models.Profile;
using Altinn.Platform.Authentication.Services.Interfaces;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Platform.Authentication.Services
{
    /// <summary>
    /// Orchestrates the self-identified account-link "request email" step (issue #2035). The SI email
    /// address and the minted token never leave this component: the email goes straight to the SI
    /// mailbox via Altinn Notifications, and the caller only learns that the request was accepted.
    /// </summary>
    public class SelfIdentifiedLinkService : ISelfIdentifiedLinkService
    {
        private readonly IUserProfileService _userProfileService;
        private readonly ISelfIdentifiedLinkTokenService _linkTokenService;
        private readonly IAltinnNotificationClient _notificationClient;
        private readonly SelfIdentifiedLinkSettings _settings;
        private readonly ILogger<SelfIdentifiedLinkService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="SelfIdentifiedLinkService"/> class.
        /// </summary>
        public SelfIdentifiedLinkService(
            IUserProfileService userProfileService,
            ISelfIdentifiedLinkTokenService linkTokenService,
            IAltinnNotificationClient notificationClient,
            IOptions<SelfIdentifiedLinkSettings> settings,
            ILogger<SelfIdentifiedLinkService> logger)
        {
            _userProfileService = userProfileService;
            _linkTokenService = linkTokenService;
            _notificationClient = notificationClient;
            _settings = settings.Value;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task RequestLinkAsync(string userName, Guid toPartyUuid, CancellationToken cancellationToken = default)
        {
            SelfIdentifiedLinkTarget? target =
                await _userProfileService.GetSelfIdentifiedLinkTargetAsync(userName, cancellationToken);

            if (target is null)
            {
                // Unknown / inactive / no email. Silent no-op - the caller responds identically so the
                // existence of the username is not revealed. The username must not be logged.
                _logger.LogInformation("Self-identified link request had no eligible target; no email sent.");
                return;
            }

            string token = await _linkTokenService.MintAsync(target.PartyUuid, toPartyUuid, cancellationToken);
            string linkUrl = QueryHelpers.AddQueryString(_settings.AccessManagementLinkUrl, "token", token);
            string body = BuildEmailBody(linkUrl);
            string idempotencyId = $"si-link_{target.PartyUuid:N}_{toPartyUuid:N}_{Guid.NewGuid():N}";

            bool sent = await _notificationClient.SendEmailAsync(
                target.Email, _settings.EmailSubject, body, idempotencyId, cancellationToken);

            if (!sent)
            {
                _logger.LogError("Self-identified link email was not accepted by the Notifications service.");
            }
        }

        private static string BuildEmailBody(string linkUrl)
        {
            // Norwegian template; subject/body localization is a tracked follow-up in #2035.
            return $"""
                <p>Hei,</p>
                <p>Det er bedt om å koble denne selvidentifiserte Altinn-brukeren til en innlogget bruker.</p>
                <p>Hvis dette var deg, følg lenken under for å fullføre koblingen. Lenken er gyldig en kort stund.</p>
                <p><a href="{linkUrl}">Fullfør koblingen i Altinn</a></p>
                <p>Hvis du ikke ba om dette, kan du se bort fra denne e-posten.</p>
                <p>Med vennlig hilsen,<br>Altinn</p>
                """;
        }
    }
}
