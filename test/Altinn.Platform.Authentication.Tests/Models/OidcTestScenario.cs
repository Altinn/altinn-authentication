#nullable enable
using System;
using System.Collections.Generic;
using System.Web;

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

        public string? DownstreamCodeVerifier { get; internal set; }

        public string? DownstreamCodeChallenge { get; internal set; }

        public string DownstreamClientCallbackUrl { get; set; } = "https://af.altinn.no/api/cb";

        public string? UpstreamProviderCode { get; internal set; }

        public List<Uri> RedirectUris { get; set; } = [new Uri("https://af.altinn.no/api/cb")];

        public List<string> Scopes { get; set; } = ["openid", "altinn:portal/enduser"];

        public List<string> AllowedScopes { get; set; } = ["openid", "altinn:portal/enduser"];

        public List<string> Acr { get; set; } = ["idporten-loa-substantial"];

        public List<string> Amr { get; set; } = ["BankID Mobil"];

        public string? ClientSecret { get; set; } 

        public string? HashedClientSecret { get; set; }

        public string GetAuthorizationRequestUrl()
        {
            // Downstream authorize query (what Arbeidsflate would send)
            string redirectUri = Uri.EscapeDataString(DownstreamClientCallbackUrl);

            string url =
                "/authentication/api/v1/authorize" +
                $"?redirect_uri={redirectUri}" +
                $"&scope={Uri.EscapeDataString(string.Join(" ", Scopes))}" +
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
