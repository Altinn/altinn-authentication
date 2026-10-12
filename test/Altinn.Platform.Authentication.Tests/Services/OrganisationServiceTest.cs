using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Exceptions;
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

namespace Altinn.Platform.Authentication.Tests.Services
{
    /// <summary>
    /// Testclass for harvesting orgs.
    /// </summary>
    public class OrganisationServiceTest
    {
        private readonly IMemoryCache _memoryCache;
        private readonly Mock<ILogger<OrganisationsService>> _loggerMock;
        private readonly Mock<IOptions<GeneralSettings>> _generalSettingsMock;

        public OrganisationServiceTest()
        {
            GeneralSettings generalSettings = new GeneralSettings { OrganisationRepositoryLocation = "https://mock.com/altinn-orgs.json", OidcRefreshTokenPepper = "YWRzZmFzZmRhc2ZzYWVmZWY=" };
            _generalSettingsMock = new Mock<IOptions<GeneralSettings>>();
            _generalSettingsMock.Setup(o => o.Value).Returns(generalSettings);
            _loggerMock = new Mock<ILogger<OrganisationsService>>();
            _memoryCache = new MemoryCache(new MemoryCacheOptions());
        }

        /// <summary>
        /// Tests harvest orgs.
        /// </summary>
        [Fact]
        public async Task LookupOrg_NoCache_CacheSuccessfullyPopulated()
        {
            // Arrange
            string expectedOrgNoKey = "974760223";

            HttpClient httpClient = GetTestHttpClient();
            OrganisationsService orgService = new OrganisationsService(httpClient, _memoryCache, _loggerMock.Object, _generalSettingsMock.Object);

            // Act 
            string org = await orgService.LookupOrg("974760223");
            _memoryCache.TryGetValue("organisationDictionary", out Dictionary<string, Organisation>? actualCachedDictionary);

            // Assert
            Assert.Equal("dibk", org);
            Assert.NotNull(actualCachedDictionary);
            Assert.True(actualCachedDictionary.TryGetValue(expectedOrgNoKey, out _));
        }

        [Fact]
        public async Task GetOrganisationByOrg_OrgReturnedFromCache()
        {
            // Arrange
            string cacheKey = "organisationDictionary";
            Dictionary<string, Organisation> organisationDictionary = new Dictionary<string, Organisation>
            {
                ["976029100"] = new Organisation
                {
                    Org = "nbib",
                    OrgNumber = "976029100",
                    Name = new Dictionary<string, string>
                    {
                        ["en"] = "National Library of Norway",
                        ["nb"] = "Nasjonalbiblioteket"
                    }
                }
            };

            _memoryCache.Set(cacheKey, organisationDictionary);
            _generalSettingsMock.Setup(o => o.Value).Returns(new GeneralSettings { OrganisationRepositoryLocation = "https://mock.com/altinn-orgs.json", OidcRefreshTokenPepper = "YWRzZmFzZmRhc2ZzYWVmZWY=" });

            OrganisationsService orgService = new OrganisationsService(null!, _memoryCache, _loggerMock.Object, _generalSettingsMock.Object); // deliberately null: the org is served from cache, so no HTTP client is needed

            // Act
            string orgName = await orgService.LookupOrg("976029100");

            // Assert
            Assert.Equal("nbib", orgName);
        }

        /// <summary>
        /// Regression test for an org list entry without an orgnr (null or empty). Before the fix the entry
        /// reached Dictionary.Add, which threw and was wrapped in OrganisationHarvestException, so one bad
        /// entry in altinn-orgs.json made every LookupOrg call fail (and nothing was cached, so every call
        /// re-downloaded the list). The entry must now be skipped with a warning while the rest resolve.
        /// </summary>
        [Fact]
        public async Task LookupOrg_OrgWithoutOrgNumber_IsSkippedAndRemainingOrgsResolve()
        {
            // Arrange
            Dictionary<string, Organisation> organisations = new Dictionary<string, Organisation>
            {
                ["dibk"] = new Organisation { Org = "dibk", OrgNumber = "974760223" },
                ["noorgnr"] = new Organisation { Org = "noorgnr", OrgNumber = null! },
                ["emptyorgnr"] = new Organisation { Org = "emptyorgnr", OrgNumber = string.Empty },
                ["emptyorgnr2"] = new Organisation { Org = "emptyorgnr2", OrgNumber = string.Empty }, // second empty: pre-fix this was the duplicate-key crash
                ["brg"] = new Organisation { Org = "brg", OrgNumber = "974760673" }
            };

            HttpClient httpClient = GetTestHttpClient(organisations);
            OrganisationsService orgService = new OrganisationsService(httpClient, _memoryCache, _loggerMock.Object, _generalSettingsMock.Object);

            // Act
            string org = await orgService.LookupOrg("974760223");
            string orgAfterSkippedEntry = await orgService.LookupOrg("974760673");
            _memoryCache.TryGetValue("organisationDictionary", out Dictionary<string, Organisation>? cachedDictionary);

            // Assert
            Assert.Equal("dibk", org);
            Assert.Equal("brg", orgAfterSkippedEntry);
            Assert.NotNull(cachedDictionary);
            Assert.Equal(2, cachedDictionary.Count);
            Assert.False(cachedDictionary.ContainsKey(string.Empty), "an empty orgnr must not be used as a dictionary key");
            Assert.DoesNotContain(cachedDictionary.Values, o => o.Org == "noorgnr" || o.Org == "emptyorgnr" || o.Org == "emptyorgnr2");
            VerifyWarningLogged("missing orgNumber", Times.Exactly(3));
            VerifyWarningLogged("Organisation noorgnr is missing orgNumber", Times.Once());
            VerifyWarningLogged("Organisation emptyorgnr is missing orgNumber", Times.Once());
            VerifyWarningLogged("Organisation emptyorgnr2 is missing orgNumber", Times.Once());
        }

