using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altinn.Platform.Authentication.Core.Models.AccessPackages
{
    /// <summary>
    /// Model for an Access Package
    /// </summary>
    public record AccessPackage
    {
        /// <summary>
        /// The unique identifier for the Access Package
        /// </summary>
        public string Urn { get; set; }
    }
}
