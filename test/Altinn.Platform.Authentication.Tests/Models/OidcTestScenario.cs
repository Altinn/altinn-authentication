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
    }
}