        /// <summary>
        /// Once the dictionary is built with the bad entry skipped it is cached, so the list is downloaded
        /// once and later lookups do not touch the HTTP client. Guards against a regression where a skipped
        /// entry would bypass the cache write.
        /// </summary>
        [Fact]
        public async Task LookupOrg_OrgWithoutOrgNumber_ListIsFetchedOnceAndCached()
        {
            // Arrange
            Dictionary<string, Organisation> organisations = new Dictionary<string, Organisation>
            {
                ["dibk"] = new Organisation { Org = "dibk", OrgNumber = "974760223" },
                ["noorgnr"] = new Organisation { Org = "noorgnr", OrgNumber = null! }
            };

            Mock<HttpMessageHandler> handlerMock = GetTestHttpMessageHandler(organisations);
            OrganisationsService orgService = new OrganisationsService(new HttpClient(handlerMock.Object), _memoryCache, _loggerMock.Object, _generalSettingsMock.Object);

            // Act
            string first = await orgService.LookupOrg("974760223");
            string second = await orgService.LookupOrg("974760223");
            string unknown = await orgService.LookupOrg("000000000");

            // Assert
            Assert.Equal("dibk", first);
            Assert.Equal("dibk", second);
            Assert.Null(unknown);
            handlerMock.Protected().Verify("SendAsync", Times.Once(), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
        }

        /// <summary>
        /// The fix only skips entries without an orgnr. An org list with no entries at all is still treated
        /// as a harvest failure, so a broken or truncated upstream file does not silently cache an empty map.
        /// </summary>
        [Fact]
        public async Task LookupOrg_EmptyOrgList_ThrowsOrganisationHarvestExceptionAndCachesNothing()
        {
            // Arrange
            HttpClient httpClient = GetTestHttpClient(new Dictionary<string, Organisation>());
            OrganisationsService orgService = new OrganisationsService(httpClient, _memoryCache, _loggerMock.Object, _generalSettingsMock.Object);

            // Act
            OrganisationHarvestException ex = await Assert.ThrowsAsync<OrganisationHarvestException>(() => orgService.LookupOrg("974760223"));

            // Assert
            Assert.IsType<OrganisationHarvestException>(ex.InnerException);
            Assert.Contains("empty", ex.InnerException!.Message, StringComparison.OrdinalIgnoreCase);
            Assert.False(_memoryCache.TryGetValue("organisationDictionary", out _));
        }

        private void VerifyWarningLogged(string messageFragment, Times times)
        {
            _loggerMock.Verify(
                l => l.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((state, _) => state!.ToString()!.Contains(messageFragment)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                times);
        }

        private static HttpClient GetTestHttpClient(Dictionary<string, Organisation>? organisations = null)
        {
            return new HttpClient(GetTestHttpMessageHandler(organisations).Object);
        }

        private static Mock<HttpMessageHandler> GetTestHttpMessageHandler(Dictionary<string, Organisation>? organisations = null)
        {
            organisations ??= new Dictionary<string, Organisation>
            {
                ["dibk"] = new Organisation { Org = "dibk", OrgNumber = "974760223" },
                ["nbib"] = new Organisation
                {
                    Org = "nbib",
                    OrgNumber = "976029100",
                    Name = new Dictionary<string, string>
                    {
                        ["en"] = "National Library of Norway",
                        ["nb"] = "Nasjonalbiblioteket"
                    }
                },
                ["brg"] = new Organisation { Org = "brg", OrgNumber = "974760673" }
            };

            HttpResponseMessage orgsResponseMessage = new HttpResponseMessage
            {
                StatusCode = System.Net.HttpStatusCode.OK,
                Content = new StringContent($"{{\"orgs\": {JsonConvert.SerializeObject(organisations)} }}", Encoding.UTF8, "application/json")
            };

            Mock<HttpMessageHandler> handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);

            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(p => p.RequestUri!.ToString().Contains("altinn-orgs")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(orgsResponseMessage)
                .Verifiable();

            return handlerMock;
        }
    }
}
