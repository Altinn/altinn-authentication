using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Core.Models.ResourceRegistry;
using Altinn.Platform.Authentication.Core.Models.Rights;

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

        /// <summary>
        /// Gets a list of policyRightDTOs from a resourceId
        /// </summary>
        /// <param name="resourceId"></param>
        /// <returns>List of PolicyRightDTOs</returns>
        Task<List<PolicyRightsDTO>> GetRights(string resourceId);
    }
}
