using System.Collections.Generic;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Services.Interfaces;
using Microsoft.IdentityModel.Tokens;

namespace Altinn.Platform.Authentication.Services
{
    /// <inheritdoc />
    public class SigningKeysRetriever : ISigningKeysRetriever
    {
        /// <inheritdoc />
        public async Task<ICollection<SecurityKey>> GetSigningKeys(string url)
        {
            return (await Altinn.Platform.Authentication.Helpers.ConfigurationMangerHelper.GetOidcConfiguration(url)).SigningKeys;
        }
    }
}
