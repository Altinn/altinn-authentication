namespace Altinn.Platform.Authentication.Core.Models.Oidc
{
    public sealed class TokenResponseDto
    {
        public string access_token { get; init; } = default!;
        public string token_type { get; init; } = "Bearer";
        public long expires_in { get; init; }
        public string? id_token { get; init; }
        public string? scope { get; init; }
        public string? refresh_token { get; init; }

        public long? refresh_token_expires_in { get; init; }
    }
}
