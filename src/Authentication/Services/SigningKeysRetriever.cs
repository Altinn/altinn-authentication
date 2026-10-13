using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Helpers;
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
            return (await ConfigurationMangerHelper.GetOidcConfiguration(url)).SigningKeys;
        }

        /// <inheritdoc />
        public async Task<string?> GetIssuer(string url, CancellationToken cancellationToken = default)
        {
            // The configuration manager caches the document, so this is not a fetch per call.
            // WaitAsync makes the wait cancellable; the underlying retrieval does not take a token.
            return (await ConfigurationMangerHelper.GetOidcConfiguration(url).WaitAsync(cancellationToken)).Issuer;
        }
    }
}
