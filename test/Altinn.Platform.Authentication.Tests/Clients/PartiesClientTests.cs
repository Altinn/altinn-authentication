using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Authentication.Integration.Clients;
using Altinn.Authentication.Integration.Configuration;
using Altinn.Common.AccessTokenClient.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Xunit;
using RegisterContracts = Altinn.Register.Contracts;

namespace Altinn.Platform.Authentication.Tests.Clients
{
    /// <summary>
    /// Tests for <see cref="PartiesClient.GetPartyIdentifiersAndUsernameByPersonIdentifier"/> against a
    /// mocked <see cref="HttpMessageHandler"/>, so the real request building and response deserialization
    /// run. The controller tests mock <c>IPartiesClient</c> wholesale and therefore cannot see either.
    /// </summary>
    public class PartiesClientTests
    {
        private const string Ssn = "17899198255";

        [Fact]
        public async Task GetPartyIdentifiersAndUsernameByPersonIdentifier_RequestsDateOfDeathField()
        {
            // Arrange
            HttpRequestMessage? captured = null;
            PartiesClient client = CreateClient(
                CreateHttpClient(HttpStatusCode.OK, PartyResponse(DeceasedPersonJson), r => captured = r));

            // Act
            await client.GetPartyIdentifiersAndUsernameByPersonIdentifier(Ssn, CancellationToken.None);

            // Assert: without 'person.date-of-death' in the field list Register omits dateOfDeath, and the
            // exchange rejects every person. Only the date of death is requested, not the whole person object.
            Assert.NotNull(captured);
            string query = captured.RequestUri!.Query.TrimStart('?');
            string fields = query.Split('&')
                .Single(p => p.StartsWith("fields=", StringComparison.Ordinal))
                .Substring("fields=".Length);
            string[] requested = fields.Split(',');

            Assert.Contains("person.date-of-death", requested);
            Assert.DoesNotContain("person", requested);
            Assert.Contains("user", requested);
        }

        [Fact]
        public async Task GetPartyIdentifiersAndUsernameByPersonIdentifier_DeceasedPerson_DateOfDeathHasValue()
        {
            // Arrange: the AT22 response for party 50459464 (synthetic test person).
            PartiesClient client = CreateClient(
                CreateHttpClient(HttpStatusCode.OK, PartyResponse(DeceasedPersonJson)));

            // Act
            RegisterContracts.Party? party = await client.GetPartyIdentifiersAndUsernameByPersonIdentifier(Ssn, CancellationToken.None);

            // Assert
            Assert.NotNull(party);
            RegisterContracts.Person person = Assert.IsType<RegisterContracts.Person>(party);
            Assert.True(person.DateOfDeath.HasValue);
            Assert.Equal(new DateOnly(2020, 12, 22), person.DateOfDeath.Value);

            // Death is signalled by dateOfDeath alone - the party is neither deleted nor without a user.
            Assert.False(person.IsDeleted.Value);
            Assert.True(person.User.HasValue);
        }

        [Fact]
        public async Task GetPartyIdentifiersAndUsernameByPersonIdentifier_LivingPerson_DateOfDeathIsNullNotUnset()
        {
            // Arrange
            PartiesClient client = CreateClient(
                CreateHttpClient(HttpStatusCode.OK, PartyResponse(LivingPersonJson)));

            // Act
            RegisterContracts.Party? party = await client.GetPartyIdentifiersAndUsernameByPersonIdentifier(Ssn, CancellationToken.None);

            // Assert: this is the distinction a date-of-death guard depends on. A living person must come
            // back IsNull (asked for, no value). IsUnset would mean the field was never requested, which
            // is indistinguishable from "alive" unless the guard treats it as an error.
            Assert.NotNull(party);
            RegisterContracts.Person person = Assert.IsType<RegisterContracts.Person>(party);
            Assert.False(person.DateOfDeath.HasValue);
            Assert.True(person.DateOfDeath.IsNull);
            Assert.False(person.DateOfDeath.IsUnset);
        }

