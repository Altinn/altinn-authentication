using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altinn.Platform.Authentication.Core.Models.AccessPackages
{
    /// <summary>
    /// Define the types of Providers
    /// </summary>
    public class ProviderType
    {
        /// <summary>
        /// Provider type identifier
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Provider type name
        /// </summary>
        public string Name { get; set; }
    }
}
