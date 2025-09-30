using System;

namespace Altinn.Platform.Authentication.Tests.Models
{
    public class OidcTestScenario
    {
        public required string ScenarioId { get; set; }

        public required string Title { get; set; }

        public required string Description { get; set; }

        public required string Ssn { get; set; }

        public string? DownstreamClientId { get; set; } = null;

        public string? DownstreamNonce { get; set; } = null;

        public string? DownstreamState { get; set; } = null;

        public string DownstreamCodeVerifier { get; internal set; }

        public string DownstreamCodeChallenge { get; internal set; }

        public string DownstreamClientCallbackUrl { get; set; } = "https://af.altinn.no/api/cb";
        
        public string UpstreamProviderCode { get; internal set; }

        public string GetAuthorizationRequestUrl()
        {
            // Downstream authorize query (what Arbeidsflate would send)
            string redirectUri = Uri.EscapeDataString(DownstreamClientCallbackUrl);

            string url =
                "/authentication/api/v1/authorize" +
                $"?redirect_uri={redirectUri}" +
                "&scope=openid%20altinn%3Aportal%2Fenduser" +
                "&acr_values=idporten-loa-substantial" +
                $"&state={DownstreamState}" +
                $"&client_id={DownstreamClientId}" +
                "&response_type=code" +
                $"&nonce={DownstreamNonce}" +
                $"&code_challenge={DownstreamCodeChallenge}" +
                "&code_challenge_method=S256";

            return url;
        }
    }
}