        [Fact]
        public async Task GetPartyIdentifiersAndUsernameByPersonIdentifier_PersonFieldNotReturned_DateOfDeathIsUnset()
        {
            // Arrange: what Register sends when 'person.date-of-death' is absent from the field list. Kept as
            // a test so the difference between "not requested" and "alive" stays visible.
            PartiesClient client = CreateClient(
                CreateHttpClient(HttpStatusCode.OK, PartyResponse(MinimalPersonJson)));

            // Act
            RegisterContracts.Party? party = await client.GetPartyIdentifiersAndUsernameByPersonIdentifier(Ssn, CancellationToken.None);

            // Assert
            Assert.NotNull(party);
            RegisterContracts.Person person = Assert.IsType<RegisterContracts.Person>(party);
            Assert.True(person.DateOfDeath.IsUnset);
            Assert.False(person.DateOfDeath.HasValue);
        }

        private const string DeceasedPersonJson = """
            {
              "partyType": "person",
              "personIdentifier": "17899198255",
              "firstName": "SMIGRENDE",
              "lastName": "STILLING",
              "shortName": "STILLING SMIGRENDE",
              "dateOfBirth": "1991-09-17",
              "dateOfDeath": "2020-12-22",
              "partyUuid": "b6a019e8-96ba-489c-83cc-a3fe66d33b7a",
              "versionId": 641270138,
              "partyId": 50459464,
              "displayName": "SMIGRENDE STILLING",
              "createdAt": "2025-03-10T17:36:37.345775+00:00",
              "modifiedAt": "2026-04-29T10:34:38.40008+00:00",
              "isDeleted": false,
              "deletedAt": null,
              "user": { "userId": 20875912, "username": null, "userIds": [ 20875912 ] }
            }
            """;

        private const string LivingPersonJson = """
            {
              "partyType": "person",
              "personIdentifier": "17899198255",
              "firstName": "LEVENDE",
              "lastName": "PERSON",
              "shortName": "PERSON LEVENDE",
              "dateOfBirth": "1991-09-17",
              "dateOfDeath": null,
              "partyUuid": "b6a019e8-96ba-489c-83cc-a3fe66d33b7a",
              "versionId": 641270138,
              "partyId": 50459464,
              "displayName": "LEVENDE PERSON",
              "createdAt": "2025-03-10T17:36:37.345775+00:00",
              "modifiedAt": "2026-04-29T10:34:38.40008+00:00",
              "isDeleted": false,
              "deletedAt": null,
              "user": { "userId": 20875912, "username": null, "userIds": [ 20875912 ] }
            }
            """;

        private const string MinimalPersonJson = """
            {
              "partyType": "person",
              "personIdentifier": "17899198255",
              "partyUuid": "b6a019e8-96ba-489c-83cc-a3fe66d33b7a",
              "versionId": 641270138,
              "partyId": 50459464,
              "user": { "userId": 20875912, "username": null, "userIds": [ 20875912 ] }
            }
            """;

        private static string PartyResponse(string partyJson) => $$"""{ "data": [ {{partyJson}} ] }""";

        private static PartiesClient CreateClient(HttpClient httpClient)
        {
            PlatformSettings settings = new()
            {
                ApiRegisterEndpoint = "https://platform.local/register/api/v1/",
                ApiRegisterInternalEndpoint = "https://platform.local/register/api/v2/internal/",
                SubscriptionKeyHeaderName = "Ocp-Apim-Subscription-Key",
                SubscriptionKey = "key",
            };

            Mock<IAccessTokenGenerator> accessTokenGenerator = new();
            accessTokenGenerator
                .Setup(a => a.GenerateAccessToken(It.IsAny<string>(), It.IsAny<string>()))
                .Returns("platform-access-token");

            return new PartiesClient(
                httpClient,
                new Mock<ILogger<PartiesClient>>().Object,
                new Mock<IHttpContextAccessor>().Object,
                Options.Create(settings),
                accessTokenGenerator.Object);
        }

        private static HttpClient CreateHttpClient(
            HttpStatusCode statusCode,
            string responseBody,
            Action<HttpRequestMessage>? capture = null)
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
                    Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
                });

            return new HttpClient(handlerMock.Object);
        }
    }
}
