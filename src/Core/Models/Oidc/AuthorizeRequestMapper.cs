#nullable enable
using System.Net;

namespace Altinn.Platform.Authentication.Core.Models.Oidc
{
    /// <summary>
    /// Transforms an incoming DTO into a normalized internal model.
    /// </summary>
    public static class AuthorizeRequestMapper
    {
        public static AuthorizeRequest Normalize(
            AuthorizeRequestDto dto,
            IPAddress? clientIp = null,
            string? userAgentHash = null,
            Guid? correlationId = null)
        {
            static string[] SplitSpace(string? s) =>
                string.IsNullOrWhiteSpace(s) ? [] :
                s.Split([' '], StringSplitOptions.RemoveEmptyEntries);

            static string[] SplitLocales(string? s) =>
                string.IsNullOrWhiteSpace(s) ? [] :
                s.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);

            if (string.IsNullOrWhiteSpace(dto.RedirectUri))
            {
                throw new ArgumentException("redirect_uri is required.", nameof(dto));
            }
            
            if (!Uri.TryCreate(dto.RedirectUri, UriKind.Absolute, out var uri))
            {
               throw new ArgumentException($"redirect_uri must be an absolute URI. Received: {dto.RedirectUri}", nameof(dto));
            }


            return new AuthorizeRequest
            {
                ResponseType = dto.ResponseType ?? "code",
                ClientId = dto.ClientId,
                RedirectUri = uri,
                Scopes = SplitSpace(dto.Scope),
                State = dto.State,
                Nonce = dto.Nonce,
                CodeChallenge = dto.CodeChallenge,
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
                Resource = dto.Resource,

                // new context fields
                ClientIp = clientIp,
                UserAgentHash = userAgentHash,
                CorrelationId = correlationId
            };
        }
    }
}
