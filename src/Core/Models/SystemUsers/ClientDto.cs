using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altinn.Platform.Authentication.Core.Models.SystemUsers
{
    /// <summary>
    /// Model representing a connected client party, meaning a party which has been authorized for one or more accesses, either directly or through role(s), access packages, resources or resource instances.
    /// Model can be used both to represent a connection received from another party or a connection provided to another party.
    /// </summary>
    public class ClientDto
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ClientDto"/> class.
        /// </summary>
        public ClientDto()
        {
        }

        /// <summary>
        /// Gets or sets the party
        /// </summary>
        public ClientParty Party { get; set; }

        /// <summary>
        /// Gets or sets a collection of all access information for the client 
        /// </summary>
        public List<ClientRoleAccessPackages> Access { get; set; } = [];

        /// <summary>
        /// Composite Key instances
        /// </summary>
        public class ClientParty
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="ClientParty"/> class.
            /// </summary>
            public ClientParty()
            {
            }

            /// <summary>
            /// Gets or sets the universally unique identifier of the party
            /// </summary>
            public Guid Id { get; set; }

            /// <summary>
            /// Gets or sets the name of the party
            /// </summary>
            public string Name { get; set; }

            /// <summary>
            /// Gets the organization number if the party is an organization
            /// </summary>
            public string OrganizationNumber { get; set; }

            /// <summary>
            /// Gets or sets the unit type if the party is an organization
            /// </summary>
            public string UnitType { get; set; }

            /* ToBe Added in the future maybe
            /// <summary>
            /// Gets or sets the type of party
            /// </summary>
            public string Type { get; set; }

            /// <summary>
            /// Gets or sets whether this party is marked as deleted in the Central Coordinating Register for Legal Entities
            /// </summary>
            public bool IsDeleted { get; set; }

            /// <summary>
            /// Gets or sets a set of subunits of this party, which the authorized subject also has some access to.
            /// </summary>
            public List<ClientParty> Subunits { get; set; } = [];
            */
        }

        /// <summary>
        /// Composite Key instances
        /// </summary>
        public class ClientRoleAccessPackages
        {
            /// <summary>
            /// Role
            /// </summary>
            public string Role { get; set; }

            /// <summary>
            /// Packages
            /// </summary>
            public string[] Packages { get; set; }
        }
    }
}
