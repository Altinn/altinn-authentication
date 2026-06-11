#nullable enable
using System;
using System.Globalization;
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
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<SelfIdentifiedLinkService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="SelfIdentifiedLinkService"/> class.
        /// </summary>
        public SelfIdentifiedLinkService(
            IUserProfileService userProfileService,
            ISelfIdentifiedLinkTokenService linkTokenService,
            IAltinnNotificationClient notificationClient,
            IOptions<SelfIdentifiedLinkSettings> settings,
            TimeProvider timeProvider,
            ILogger<SelfIdentifiedLinkService> logger)
        {
            _userProfileService = userProfileService;
            _linkTokenService = linkTokenService;
            _notificationClient = notificationClient;
            _settings = settings.Value;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<string?> RequestLinkAsync(string? userName, Guid toPartyUuid, CancellationToken cancellationToken = default)
        {
            SelfIdentifiedLinkTarget? target =
                await _userProfileService.GetSelfIdentifiedLinkTargetAsync(userName, cancellationToken);

            if (target is null)
            {
                // Unknown / inactive / no email. No email is sent and no masked address is returned.
                // The username must not be logged.
                _logger.LogInformation("Self-identified link request had no eligible target; no email sent.");
                return null;
            }

            if (_settings.AccessManagementLinkUrl is null)
            {
                throw new InvalidOperationException("SelfIdentifiedLinkSettings.AccessManagementLinkUrl is not configured.");
            }

            string token = await _linkTokenService.MintAsync(target.PartyUuid, toPartyUuid, cancellationToken);
            string linkUrl = QueryHelpers.AddQueryString(_settings.AccessManagementLinkUrl.AbsoluteUri, "token", token);
            string body = BuildEmailBody(linkUrl);

            // Idempotency id is bucketed to the minute so accidental double-submits (the same from/to
            // within the same minute) de-duplicate to a single email on the Notifications side, while a
            // genuine re-request later still produces a new email.
            string minuteBucket = _timeProvider.GetUtcNow().ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture);
            string idempotencyId = $"si-link_{target.PartyUuid:N}_{toPartyUuid:N}_{minuteBucket}";

            bool sent = await _notificationClient.SendEmailAsync(
                target.Email, _settings.EmailSubject, body, idempotencyId, cancellationToken);

            if (!sent)
            {
                _logger.LogError("Self-identified link email was not accepted by the Notifications service.");
                return null;
            }

            // Only the masked address is returned to the caller; the full email never leaves here.
            return MaskEmail(target.Email);
        }

        /// <summary>
        /// Masks an email address for display, revealing only the first character of the local part and
        /// of the domain name plus the TLD, e.g. <c>rune@gmail.com</c> -&gt; <c>r*****@g****.com</c>.
        /// Uses fixed-length masks so the original lengths are not revealed.
        /// </summary>
        public static string MaskEmail(string email)
        {
            int at = email.IndexOf('@');
            if (at <= 0 || at == email.Length - 1)
            {
                // Not a normal address - don't reveal anything.
                return "*****";
            }

            string local = email[..at];
            string domain = email[(at + 1)..];
            string maskedLocal = local[0] + new string('*', 5);

            int dot = domain.LastIndexOf('.');
            string maskedDomain = dot <= 0
                ? domain[0] + new string('*', 4)
                : domain[0] + new string('*', 4) + domain[dot..];

            return $"{maskedLocal}@{maskedDomain}";
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
