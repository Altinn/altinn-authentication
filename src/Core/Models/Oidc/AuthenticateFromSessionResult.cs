namespace Altinn.Platform.Authentication.Core.Models.Oidc
{
    public sealed class AuthenticateFromSessionResult
    {
        /// <summary>
        /// 
        /// </summary>
        public AuthenticateFromSessionResultKind Kind { get; init; }

        public IEnumerable<CookieInstruction> Cookies { get; init; } = [];

        /// <summary>
        /// The authentication context class reference (acr) of the resolved session, when one was found.
        /// Used to decide whether the session satisfies a requested authentication level (step-up).
        /// </summary>
        public string? Acr { get; init; }
    }
}
