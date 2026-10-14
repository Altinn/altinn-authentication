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
        // Access-management frontend landing route that consumes the link token. Built from
        // GeneralSettings.HostName the same way the system-user confirm URLs are
        // (see RequestSystemUserController/ChangeRequestSystemUserController), so the AM frontend
        // host is configured in exactly one place (HostName) per environment.
        private const string LandingUrlPrefix = "https://am.ui.";
        private const string LandingUrlPath = "/accessmanagement/ui/altinn2account";

        private readonly IUserProfileService _userProfileService;
        private readonly ISelfIdentifiedLinkTokenService _linkTokenService;
        private readonly IAltinnNotificationClient _notificationClient;
        private readonly SelfIdentifiedLinkSettings _settings;
        private readonly GeneralSettings _generalSettings;
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
            IOptions<GeneralSettings> generalSettings,
            TimeProvider timeProvider,
            ILogger<SelfIdentifiedLinkService> logger)
        {
            _userProfileService = userProfileService;
            _linkTokenService = linkTokenService;
            _notificationClient = notificationClient;
            _settings = settings.Value;
            _generalSettings = generalSettings.Value;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<string?> RequestLinkAsync(string? userName, Guid toPartyUuid, string lang = "", CancellationToken cancellationToken = default)
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

            if (string.IsNullOrWhiteSpace(_generalSettings.HostName))
            {
                throw new InvalidOperationException("GeneralSettings.HostName is not configured; cannot build the access-management link URL.");
            }

            string token = await _linkTokenService.MintAsync(target.PartyUuid, toPartyUuid, cancellationToken);
            string baseUrl = $"{LandingUrlPrefix}{_generalSettings.HostName.Trim()}{LandingUrlPath}";
            string linkUrl = QueryHelpers.AddQueryString(baseUrl, "token", token);
            string body = BuildEmailBody(linkUrl, lang);
            string subject = _settings.EmailSubject.TryGetValue(lang, out var s) ? s : _settings.EmailSubject["no_nb"];

            // Idempotency id is bucketed to the minute so accidental double-submits (the same from/to
            // within the same minute) de-duplicate to a single email on the Notifications side, while a
            // genuine re-request later still produces a new email.
            string minuteBucket = _timeProvider.GetUtcNow().ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture);
            string idempotencyId = $"si-link_{target.PartyUuid:N}_{toPartyUuid:N}_{minuteBucket}";

            bool sent = await _notificationClient.SendEmailAsync(
                target.Email, subject, body, idempotencyId, cancellationToken);

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

        private static string BuildEmailBody(string linkUrl, string lang)
        {
            return lang switch
            {
                "no_nn" => $"""
                    <p>Hei,</p>
                    <p>Det er bedt om å kople denne sjølvidentifiserte Altinn-brukaren til ein innlogga brukar.</p>
                    <p>Viss dette var deg, følg lenkja under for å fullføre koplinga. Lenkja er gyldig ei kort stund.</p>
                    <p><a href="{linkUrl}">Fullfør koplinga i Altinn</a></p>
                    <p>Viss du ikkje bad om dette, kan du sjå bort frå denne e-posten.</p>
                    <p>Med venleg helsing,<br>Altinn</p>
                    """,

                "en" => $"""
                    <p>Hello,</p>
                    <p>A request has been made to link this self-identified Altinn user to a logged-in user.</p>
                    <p>If this was you, follow the link below to complete the linking. The link is valid for a short time.</p>
                    <p><a href="{linkUrl}">Complete the linking in Altinn</a></p>
                    <p>If you did not request this, you can disregard this email.</p>
                    <p>Best regards,<br>Altinn</p>
                    """,

                // Default: Norwegian Bokmål
                _ => $"""
                    <p>Hei,</p>
                    <p>Det er bedt om å koble denne selvidentifiserte Altinn-brukeren til en innlogget bruker.</p>
                    <p>Hvis dette var deg, følg lenken under for å fullføre koblingen. Lenken er gyldig en kort stund.</p>
                    <p><a href="{linkUrl}">Fullfør koblingen i Altinn</a></p>
                    <p>Hvis du ikke ba om dette, kan du se bort fra denne e-posten.</p>
                    <p>Med vennlig hilsen,<br>Altinn</p>
                    """
            };
        }
    }
}
