#nullable enable
using System;
using System.Collections.Generic;
using System.Web;
using Altinn.Platform.Authentication.Core.Helpers;

namespace Altinn.Platform.Authentication.Tests.Models
{
    /// <summary>
    /// Helper model to keep track of an OIDC test scenario.
    /// To reduce the amount of boilerplate in the test scenario in each test case,
    /// Support multiple logins in same session
    /// </summary>
    public class OidcTestScenario
    {
        public required string ScenarioId { get; set; }

        public required string Title { get; set; }

        public required string Description { get; set; }

        public required string Ssn { get; set; }

        public string? ExternalIdentity { get; set; }

        public int? UserId { get; set; }

        public int? PartyId { get; set; }

        public string? UserName { get; set; }

        public string? DownstreamClientId { get; set; } = null;

        public string? DaownstreamNonce { get; set; } = null;

        public string? DaownstreamState { get; set; } = null;

        public string? DaownstreamCodeVerifier { get; internal set; }

        public string? DaownstreamCodeChallenge { get; internal set; }

        public string DownstreamClientCallbackUrl { get; set; } = "https://af.altinn.no/api/cb";

        public string? UpstreamProviderCode { get; internal set; }

        public List<Uri> RedirectUris { get; set; } = [new Uri("https://af.altinn.no/api/cb")];

        public List<string> Scopes { get; set; } = ["openid", "altinn:portal/enduser"];

        public List<string> AllowedScopes { get; set; } = ["openid", "altinn:portal/enduser"];

        public List<string> Acr { get; set; } = ["idporten-loa-high"];

        public List<string> Amr { get; set; } = ["BankID Mobil"];

        public Dictionary<string, string>? CustomClaims { get; set; }

        public string? ClientSecret { get; set; }

        public string? HashedClientSecret { get; set; }

        /// <summary>
        /// Which is the current login count for this scenario.
        /// </summary>
        public int? LoginCount { get; set; } = 1;

        /// <summary>
        /// The number of upstream logins to perform in this scenario.
        /// </summary>
        public int NumberOfLogins { get; set; } = 1;

        public List<LoginTestState> LoginStates { get; set; } = new();

        public string GetAuthorizationRequestUrl()
        {
            // Downstream authorize query (what Arbeidsflate would send)
            string redirectUri = Uri.EscapeDataString(DownstreamClientCallbackUrl);

            string url =
                "/authentication/api/v1/authorize" +
                $"?redirect_uri={redirectUri}" +
                $"&scope={Uri.EscapeDataString(string.Join(" ", Scopes))}" +
                $"&acr_values={GetAcr()}" +
                $"&state={GetDownstreamState()}" +
                $"&client_id={DownstreamClientId}" +
                "&response_type=code" +
                $"&nonce={GetDownstreamNonce()}" +
                $"&code_challenge={GetDownstreamCodeChallenge()}" +
                "&code_challenge_method=S256";

            return url;
        }

        public string GetDownstreamState(int loginAttempt = 0)
        {
            if (loginAttempt == 0)
            {
                loginAttempt = LoginCount ?? 1;
            }

            return LoginStates[loginAttempt - 1].DownstreamState;
        }

        public string GetDownstreamNonce(int loginAttempt = 0)
        {
            if (loginAttempt == 0)
            {
                loginAttempt = LoginCount ?? 1;
            }

            return LoginStates[loginAttempt - 1].DownstreamNonce;
        }

        public string GetDownstreamCodeVerifier(int loginAttempt = 0)
        {
            if (loginAttempt == 0)
            {
                loginAttempt = LoginCount ?? 1;
            }

            return LoginStates[loginAttempt - 1].DownstreamCodeVerifier;
        }

        public string GetDownstreamCodeChallenge(int loginAttempt = 0)
        {
            if (loginAttempt == 0)
            {
                loginAttempt = LoginCount ?? 1;
            }

            return LoginStates[loginAttempt - 1].DownstreamCodeChallenge;
        }

        public void SetLoginAttempt(int attempt)
        {
            if (LoginStates.Count < attempt)
            {
                LoginTestState state = new()
                {
                    DownstreamState = CryptoHelpers.RandomBase64Url(32),
                    DownstreamNonce = CryptoHelpers.RandomBase64Url(32),
                    DownstreamCodeVerifier = Pkce.RandomPkceVerifier(),
                };

                state.DownstreamCodeChallenge = Pkce.ComputeS256CodeChallenge(state.DownstreamCodeVerifier);
                LoginStates.Add(state);
            }

            LoginCount = attempt;
        }

        private string GetAcr()
        {
            return Acr[0];
        }
    }
}
