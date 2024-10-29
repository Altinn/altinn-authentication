using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Core.Models.ResourceRegistry;
using Altinn.Platform.Authentication.Core.Models.Rights;
using Altinn.Platform.Authentication.Integration.ResourceRegister;

namespace Altinn.Platform.Authentication.Tests.Mocks
{
    public class ResourceRegistryClientMock : IResourceRegistryClient
    {
        public async Task<ServiceResource?> GetResource(string resourceId)
        {
            string dataFileName = string.Empty;
            if (resourceId == "ske-krav-og-betalinger")
            {
                dataFileName = "Data/ResourceRegistry/kravogbetaling.json";
                string content = File.ReadAllText(dataFileName);
                return (ServiceResource)JsonSerializer.Deserialize(content, typeof(ServiceResource), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }

            return null;
        }

        public Task<List<PolicyRightsDTO>> GetRights(string resourceId)
        {
            throw new System.NotImplementedException();
        }
    }
}
