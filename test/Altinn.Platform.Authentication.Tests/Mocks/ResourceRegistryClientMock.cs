using System;
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
            }

            if (resourceId == "ske-krav-og-betalinger-2")
            {
                dataFileName = "Data/ResourceRegistry/kravogbetaling.json";
            }

            if (resourceId == "ske-krav-og-betalinger-3")
            {
                dataFileName = "Data/ResourceRegistry/kravogbetaling.json";
            }

            if (resourceId == "ske-krav-og-betalinger-subres")
            {
                dataFileName = "Data/ResourceRegistry/kravogbetaling.json";              
            }

            if (resourceId == "app_ttd_endring-av-navn-v2")
            {
                dataFileName = "Data/ResourceRegistry/app_endringavnavnv2.json";
            }

            if (resourceId == "ttd-am-k6")
            {
                dataFileName = "Data/ResourceRegistry/ttd-am-k6.json";
            }

            if (dataFileName != string.Empty)
            {
                string content = File.ReadAllText(dataFileName);
                return JsonSerializer.Deserialize<ServiceResource>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }

            return null;
        }

        public async Task<List<PolicyRightsDTO>> GetRights(string resourceId)
        {
            if (resourceId == "ske-krav-og-betalinger")
            {
                string content = File.ReadAllText("Data/ResourceRegistry/policyrightDTO-kravogbetaling.json");
                return JsonSerializer.Deserialize<List<PolicyRightsDTO>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
            }

            if (resourceId == "ske-krav-og-betalinger-2")
            {
                string content = File.ReadAllText("Data/ResourceRegistry/policyrightDTO-kravogbetaling2.json");
                return JsonSerializer.Deserialize<List<PolicyRightsDTO>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
            }

            return null!; // interface declares a non-nullable list, but the mock mirrors the null returned for unknown resources
        }
    }
}
