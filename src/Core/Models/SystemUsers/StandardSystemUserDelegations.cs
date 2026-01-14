using Altinn.Platform.Authentication.Core.Models.AccessPackages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Altinn.Platform.Authentication.Core.Models.SystemUsers
{
    public class StandardSystemUserDelegations
    {
        [JsonPropertyName("systemUserId")]
        public Guid SystemUserId { get; set; }

        [JsonPropertyName("rights")]
        public List<Right> Rights { get; set; } = new List<Right>();

        [JsonPropertyName("accessPackages")]
        public List<AccessPackage> AccessPackages { get; set; } = new List<AccessPackage>();

    }
}
