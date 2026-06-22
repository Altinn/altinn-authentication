using System;

namespace Altinn.Platform.Authentication.Exceptions
{
    /// <summary>
    /// Thrown when self-identified user provisioning against register fails, so that the
    /// OIDC sign-in flow aborts with a clear error instead of continuing with an unpopulated user.
    /// </summary>
    public class RegisterUserProvisioningException : Exception
    {
        /// <summary>
        /// Empty constructor.
        /// </summary>
        public RegisterUserProvisioningException() : base()
        {
        }

        /// <summary>
        /// With message.
        /// </summary>
        public RegisterUserProvisioningException(string message) : base(message)
        {
        }

        /// <summary>
        /// With message and inner exception.
        /// </summary>
        public RegisterUserProvisioningException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
