namespace Altinn.Platform.Authentication.Core.Models.Profile
{
    /// <summary>
    /// Username and password for a self-identified user, used when validating credentials via basic authentication.
    /// </summary>
    public class SiUserCredentials
    {
        /// <summary>
        /// The self-identified user's username.
        /// </summary>
        public required string UserName { get; set; }

        /// <summary>
        /// The self-identified user's password.
        /// </summary>
        public required string Password { get; set; }
    }
}
