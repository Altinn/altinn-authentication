namespace Altinn.Platform.Authentication.Core.Models.Oidc
{
    public class AuthenticateFromSessionInput
    {
        /// <summary>
        /// The session handle used to identify the user's session.
        /// </summary>
        public required string SessionHandle { get; init; }
    }
}
