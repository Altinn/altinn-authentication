namespace Altinn.Platform.Authentication.Core.Models.Oidc
{
    public sealed class AuthenticateFromAltinn2TicketResult
    {
        /// <summary>
        /// 
        /// </summary>
        public AuthenticateFromAltinn2TicketResultKind Kind { get; init; }

        public IEnumerable<CookieInstruction> Cookies { get; init; } = [];
    }
}
