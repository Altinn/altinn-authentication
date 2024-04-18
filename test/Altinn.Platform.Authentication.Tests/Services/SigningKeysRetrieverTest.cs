using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Model;
using Altinn.Platform.Authentication.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;
using Moq.Protected;
using Newtonsoft.Json;
using Xunit;
using static System.Net.WebRequestMethods;

namespace Altinn.Platform.Authentication.Tests.Services
{
    /// <summary>
    /// Testclass for SigningKeysRetriever
    /// </summary>
    public class SigningKeysRetrieverTest
    {
        public SigningKeysRetrieverTest()
        {
        }

        /// <summary>
        /// Tests SigningKeysRetriever
        /// </summary>
        [Fact]
        public async Task SigningKeysRetriever_Ok()
        {
            // Arrange
            string url1 = "https://platform.at22.altinn.cloud/authentication/api/v1/openid/.well-known/openid-configuration";
            string url2 = "https://test.idporten.no/.well-known/openid-configuration";

            // Act
            var keys1 = await new SigningKeysRetriever().GetSigningKeys(url1);
            var keys2 = await new SigningKeysRetriever().GetSigningKeys(url2);
            var keys3 = await new SigningKeysRetriever().GetSigningKeys(url2);

            // Assert
            Assert.NotNull(keys1);
            Assert.NotNull(keys2);
            Assert.Equal(keys2, keys3);
        }
     }
}
