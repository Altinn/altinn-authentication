using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altinn.Platform.Authentication.Core.Models.Rights
{
    /// <summary>
    /// Response model for the result of a delegation of one or more rights to a recipient.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class RightsDelegationResponseExternal
    {
        /// <summary>
        /// Gets or sets a set of Attribute Id and Attribute Value identfying the single person or entity receiving the delegation
        /// </summary>
        [Required]
        public List<AttributeMatchExternal> To { get; set; }

        /// <summary>
        /// Gets or sets a list of rights delegation results with status wheter the delegation was successfully delegated to the recipient.
        /// </summary>
        [Required]
        public List<RightDelegationResultExternal> RightDelegationResults { get; set; }
    }
}
