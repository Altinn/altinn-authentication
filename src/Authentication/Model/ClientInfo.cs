using System;
using System.Collections.Generic;
using Altinn.Platform.Authentication.Core.Models;
using Altinn.Platform.Authentication.Core.Models.AccessPackages;
using static Altinn.Platform.Authentication.Core.Models.SystemUsers.ClientDto;

namespace Altinn.Platform.Authentication.Model
{
    /// <summary>
    /// Represents information about a client, including their organization number and name.
    /// </summary>
    public class ClientInfo
    {
        /// <summary>
        /// Gets or sets the unique identifier for the client.
        /// </summary>
        public Guid ClientId { get; set; }

        /// <summary>
        /// Gets or sets the organization number associated with the client.
        /// </summary>
        public string ClientOrganizationNumber { get; set; }
        
        /// <summary>
        /// Gets or sets the name of the client organization.
        /// </summary>
        public string ClientOrganizationName { get; set; }

        /// <summary>
        /// Gets or sets the list of access packages associated with client roles.
        /// </summary>
        public List<ClientRoleAccessPackages> Access { get; set; } = [];
    }
}
