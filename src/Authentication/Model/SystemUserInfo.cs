using System;

namespace Altinn.Platform.Authentication.Model
{
    /// <summary>
    /// information about a system user.
    /// </summary>
    public class SystemUserInfo
    {
        /// <summary>
        /// Gets or sets the unique identifier for the system user.
        /// </summary>
        public Guid SystemUserId { get; set; }

        /// <summary>
        /// Gets or sets the organization associated with the system user.
        /// </summary>
        public string SystemUserOwnerOrg { get; set; }  
    }
}
