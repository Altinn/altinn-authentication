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
                string content = File.ReadAllText(dataFileName);
                return (ServiceResource)JsonSerializer.Deserialize(content, typeof(ServiceResource), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }

            if (resourceId == "ske-krav-og-betalinger-2")
            {
                dataFileName = "Data/ResourceRegistry/kravogbetaling.json";
                string content = File.ReadAllText(dataFileName);
                return (ServiceResource)JsonSerializer.Deserialize(content, typeof(ServiceResource), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }

            if (resourceId == "ske-krav-og-betalinger-3")
            {
                dataFileName = "Data/ResourceRegistry/kravogbetaling.json";
                string content = File.ReadAllText(dataFileName);
                return (ServiceResource)JsonSerializer.Deserialize(content, typeof(ServiceResource), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }

            if (resourceId == "ske-krav-og-betalinger-subres")
            {
                dataFileName = "Data/ResourceRegistry/kravogbetaling.json";
                string content = File.ReadAllText(dataFileName);
                return (ServiceResource)JsonSerializer.Deserialize(content, typeof(ServiceResource), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }

            if (resourceId == "app_ttd_endring-av-navn-v2")
            {
                dataFileName = "Data/ResourceRegistry/app_endringavnavnv2.json";
                string content = File.ReadAllText(dataFileName);
                return (ServiceResource)JsonSerializer.Deserialize(content, typeof(ServiceResource), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }

            return null;
        }

        public async Task<List<PolicyRightsDTO>> GetRights(string resourceId)
        {
            string dataFileName = string.Empty;
            if (resourceId == "ske-krav-og-betalinger")
            {
                List<PolicyRightsDTO> res = [];
                dataFileName = "Data/ResourceRegistry/policyrightDTO-kravogbetaling.json";
                string content = string.Empty;
                try
                {
                    content = File.ReadAllText(dataFileName);
                    res = (List<PolicyRightsDTO>) JsonSerializer.Deserialize(content, typeof(List<PolicyRightsDTO>), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
                catch (Exception ex)
                {
                    string message = $"Error reading file {dataFileName}";
                    throw;
                }                
                                
                return res;
            }

            if (resourceId == "ske-krav-og-betalinger-2")
            {
                List<PolicyRightsDTO> res = [];
                dataFileName = "Data/ResourceRegistry/policyrightDTO-kravogbetaling2.json";
                string content = string.Empty;
                try
                {
                    content = File.ReadAllText(dataFileName);
                    res = (List<PolicyRightsDTO>)JsonSerializer.Deserialize(content, typeof(List<PolicyRightsDTO>), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
                catch (Exception ex)
                {
                    string message = $"Error reading file {dataFileName}";
                    throw;
                }

                return res;
            }

            return null;
        }
    }
}
