namespace Altinn.Platform.Authentication.Core.Models.Oidc
{
    public sealed class AuthorizeRequest
    {
        public string ResponseType { get; init; } = "code";
        public string ClientId { get; init; } = default!;
        public Uri RedirectUri { get; init; } = default!;
        public string[] Scopes { get; init; } = Array.Empty<string>();
        public string? State { get; init; }
        public string? Nonce { get; init; }
        public string CodeChallenge { get; init; } = default!;
        public string CodeChallengeMethod { get; init; } = "S256";
        public string[] AcrValues { get; init; } = Array.Empty<string>();
        public string[] Prompts { get; init; } = Array.Empty<string>();
        public string[] UiLocales { get; init; } = Array.Empty<string>();
        public int? MaxAge { get; init; }
        public string? RequestUri { get; init; }
        public string? RequestObject { get; init; }
        public string? ResponseMode { get; init; }
        public string? LoginHint { get; init; }
        public string? IdTokenHint { get; init; }
        public string? ClaimsJson { get; init; }
        public string? ClaimsLocales { get; init; }
        public string? AuthorizationDetailsJson { get; init; }
        public string? Resource { get; init; }
    }

    public static class AuthorizeRequestMapper
    {
        public static AuthorizeRequest Normalize(AuthorizeRequestDto dto)
        {
            static string[] SplitSpace(string? s) =>
                string.IsNullOrWhiteSpace(s) ? Array.Empty<string>() :
                s.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            static string[] SplitLocales(string? s) =>
                string.IsNullOrWhiteSpace(s) ? Array.Empty<string>() :
                s.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);

            var redirect = Uri.TryCreate(dto.RedirectUri ?? "", UriKind.Absolute, out var uri)
                ? uri
                : throw new ArgumentException("redirect_uri must be absolute");

            return new AuthorizeRequest
            {
                ResponseType = dto.ResponseType ?? "code",
                ClientId = dto.ClientId ?? throw new ArgumentException("client_id is required"),
                RedirectUri = uri!,
                Scopes = SplitSpace(dto.Scope),
                State = dto.State,
                Nonce = dto.Nonce,
                CodeChallenge = dto.CodeChallenge ?? throw new ArgumentException("code_challenge is required"),
                CodeChallengeMethod = dto.CodeChallengeMethod ?? "S256",
                AcrValues = SplitSpace(dto.AcrValues),
                Prompts = SplitSpace(dto.Prompt),
                UiLocales = SplitLocales(dto.UiLocales),
                MaxAge = dto.MaxAge,
                RequestUri = dto.RequestUri,
                RequestObject = dto.RequestObject,
                ResponseMode = dto.ResponseMode,
                LoginHint = dto.LoginHint,
                IdTokenHint = dto.IdTokenHint,
                ClaimsJson = dto.Claims,
                ClaimsLocales = dto.ClaimsLocales,
                AuthorizationDetailsJson = dto.AuthorizationDetails,
                Resource = dto.Resource
            };
        }
    }
}
