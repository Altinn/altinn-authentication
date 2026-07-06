using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Authentication.Core.Problems;
using Altinn.Authentication.Integration.Configuration;
using Altinn.Authorization.ProblemDetails;
using Altinn.Common.AccessTokenClient.Services;
using Altinn.Platform.Authentication.Core.Models.AccessPackages;
using Altinn.Platform.Authentication.Core.Models.Rights;
using Altinn.Platform.Authentication.Core.Models.Rights.ConnectionsDtos;
using Altinn.Platform.Authentication.Integration.AccessManagement;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Xunit;

namespace Altinn.Platform.Authentication.Tests.Clients
{
    /// <summary>
    /// Tests for <see cref="AccessManagementClient"/>, covering that the response body from
    /// Access Management is logged when a delegation check fails (issue #2027).
    /// </summary>
    public class AccessManagementClientTests
    {
        private readonly Mock<ILogger<AccessManagementClient>> _loggerMock = new();

        [Fact]
        public async Task CheckDelegationAccess_Non200_LogsStatusCodePartyResourceAndResponseBody_ReturnsNull()
        {
            // Arrange
            Guid partyUuid = Guid.NewGuid();
            const string resource = "ttd-am-k6";
            const string responseBody = "{\"title\":\"Forbidden\",\"detail\":\"Party does not have permission to perform delegation check\"}";
            var client = CreateClient(CreateHttpClient(HttpStatusCode.Forbidden, responseBody));

            // Act
            ResourceCheckDto? result = await client.CheckDelegationAccess(partyUuid, resource, CancellationToken.None);

            // Assert
            Assert.Null(result);
            VerifyErrorLogged("Forbidden", partyUuid.ToString(), resource, responseBody);
        }

        [Fact]
        public async Task CheckDelegationAccess_Non200_NonJsonBody_DoesNotThrow_LogsRawBody()
        {
            // Arrange: a gateway/proxy error returning plain text instead of ProblemDetails JSON.
            // Before the fix this threw a JsonException and the body was never logged.
            Guid partyUuid = Guid.NewGuid();
            const string responseBody = "502 Bad Gateway - upstream timed out";
            var client = CreateClient(CreateHttpClient(HttpStatusCode.BadGateway, responseBody, "text/plain"));

            // Act
            ResourceCheckDto? result = await client.CheckDelegationAccess(partyUuid, "some-resource", CancellationToken.None);

            // Assert
            Assert.Null(result);
            VerifyErrorLogged("BadGateway", partyUuid.ToString(), responseBody);
        }

        [Fact]
        public async Task CheckDelegationAccess_200_ReturnsDto_NoErrorLogged()
        {
            // Arrange
            const string responseBody = "{\"resource\":{\"refId\":\"ttd-am-k6\"},\"rights\":[{\"right\":{\"key\":\"read\"},\"result\":true}]}";
            var client = CreateClient(CreateHttpClient(HttpStatusCode.OK, responseBody));

            // Act
            ResourceCheckDto? result = await client.CheckDelegationAccess(Guid.NewGuid(), "ttd-am-k6", CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result.Rights);
            VerifyNoErrorLogged();
        }

        [Fact]
        public async Task CheckDelegationAccessForAccessPackage_Non200_LogsStatusCodePartyPackagesAndResponseBody_ReturnsProblem()
        {
            // Arrange
            Guid partyId = Guid.NewGuid();
            const string packageUrn = "urn:altinn:accesspackage:skattnaering";
            const string responseBody = "{\"title\":\"Internal Server Error\",\"detail\":\"Delegation check failed unexpectedly\"}";
            var client = CreateClient(CreateHttpClient(HttpStatusCode.InternalServerError, responseBody));

            // Act
            List<Result<AccessPackageDto.Check>> results = [];
            await foreach (Result<AccessPackageDto.Check> result in client.CheckDelegationAccessForAccessPackage(partyId, [packageUrn], CancellationToken.None))
            {
                results.Add(result);
            }

            // Assert
            Result<AccessPackageDto.Check> single = Assert.Single(results);
            Assert.True(single.IsProblem);
            Assert.Equal(Problem.AccessPackage_DelegationCheckFailed.Title, single.Problem!.Title);
            VerifyErrorLogged("InternalServerError", partyId.ToString(), packageUrn, responseBody);
        }

        [Fact]
        public async Task CheckDelegationAccessForAccessPackage_Non200_NonJsonBody_DoesNotThrow_LogsRawBody()
        {
            // Arrange
            Guid partyId = Guid.NewGuid();
            const string responseBody = "Service Unavailable";
            var client = CreateClient(CreateHttpClient(HttpStatusCode.ServiceUnavailable, responseBody, "text/plain"));

            // Act
            List<Result<AccessPackageDto.Check>> results = [];
            await foreach (Result<AccessPackageDto.Check> result in client.CheckDelegationAccessForAccessPackage(partyId, ["urn:altinn:accesspackage:skattnaering"], CancellationToken.None))
            {
                results.Add(result);
            }

            // Assert
            Result<AccessPackageDto.Check> single = Assert.Single(results);
            Assert.True(single.IsProblem);
            VerifyErrorLogged("ServiceUnavailable", responseBody);
        }

        private static HttpClient CreateHttpClient(HttpStatusCode statusCode, string responseBody, string mediaType = "application/json")
        {
            Mock<HttpMessageHandler> handlerMock = new();
            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(statusCode)
                {
                    Content = new StringContent(responseBody, Encoding.UTF8, mediaType)
                });

            return new HttpClient(handlerMock.Object);
        }

        private AccessManagementClient CreateClient(HttpClient httpClient)
        {
            DefaultHttpContext httpContext = new();
            httpContext.Request.Headers.Authorization = "Bearer unittest-token";

            Mock<IHttpContextAccessor> httpContextAccessorMock = new();
            httpContextAccessorMock.Setup(a => a.HttpContext).Returns(httpContext);

            IOptions<AccessManagementSettings> accessManagementSettings = Options.Create(new AccessManagementSettings
            {
                ApiAccessManagementEndpoint = "http://localhost:5117/accessmanagement/api/v1/"
            });

            IOptions<PlatformSettings> platformSettings = Options.Create(new PlatformSettings
            {
                JwtCookieName = "AltinnStudioRuntime"
            });

            return new AccessManagementClient(
                httpClient,
                _loggerMock.Object,
                httpContextAccessorMock.Object,
                accessManagementSettings,
                platformSettings,
                new Mock<IWebHostEnvironment>().Object,
                new Mock<IAccessTokenGenerator>().Object);
        }

        private void VerifyErrorLogged(params string[] expectedFragments)
        {
            _loggerMock.Verify(
                l => l.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((state, _) => expectedFragments.All(fragment => state.ToString()!.Contains(fragment))),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        private void VerifyNoErrorLogged()
        {
            _loggerMock.Verify(
                l => l.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Never);
        }
    }
}
