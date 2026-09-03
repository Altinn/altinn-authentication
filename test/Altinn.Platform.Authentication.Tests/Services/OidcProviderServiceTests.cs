#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Model;
using Altinn.Platform.Authentication.Services;
using Altinn.Platform.Authentication.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace Altinn.Platform.Authentication.Tests.Services
{
    /// <summary>
    /// Tests for <see cref="OidcProviderService"/>, covering the outcome counter
    /// (<c>altinn.authentication.oidc.upstream_token_exchange</c>) and the diagnostics we log when
    /// the upstream token endpoint refuses or fails to serve a code-to-token request.
    /// </summary>
    /// <remarks>
    /// Motivated by an ID-porten incident where the only signal was <c>LogError</c> with a bare status
    /// code: the OAuth <c>error</c> / <c>error_description</c> body was discarded, so the cause could
    /// not be told apart from ordinary user behaviour, and there was no metric to alert on.
    /// </remarks>
    public class OidcProviderServiceTests : IDisposable
    {
        private const string InstrumentName = "altinn.authentication.oidc.upstream_token_exchange";

        /// <summary>
        /// <see cref="Altinn.Authorization.ServiceDefaults.Telemetry.IMetrics{TSelf}.MeterName"/>
        /// defaults to the name of the assembly that declares the metrics type.
        /// </summary>
        private static readonly string MeterName = typeof(OidcProviderService).Assembly.GetName().Name!;

        private readonly Mock<ILogger<OidcProviderService>> _loggerMock = new();

        private readonly ServiceProvider _services = new ServiceCollection().AddMetrics().BuildServiceProvider();

        /// <inheritdoc/>
        public void Dispose()
        {
            _services.Dispose();
            GC.SuppressFinalize(this);
        }

        [Fact]
        public async Task GetTokens_Success_ReturnsResponse_AndCountsSuccess()
        {
            const string body = """{"access_token":"at","id_token":"it","token_type":"Bearer","expires_in":300}""";
            (OidcProviderService sut, MetricCollector<int> collector) = CreateSut(HttpStatusCode.OK, body);

            OidcCodeResponse? result = await sut.GetTokens("code", NewProvider(), "https://at.altinn.no/cb", "verifier", CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal("it", result.IdToken);

            IReadOnlyList<CollectedMeasurement<int>> measurements = collector.GetMeasurementSnapshot();
            CollectedMeasurement<int> measurement = Assert.Single(measurements);
            Assert.Equal(1, measurement.Value);
            AssertSuccessTags(measurement, statusCode: 200);
            VerifyNoErrorLogged();
        }

        [Fact]
        public async Task GetTokens_InvalidGrant_ReturnsNull_CountsErrorType_AndLogsWarningNotError()
        {
            // A replayed or expired code is normal user behaviour (back button), so it must not raise
            // the error rate an alert is written on.
            const string body = """{"error":"invalid_grant","error_description":"Authorization code is expired"}""";
            (OidcProviderService sut, MetricCollector<int> collector) = CreateSut(HttpStatusCode.BadRequest, body);

            OidcCodeResponse? result = await sut.GetTokens("code", NewProvider(), "https://at.altinn.no/cb", "verifier", CancellationToken.None);

            Assert.Null(result);
            CollectedMeasurement<int> measurement = Assert.Single(collector.GetMeasurementSnapshot());
            AssertFailureTags(measurement, statusCode: 400, errorType: "invalid_grant");

            VerifyNoErrorLogged();
            VerifyLogged(LogLevel.Warning, "idporten", "invalid_grant", "Authorization code is expired");
        }

        [Fact]
        public async Task GetTokens_InvalidClient_ReturnsNull_CountsErrorType_AndLogsErrorWithDescription()
        {
            // The incident case: an expired client secret. The error_description is what tells an
            // on-call engineer which of the 4xx causes they are looking at.
            const string body = """{"error":"invalid_client","error_description":"client authentication failed"}""";
            (OidcProviderService sut, MetricCollector<int> collector) = CreateSut(HttpStatusCode.Unauthorized, body);

            OidcCodeResponse? result = await sut.GetTokens("code", NewProvider(), "https://at.altinn.no/cb", "verifier", CancellationToken.None);

            Assert.Null(result);
            CollectedMeasurement<int> measurement = Assert.Single(collector.GetMeasurementSnapshot());
            AssertFailureTags(measurement, statusCode: 401, errorType: "invalid_client");

            VerifyLogged(LogLevel.Error, "idporten", "invalid_client", "client authentication failed");
        }

        [Fact]
        public async Task GetTokens_UnknownErrorCode_IsFoldedIntoOtherSentinel()
        {
            // Cardinality guard: the upstream controls this value, so only a known set reaches the metric.
            const string body = """{"error":"a_brand_new_upstream_code","error_description":"nope"}""";
            (OidcProviderService sut, MetricCollector<int> collector) = CreateSut(HttpStatusCode.BadRequest, body);

            await sut.GetTokens("code", NewProvider(), "https://at.altinn.no/cb", "verifier", CancellationToken.None);

            CollectedMeasurement<int> measurement = Assert.Single(collector.GetMeasurementSnapshot());
            AssertFailureTags(measurement, statusCode: 400, errorType: "_OTHER");

            // The raw code is still logged, it is only kept off the metric dimension.
            VerifyLogged(LogLevel.Error, "a_brand_new_upstream_code");
        }

        [Theory]
        [InlineData(HttpStatusCode.ServiceUnavailable, 503)]
        [InlineData(HttpStatusCode.InternalServerError, 500)]
        [InlineData(HttpStatusCode.TooManyRequests, 429)]
        public async Task GetTokens_UpstreamUnhealthy_CountsOtherSentinelWithStatusCode(HttpStatusCode statusCode, int expected)
        {
            // A gateway in front of the OP typically answers HTML, not an OAuth error object.
            const string body = "<html><head><title>503 Service Unavailable</title></head></html>";
            (OidcProviderService sut, MetricCollector<int> collector) = CreateSut(statusCode, body, "text/html");

            OidcCodeResponse? result = await sut.GetTokens("code", NewProvider(), "https://at.altinn.no/cb", "verifier", CancellationToken.None);

            Assert.Null(result);
            CollectedMeasurement<int> measurement = Assert.Single(collector.GetMeasurementSnapshot());
            AssertFailureTags(measurement, statusCode: expected, errorType: "_OTHER");

            VerifyLogged(LogLevel.Error, "503 Service Unavailable");
        }

        [Fact]
        public async Task GetTokens_UnparseableErrorBody_IsTruncatedInLog()
        {
            string body = new string('x', 4000);
            (OidcProviderService sut, _) = CreateSut(HttpStatusCode.BadGateway, body, "text/html");

            await sut.GetTokens("code", NewProvider(), "https://at.altinn.no/cb", "verifier", CancellationToken.None);

            // 512 characters plus the ellipsis marker, so an HTML error page cannot flood the log.
            VerifyLogged(LogLevel.Error, new string('x', 512) + "...");
            VerifyNotLogged(LogLevel.Error, new string('x', 513));
        }

        [Fact]
        public async Task GetTokens_TransportFailure_ReturnsNull_CountsExceptionTypeAndNoStatusCode()
        {
            // No HTTP response at all: DNS/TLS/connect failure, a request timeout, or an open circuit.
            (OidcProviderService sut, MetricCollector<int> collector) = CreateSutThatThrows(new HttpRequestException("no such host"));

            OidcCodeResponse? result = await sut.GetTokens("code", NewProvider(), "https://at.altinn.no/cb", "verifier", CancellationToken.None);

            Assert.Null(result);
            CollectedMeasurement<int> measurement = Assert.Single(collector.GetMeasurementSnapshot());

            Assert.Equal("idporten", measurement.Tags["provider"]);
            Assert.Equal("System.Net.Http.HttpRequestException", measurement.Tags["error.type"]);

            // There was no response, so there is no status code to report — the tag is omitted entirely
            // rather than reported as a fictitious 0.
            Assert.False(measurement.Tags.ContainsKey("http.response.status_code"));

            VerifyLogged(LogLevel.Error, "idporten");
        }

        [Fact]
        public async Task GetTokens_Ok_ButNoIdToken_CountsInvalidResponse()
        {
            const string body = """{"access_token":"at","token_type":"Bearer"}""";
            (OidcProviderService sut, MetricCollector<int> collector) = CreateSut(HttpStatusCode.OK, body);

            OidcCodeResponse? result = await sut.GetTokens("code", NewProvider(), "https://at.altinn.no/cb", "verifier", CancellationToken.None);

            Assert.Null(result);
            CollectedMeasurement<int> measurement = Assert.Single(collector.GetMeasurementSnapshot());
            AssertFailureTags(measurement, statusCode: 200, errorType: "invalid_response");
        }

        [Fact]
        public async Task GetTokens_Ok_ButNotJson_CountsInvalidResponse_AndDoesNotThrow()
        {
            (OidcProviderService sut, MetricCollector<int> collector) = CreateSut(HttpStatusCode.OK, "not json at all", "text/plain");

            OidcCodeResponse? result = await sut.GetTokens("code", NewProvider(), "https://at.altinn.no/cb", "verifier", CancellationToken.None);

            Assert.Null(result);
            CollectedMeasurement<int> measurement = Assert.Single(collector.GetMeasurementSnapshot());
            AssertFailureTags(measurement, statusCode: 200, errorType: "invalid_response");
        }

        [Fact]
        public async Task GetTokens_CallerCancelled_Rethrows_AndRecordsNoMeasurement()
        {
            // The browser went away. That is not an upstream failure and must not move the metric.
            using CancellationTokenSource cts = new();
            await cts.CancelAsync();

            (OidcProviderService sut, MetricCollector<int> collector) = CreateSutThatThrows(new TaskCanceledException());

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => sut.GetTokens("code", NewProvider(), "https://at.altinn.no/cb", "verifier", cts.Token));

            Assert.Empty(collector.GetMeasurementSnapshot());
            VerifyNoErrorLogged();
        }

        [Fact]
        public async Task GetTokens_TagsProviderWithIssuerKey_SoAlertsCanSplitPerProvider()
        {
            const string body = """{"error":"server_error","error_description":"boom"}""";
            (OidcProviderService sut, MetricCollector<int> collector) = CreateSut(HttpStatusCode.InternalServerError, body);

            OidcProvider provider = NewProvider();
            provider.IssuerKey = "uidp";

            await sut.GetTokens("code", provider, "https://at.altinn.no/cb", "verifier", CancellationToken.None);

            CollectedMeasurement<int> measurement = Assert.Single(collector.GetMeasurementSnapshot());
            Assert.Equal("uidp", measurement.Tags["provider"]);
        }

        private static OidcProvider NewProvider() => new()
        {
            IssuerKey = "idporten",
            Issuer = "https://test.idporten.no",
            TokenEndpoint = "https://test.idporten.no/token",
            ClientId = "test-client",
            ClientSecret = "test-secret",
        };

        /// <summary>
        /// A success carries no <c>error.type</c> at all — that absence is what an alert query uses to
        /// separate successes from failures, so assert it rather than a sentinel value.
        /// </summary>
        private static void AssertSuccessTags(CollectedMeasurement<int> measurement, int statusCode)
        {
            Assert.Equal("idporten", measurement.Tags["provider"]);
            Assert.Equal(statusCode, measurement.Tags["http.response.status_code"]);
            Assert.False(measurement.Tags.ContainsKey("error.type"));
        }

        private static void AssertFailureTags(CollectedMeasurement<int> measurement, int statusCode, string errorType)
        {
            Assert.Equal("idporten", measurement.Tags["provider"]);
            Assert.Equal(statusCode, measurement.Tags["http.response.status_code"]);
            Assert.Equal(errorType, measurement.Tags["error.type"]);
        }

        private (OidcProviderService Sut, MetricCollector<int> Collector) CreateSut(HttpStatusCode statusCode, string responseBody, string mediaType = "application/json")
        {
            Mock<HttpMessageHandler> handlerMock = new();
            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() => new HttpResponseMessage(statusCode)
                {
                    Content = new StringContent(responseBody, Encoding.UTF8, mediaType)
                });

            return CreateSut(handlerMock);
        }

        private (OidcProviderService Sut, MetricCollector<int> Collector) CreateSutThatThrows(Exception exception)
        {
            Mock<HttpMessageHandler> handlerMock = new();
            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(exception);

            return CreateSut(handlerMock);
        }

        private (OidcProviderService Sut, MetricCollector<int> Collector) CreateSut(Mock<HttpMessageHandler> handlerMock)
        {
            IMeterFactory meterFactory = _services.GetRequiredService<IMeterFactory>();
            MetricCollector<int> collector = new(meterFactory, MeterName, InstrumentName);

            OidcProviderService sut = new(
                new HttpClient(handlerMock.Object),
                _loggerMock.Object,
                new TestMetricsProvider(meterFactory));

            return (sut, collector);
        }

        private void VerifyLogged(LogLevel level, params string[] expectedFragments)
        {
            _loggerMock.Verify(
                l => l.Log(
                    level,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((state, _) => expectedFragments.All(fragment => state.ToString()!.Contains(fragment))),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        private void VerifyNotLogged(LogLevel level, string fragment)
        {
            _loggerMock.Verify(
                l => l.Log(
                    level,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains(fragment)),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Never);
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
