using System;
using System.Collections.Generic;
using Altinn.Platform.Authentication.Core.Models;
using Altinn.Platform.Authentication.Core.Models.AccessPackages;

namespace Altinn.Platform.Authentication.Model
{
    /// <summary>
    /// Client information, containing information about the system user and the
    /// associated client organization.
    /// </summary>
    public class SystemUserInfo
    {
        /// <summary>
        /// Gets or sets the unique identifier of the system user.
        /// </summary>
        public Guid SystemUserId { get; set; }

        /// <summary>
        /// Gets or sets the organization number associated with the system user who owns the entity.
        /// </summary>
        public string SystemUserOwnerOrganizationNumber { get; set; }
        
        /// <summary>
        /// Gets or sets the collection of clients associated with the system user.
        /// </summary>
        public List<ClientInfo> Clients { get; set; }

        /// <summary>
        /// Gets or sets the collection of access packages associated with the systemuser of type standard.
        /// </summary>
        public List<AccessPackage> AccessPackages { get; set; }

        /// <summary>
        /// Gets or sets the collection of rights associated with the systemuser of type standard.
        /// </summary>
        public List<Right> Rights { get; set; }
    }

    /// <summary>
    /// Represents information about a client, including their organization number and name.
    /// </summary>
    /// <remarks>This class is typically used to store and transfer basic details about a client
    /// organization.</remarks>
    public class ClientInfo
    {
        /// <summary>
        /// Gets or sets the organization number associated with the client.
        /// </summary>
        public string ClientOrganizationNumber { get; set; }
        
        /// <summary>
        /// Gets or sets the name of the client organization.
        /// </summary>
        public string ClientOrganizationName { get; set; }

        /// <summary>
        /// Gets or sets the collection of access packages associated with the systemuser and the client.
        /// </summary>
        public List<AccessPackage> AccessPackages { get; set; }
    }
}
