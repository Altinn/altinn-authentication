namespace Altinn.Platform.Authentication.Core.Models.Oidc
{
    public sealed class AuthenticateFromAltinn2TicketResult
    {
        /// <summary>
        /// 
        /// </summary>
        public AuthenticateFromAltinn2TicketResultKind Kind { get; init; }

        public IEnumerable<CookieInstruction> Cookies { get; init; } = [];

        /// <summary>
        /// The authentication context class reference (acr) of the session created from the Altinn 2 ticket.
        /// Used to decide whether the session satisfies a requested authentication level (step-up).
        /// </summary>
        public string? Acr { get; init; }
    }
}
