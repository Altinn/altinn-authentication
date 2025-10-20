using System;
using System.Drawing.Text;
using System.IO;
using Altinn.Authentication.Tests.Mocks;
using Altinn.Platform.Authentication.Core.Helpers;
using Altinn.Platform.Authentication.Tests.Models;

namespace Altinn.Platform.Authentication.Tests.Utils
{
    public static class OidcScenarioHelper
    {
        public static OidcTestScenario GetScenario(string scenarioId)
        {
            OidcTestScenario scenario = null;
            string path = GetOidcScenarioPath(scenarioId);

            if (File.Exists(path))
            {
                string content = File.ReadAllText(path);
                scenario = System.Text.Json.JsonSerializer.Deserialize<OidcTestScenario>(content);
            }
            else
            {
                throw new FileNotFoundException($"The specified scenario file was not found: {path}");
            }

            if (string.IsNullOrEmpty(scenario.DownstreamClientId))
            {
                scenario.DownstreamClientId = Guid.CreateVersion7().ToString();
            }

            if (string.IsNullOrEmpty(scenario.ClientSecret))
            {
                scenario.ClientSecret = CryptoHelpers.RandomBase64Url(32);
                scenario.HashedClientSecret = ClientSecretHasher.Hash(scenario.ClientSecret);
            }

            if (scenario.LoginStates == null || scenario.LoginStates.Count == 0)
            { 
                LoginTestState state = new()
                {
                    DownstreamState = CryptoHelpers.RandomBase64Url(32),
                    DownstreamNonce = CryptoHelpers.RandomBase64Url(32),
                    DownstreamCodeVerifier = Pkce.RandomPkceVerifier(),
                    UpstreamProviderCode = CryptoHelpers.RandomBase64Url(32)
                };

                state.DownstreamCodeChallenge = Pkce.ComputeS256CodeChallenge(state.DownstreamCodeVerifier);
                scenario.LoginStates.Add(state);
            }

            return scenario;
        }

        private static string GetOidcScenarioPath(string scenario)
        {
            string unitTestFolder = Path.GetDirectoryName(new Uri(typeof(PartiesClientMock).Assembly.Location).LocalPath);
            return Path.Combine(unitTestFolder, "Data", "OidcScenarios", $"{scenario.ToLower()}.json");
        }
    }
}