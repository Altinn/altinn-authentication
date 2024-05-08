using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Altinn.Platform.Authentication.Health;
using Altinn.Platform.Authentication.Tests;
using Altinn.Platform.Authentication.Tests.RepositoryDataAccess;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Xunit;

namespace Altinn.Platform.Authentication.UnitTest
{
    /// <summary>
    /// Health check 
    /// </summary>
    public class HealthCheckTests(DbFixture dbFixture, WebApplicationFixture webApplicationFixture)
        : WebApplicationTests(dbFixture, webApplicationFixture)
    {
        private readonly WebApplicationFactory<HealthCheck> _factory;

        /// <summary>
        /// Verify that component responds on health check
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task VerifyHeltCheck_OK()
        {
            HttpClient client = CreateClient();

            HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, "/health")
            {
            };

            HttpResponseMessage response = await client.SendAsync(httpRequestMessage);
            await response.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}
