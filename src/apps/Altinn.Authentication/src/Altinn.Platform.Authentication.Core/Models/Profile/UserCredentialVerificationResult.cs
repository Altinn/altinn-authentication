namespace Altinn.Platform.Authentication.Core.Models.Profile
{
    public class UserCredentialVerificationResult
    {
        public UserProfile? UserProfile { get; set; }

        public bool IsLocked { get; set; }

        public bool WrongUserType { get; set; }
    }
}
