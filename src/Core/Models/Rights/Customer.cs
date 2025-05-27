using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altinn.Platform.Authentication.Core.Models.Rights
{
    public class Customer
    {
        /// <summary>
        /// UUid of the party
        /// </summary>
        public required Guid PartyUuid { get; set; }

        /// <summary>
        /// party id
        /// </summary>
        ////public required string PartyId { get; set; }
        
        /// <summary>
        /// Display name of the party
        /// </summary>
        public required string DisplayName { get; set; }

        /// <summary>
        /// Organisation number
        /// </summary>
        public required string OrganizationIdentifier { get; set; }
    }
}
