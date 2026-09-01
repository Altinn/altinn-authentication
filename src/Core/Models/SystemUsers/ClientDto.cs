namespace Altinn.Platform.Authentication.Core.Models.SystemUsers
{
    /// <summary>
    /// Container for <see cref="ClientRoleAccessPackages"/>, which callers reach through
    /// "using static ... ClientDto;". The client-party model that used to live alongside it was
    /// removed once the v2 client-delegation payload (ClientDelegationDto) replaced that path.
    /// </summary>
    public class ClientDto
    {
        /// <summary>
        /// The role a client is accessed through, and the access packages held via that role
        /// </summary>
        public class ClientRoleAccessPackages
        {
            /// <summary>
            /// Role
            /// </summary>
            public required string Role { get; set; }

            /// <summary>
            /// Packages
            /// </summary>
            public required string[] Packages { get; set; }
        }
    }
}
