using System;

namespace Altinn.Platform.Authentication.Tests.Models
{
    public class LoginTestState
    {
        public string SessionId { get; set; } = null!;

        public string CookieHeader { get; set; } = null!;

        public string DownstreamState { get; set; } = null!;

        public string DownstreamNonce { get; set; } = null!;

        public string DownstreamCodeVerifier { get; set; } = null!;

        public string DownstreamCodeChallenge { get; set; } = null!;

        public string UpstreamProviderCode { get; set; } = null!;
    }
}
