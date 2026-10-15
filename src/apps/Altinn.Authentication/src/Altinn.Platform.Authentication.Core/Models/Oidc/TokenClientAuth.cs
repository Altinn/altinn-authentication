namespace Altinn.Platform.Authentication.Core.Models.Oidc
{
    public sealed class TokenClientAuth
    {
        public TokenClientAuthType Type { get; }
        public string? ClientId { get; }
        public string? ClientSecret { get; }
        public string? AssertionType { get; }
        public string? Assertion { get; }

        private TokenClientAuth(TokenClientAuthType type, string? clientId = null, string? secret = null, string? assertionType = null, string? assertion = null)
        { Type = type; ClientId = clientId; ClientSecret = secret; AssertionType = assertionType; Assertion = assertion; }

        public static TokenClientAuth ClientSecretBasic(string clientId, string secret) => new(TokenClientAuthType.ClientSecretBasic, clientId, secret);
        public static TokenClientAuth ClientSecretPost(string clientId, string secret) => new(TokenClientAuthType.ClientSecretPost, clientId, secret);
        public static TokenClientAuth PrivateKeyJwt(string clientId, string type, string jwt) => new(TokenClientAuthType.PrivateKeyJwt, clientId, null, type, jwt);
        public static TokenClientAuth None(string clientId) => new(TokenClientAuthType.None, clientId);
        public static TokenClientAuth Missing() => new(TokenClientAuthType.Missing);
    }
}
