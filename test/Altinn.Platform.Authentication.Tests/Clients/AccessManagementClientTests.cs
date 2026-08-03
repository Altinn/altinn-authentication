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
using Microsoft.FeatureManagement;
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

        [Fact]
        public async Task RevokeClientFromAgentSystemUser_FlagOff_UsesV1RouteAndFromToParams()
        {
            // Arrange
            HttpRequestMessage? captured = null;
            var client = CreateClient(CreateHttpClient(HttpStatusCode.OK, string.Empty, capture: r => captured = r));
            Guid provider = Guid.NewGuid(), agent = Guid.NewGuid(), systemuser = Guid.NewGuid();

            // Act
            await client.RevokeClientFromAgentSystemUser(provider, agent, systemuser, CancellationToken.None);

            // Assert
            Assert.NotNull(captured);
            string url = captured!.RequestUri!.ToString();
            Assert.Contains("/accessmanagement/api/v1/enduser/clientdelegations/agents/clients", url);
            Assert.Contains($"from={agent}", url);
            Assert.Contains($"to={systemuser}", url);
        }

        [Fact]
        public async Task RevokeClientFromAgentSystemUser_FlagOn_UsesV2RouteAndClientAgentParams()
        {
            // Arrange
            HttpRequestMessage? captured = null;
            var client = CreateClient(CreateHttpClient(HttpStatusCode.OK, string.Empty, capture: r => captured = r), clientDelegationV2: true);
            Guid provider = Guid.NewGuid(), agent = Guid.NewGuid(), systemuser = Guid.NewGuid();

            // Act
            await client.RevokeClientFromAgentSystemUser(provider, agent, systemuser, CancellationToken.None);

            // Assert
            Assert.NotNull(captured);
            string url = captured!.RequestUri!.ToString();
            Assert.Contains("/accessmanagement/api/v2/enduser/clientdelegations/agents/clients", url);
            Assert.Contains($"client={agent}", url);
            Assert.Contains($"agent={systemuser}", url);
            Assert.DoesNotContain("/api/v1/", url);
        }

        [Theory]
        [InlineData(false, "v1", "to")]
        [InlineData(true, "v2", "agent")]
        public async Task RevokeSystemUserAsAgent_UsesVersionedRouteAndAgentParam(bool useV2, string version, string agentParam)
        {
            HttpRequestMessage? captured = null;
            var client = CreateClient(CreateHttpClient(HttpStatusCode.OK, string.Empty, capture: r => captured = r), clientDelegationV2: useV2);
            Guid party = Guid.NewGuid(), systemUser = Guid.NewGuid();

            await client.RevokeSystemUserAsAgent(party, systemUser, cascade: true, CancellationToken.None);

            string url = captured!.RequestUri!.ToString();
            Assert.Contains($"/accessmanagement/api/{version}/enduser/clientdelegations/agents?", url);
            Assert.Contains($"{agentParam}={systemUser}", url);
        }

        [Theory]
        [InlineData(false, "v1", "to")]
        [InlineData(true, "v2", "agent")]
        public async Task DeleteSystemUserAssignment_UsesVersionedRouteAndAgentParam(bool useV2, string version, string agentParam)
        {
            HttpRequestMessage? captured = null;
            var client = CreateClient(CreateHttpClient(HttpStatusCode.OK, string.Empty, capture: r => captured = r), clientDelegationV2: useV2);
            Guid facilitator = Guid.NewGuid(), systemUser = Guid.NewGuid();

            await client.DeleteSystemUserAssignment(facilitator, systemUser, CancellationToken.None);

            string url = captured!.RequestUri!.ToString();
            Assert.Contains($"/accessmanagement/api/{version}/enduser/clientdelegations/agents?", url);
            Assert.Contains($"{agentParam}={systemUser}", url);
        }

        [Theory]
        [InlineData(false, "v1", "to")]
        [InlineData(true, "v2", "agent")]
        public async Task GetClientDelegationsForAgent_UsesVersionedRouteAndAgentParam(bool useV2, string version, string agentParam)
        {
            HttpRequestMessage? captured = null;
            var client = CreateClient(CreateHttpClient(HttpStatusCode.OK, "{\"data\":[]}", capture: r => captured = r), clientDelegationV2: useV2);
            Guid provider = Guid.NewGuid(), systemUser = Guid.NewGuid();

            await client.GetClientDelegationsForAgent(systemUser, provider, CancellationToken.None);

            string url = captured!.RequestUri!.ToString();
            Assert.Contains($"/accessmanagement/api/{version}/enduser/clientdelegations/agents/accesspackages?", url);
            Assert.Contains($"party={provider}", url);
            Assert.Contains($"{agentParam}={systemUser}", url);
        }

        [Theory]
        [InlineData(false, "v1", "from", "to")]
        [InlineData(true, "v2", "client", "agent")]
        public async Task DelegateCustomerToAgentSystemUser_RenamesFromAndToParams(bool useV2, string version, string clientParam, string agentParam)
        {
            HttpRequestMessage? captured = null;
            var client = CreateClient(CreateHttpClient(HttpStatusCode.OK, "[]", capture: r => captured = r), clientDelegationV2: useV2);
            Guid provider = Guid.NewGuid(), agentClient = Guid.NewGuid(), systemUser = Guid.NewGuid();

            await client.DelegateCustomerToAgentSystemUser(systemUser, new DelegationBatchInputDto(), provider, agentClient, CancellationToken.None);

            string url = captured!.RequestUri!.ToString();
            Assert.Contains($"/accessmanagement/api/{version}/enduser/clientdelegations/agents/accesspackages?", url);
            Assert.Contains($"party={provider}", url);
            Assert.Contains($"{clientParam}={agentClient}", url);
            Assert.Contains($"{agentParam}={systemUser}", url);
        }

        [Theory]
        [InlineData(false, "v1")]
        [InlineData(true, "v2")]
        public async Task GetClientsForFacilitator_UsesVersionedRoute_WithoutRenamingParams(bool useV2, string version)
        {
            HttpRequestMessage? captured = null;
            var client = CreateClient(CreateHttpClient(HttpStatusCode.OK, "{\"data\":[]}", capture: r => captured = r), clientDelegationV2: useV2);
            Guid facilitator = Guid.NewGuid();

            await client.GetClientsForFacilitator(facilitator, ["urn:altinn:accesspackage:skattnaering"], CancellationToken.None);

            string url = captured!.RequestUri!.ToString();
            Assert.Contains($"/accessmanagement/api/{version}/enduser/clientdelegations/clients?", url);
            Assert.Contains($"party={facilitator}", url);
            Assert.Contains("packages=urn:altinn:accesspackage:skattnaering", url);

            // 'clients' takes party/packages only - the from/to -> client/agent rename must NOT apply here.
            Assert.DoesNotContain("from=", url);
            Assert.DoesNotContain("to=", url);
            Assert.DoesNotContain("agent=", url);
        }

        [Fact]
        public async Task GetClientsForFacilitator_FlagOn_ParsesV2PayloadIncludingNewResourcesField()
        {
            // v2 ClientDto adds a 'resources' array per access entry that the authentication DTO does not
            // model. This verifies the extra field is ignored and the consumed fields still deserialize.
            Guid clientId = Guid.NewGuid();
            string body = $$"""
            {
              "links": { "next": null },
              "data": [
                {
                  "client": { "id": "{{clientId}}", "name": "Acme AS", "type": "Organisasjon" },
                  "access": [
                    {
                      "role": { "urn": "urn:altinn:external-role:ccr:regnskapsforer" },
                      "packages": [ { "urn": "urn:altinn:accesspackage:skattnaering" } ],
                      "resources": [ { "urn": "urn:altinn:resource:some-resource" } ]
                    }
                  ]
                }
              ]
            }
            """;
            var client = CreateClient(CreateHttpClient(HttpStatusCode.OK, body), clientDelegationV2: true);

            var result = await client.GetClientsForFacilitator(Guid.NewGuid(), [], CancellationToken.None);

            Assert.False(result.IsProblem);
            var single = Assert.Single(result.Value);
            Assert.Equal(clientId, single.Client.Id);
            var access = Assert.Single(single.Access);
            Assert.Equal("urn:altinn:accesspackage:skattnaering", Assert.Single(access.Packages).Urn);
        }

        private static HttpClient CreateHttpClient(HttpStatusCode statusCode, string responseBody, string mediaType = "application/json", Action<HttpRequestMessage>? capture = null)
        {
            Mock<HttpMessageHandler> handlerMock = new();
            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((request, _) => capture?.Invoke(request))
                .ReturnsAsync(new HttpResponseMessage(statusCode)
                {
                    Content = new StringContent(responseBody, Encoding.UTF8, mediaType)
                });

            return new HttpClient(handlerMock.Object);
        }

        private AccessManagementClient CreateClient(HttpClient httpClient, bool clientDelegationV2 = false)
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

            Mock<IFeatureManager> featureManagerMock = new();
            featureManagerMock
                .Setup(f => f.IsEnabledAsync(AccessManagementFeatureFlags.ClientDelegationApiV2))
                .ReturnsAsync(clientDelegationV2);

            return new AccessManagementClient(
                httpClient,
                _loggerMock.Object,
                httpContextAccessorMock.Object,
                accessManagementSettings,
                platformSettings,
                new Mock<IWebHostEnvironment>().Object,
                new Mock<IAccessTokenGenerator>().Object,
                featureManagerMock.Object);
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
