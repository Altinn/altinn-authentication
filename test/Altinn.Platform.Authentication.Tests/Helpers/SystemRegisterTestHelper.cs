using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Core.Models.SystemRegisters;
using Xunit;

namespace Altinn.Platform.Authentication.Tests.Helpers
{
    public static class SystemRegisterTestHelper
    {
        private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web);

        public static async Task<HttpResponseMessage> CreateSystemRegister(HttpClient client, string dataFileName)
        {
            Stream dataStream = File.OpenRead(dataFileName);
            StreamContent content = new StreamContent(dataStream);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            HttpRequestMessage request = new(HttpMethod.Post, "/authentication/api/v1/systemregister/vendor/");
            request.Content = content;

            HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            return response;
        }

        private static async Task<List<SystemChangeLog>> GetSystemChangeLog(HttpClient client, string systemId)
        {
            HttpRequestMessage request = new(HttpMethod.Get, $"/authentication/api/v1/systemchangelog/{systemId}");
            HttpResponseMessage getResponse = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);

            List<SystemChangeLog> actualSystemChangeLog = JsonSerializer.Deserialize<List<SystemChangeLog>>(await getResponse.Content.ReadAsStringAsync(), _options);
            return actualSystemChangeLog;
        }

        public static async Task GetAndAssertSystemChangeLog(HttpClient client, string systemId, string fileName)
        {
            string systemChangeLog = File.OpenText($"Data/SystemChangeLog/{fileName}.json").ReadToEnd();
            List<SystemChangeLog> expectedSystemChangeLog = JsonSerializer.Deserialize<List<SystemChangeLog>>(systemChangeLog, _options);

            List<SystemChangeLog> actualSystemChangeLog = await GetSystemChangeLog(client, systemId);
            Assert.NotNull(actualSystemChangeLog);
            Assert.Equal(expectedSystemChangeLog.Count, actualSystemChangeLog.Count);

            foreach (var expected in expectedSystemChangeLog)
            {
                var actual = actualSystemChangeLog.FirstOrDefault(a => a.ChangeType == expected.ChangeType);
                Assert.NotNull(actual);
                Assert.Equal(expected.ChangedByOrgNumber, actual.ChangedByOrgNumber);

                var expectedJson = JsonSerializer.Serialize(expected.ChangedData, _options);
                var actualJson = JsonSerializer.Serialize(actual.ChangedData, _options);
                Assert.Equal(expectedJson, actualJson);
            }
        }
    }
}
