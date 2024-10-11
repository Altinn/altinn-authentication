using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Core.Models.ResourceRegistry;

namespace Altinn.Platform.Authentication.Integration.ResourceRegister
{
    public interface IResourceRegistryClient
    {
        /// <summary>
        /// Gets a resource by resource id
        /// </summary>
        /// <param name="resourceId">the resource id</param>
        /// <returns></returns>
        Task<ServiceResource?> GetResource(string resourceId);
    }
}
