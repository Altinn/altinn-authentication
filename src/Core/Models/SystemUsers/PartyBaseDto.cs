using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altinn.Platform.Authentication.Core.Models.SystemUsers
{
    public class PartyBaseDto
    {
        /// <summary>
        /// Gets or sets the unique identifier for the party.
        /// </summary>
        public Guid PartyUuid { get; set; }

        /// <summary>
        /// Gets or sets the type of the PArty Uuid.
        /// </summary>
        public required string EntityType { get; set; }

        /// <summary>
        /// Gets or sets the type of the entity variant.
        /// </summary>
        public required string EntityVariantType { get; set; }

        /// <summary>
        /// Gets or sets the display name.
        /// </summary>
        public required string DisplayName { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the user who created the entity.
        /// </summary>
        public Guid? CreatedBy { get; set; }
    }
}
