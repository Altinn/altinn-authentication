#nullable enable
using System;
using System.Text.Json.Serialization;

namespace Altinn.Authentication.Integration.Notification.Models
{
    /// <summary>
    /// Request body for the Altinn Notifications order-chain endpoint
    /// (<c>POST notifications/api/v1/future/orders</c>). Mirrors the platform's
    /// <c>NotificationOrderChainRequestExt</c> contract; only the fields needed to send a direct email
    /// are modelled here (issue #2035).
    /// </summary>
    public sealed class NotificationOrderChainRequest
    {
        /// <summary>
        /// Gets or sets the sender-defined idempotency id used to de-duplicate retries.
        /// </summary>
        [JsonPropertyName("idempotencyId")]
        public required string IdempotencyId { get; set; }

        /// <summary>
        /// Gets or sets an optional sender reference, for correlating with the sender's own records.
        /// </summary>
        [JsonPropertyName("sendersReference")]
        public string? SendersReference { get; set; }

        /// <summary>
        /// Gets or sets the earliest delivery time. Left unset for immediate delivery (the platform
        /// defaults to the current time).
        /// </summary>
        [JsonPropertyName("requestedSendTime")]
        public DateTime? RequestedSendTime { get; set; }

        /// <summary>
        /// Gets or sets the recipient of the notification.
        /// </summary>
        [JsonPropertyName("recipient")]
        public required NotificationRecipient Recipient { get; set; }
    }

    /// <summary>
    /// Recipient container. Only the direct-email channel is modelled here.
    /// </summary>
    public sealed class NotificationRecipient
    {
        /// <summary>
        /// Gets or sets the direct-email recipient. Using this (rather than a person/organization
        /// identifier) sends straight to the address with no contact-register lookup.
        /// </summary>
        [JsonPropertyName("recipientEmail")]
        public required RecipientEmail RecipientEmail { get; set; }
    }

    /// <summary>
    /// A direct email recipient: a specific address plus its email settings.
    /// </summary>
    public sealed class RecipientEmail
    {
        /// <summary>
        /// Gets or sets the destination email address.
        /// </summary>
        [JsonPropertyName("emailAddress")]
        public required string EmailAddress { get; set; }

        /// <summary>
        /// Gets or sets the email content and delivery settings.
        /// </summary>
        [JsonPropertyName("emailSettings")]
        public required EmailSendingOptions Settings { get; set; }
    }

    /// <summary>
    /// Email content and delivery settings.
    /// </summary>
    public sealed class EmailSendingOptions
    {
        /// <summary>
        /// Gets or sets an optional sender address. Left unset to use the platform default sender.
        /// </summary>
        [JsonPropertyName("senderEmailAddress")]
        public string? SenderEmailAddress { get; set; }

        /// <summary>
        /// Gets or sets the email subject.
        /// </summary>
        [JsonPropertyName("subject")]
        public required string Subject { get; set; }

        /// <summary>
        /// Gets or sets the email body.
        /// </summary>
        [JsonPropertyName("body")]
        public required string Body { get; set; }

        /// <summary>
        /// Gets or sets the body content type.
        /// </summary>
        [JsonPropertyName("contentType")]
        public EmailContentType ContentType { get; set; } = EmailContentType.Plain;

        /// <summary>
        /// Gets or sets when the email may be delivered.
        /// </summary>
        [JsonPropertyName("sendingTimePolicy")]
        public SendingTimePolicy SendingTimePolicy { get; set; } = SendingTimePolicy.Anytime;
    }

    /// <summary>
    /// Email body content type. Integer values match the platform contract.
    /// </summary>
    public enum EmailContentType
    {
        /// <summary>Plain text.</summary>
        Plain = 0,

        /// <summary>HTML.</summary>
        Html = 1,
    }

    /// <summary>
    /// Delivery-time policy. Integer values match the platform contract.
    /// </summary>
    public enum SendingTimePolicy
    {
        /// <summary>Deliver at any time of day (immediate).</summary>
        Anytime = 1,

        /// <summary>Hold for business hours (08:00-17:00 CET).</summary>
        Daytime = 2,
    }
}
