namespace Altinn.Platform.Authentication.Core.Models.Oidc
{
    public sealed class AuthenticateFromSessionResult
    {
        /// <summary>
        /// 
        /// </summary>
        public AuthenticateFromSessionResultKind Kind { get; init; }

        public IEnumerable<CookieInstruction> Cookies { get; init; } = [];
    }
}
