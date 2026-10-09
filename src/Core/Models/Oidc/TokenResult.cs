namespace Altinn.Platform.Authentication.Core.Models.Oidc
{
    public sealed class TokenResult
    {
        public TokenResultKind Kind { get; init; }
        public string? Error { get; init; }
        public string? ErrorDescription { get; init; }

        // On success
        public string? AccessToken { get; init; }
        public string? IdToken { get; init; }
        public long ExpiresIn { get; init; }
        public string? Scope { get; init; }

        public string? RefreshToken { get; init; }

        public long? RefreshTokenExpiresIn { get; init; }

        public static TokenResult Success(string accessToken, string? idToken, long expiresIn, string? scope, string? refreshToken, long? refreshTokenExpiresIn) =>
            new() { Kind = TokenResultKind.Success, AccessToken = accessToken, IdToken = idToken, ExpiresIn = expiresIn, Scope = scope, RefreshToken = refreshToken, RefreshTokenExpiresIn = refreshTokenExpiresIn };

        public static TokenResult InvalidClient(string desc) => new() { Kind = TokenResultKind.InvalidClient, Error = "invalid_client", ErrorDescription = desc };
        public static TokenResult InvalidRequest(string desc) => new() { Kind = TokenResultKind.InvalidRequest, Error = "invalid_request", ErrorDescription = desc };
        public static TokenResult InvalidGrant(string desc) => new() { Kind = TokenResultKind.InvalidGrant, Error = "invalid_grant", ErrorDescription = desc };
        public static TokenResult ServerError(string desc) => new() { Kind = TokenResultKind.ServerError, Error = "server_error", ErrorDescription = desc };
    }
}
