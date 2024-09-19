using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altinn.Platform.Authentication.Core.Models.Rights
{
    /// <summary>
    /// Request model for a list of all rights for a specific resource, that a user can delegate from a given reportee to a given recipient.
    /// </summary>
    public class DelegationCheckRequest
    {
        /// <summary>
        /// Gets or sets the set of Attribute Id and Attribute Value for identifying the resource of the rights to be checked
        /// </summary>
        [Required]
        public List<AttributePair> Resource { get; set; } = [];
    }
}