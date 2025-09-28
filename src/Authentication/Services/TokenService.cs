#nullable enable
using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Core.Helpers;
using Altinn.Platform.Authentication.Core.Models.Oidc;
using Altinn.Platform.Authentication.Core.RepositoryInterfaces;
using Altinn.Platform.Authentication.Core.Services.Interfaces;
using Altinn.Platform.Authentication.Helpers;
using Microsoft.Extensions.Logging;

namespace Altinn.Platform.Authentication.Services
{
    /// <summary>
    /// Token service implementation for handling OIDC token requests.
    /// </summary>
    public sealed class TokenService(
        IOidcServerClientRepository clientRepo,
        ITokenIssuer tokenIssuer,
        IAuthorizationCodeRepository authorizationCodeRepository, 
        TimeProvider time,
        ILogger<TokenService> logger) : ITokenService
    {
        private readonly ILogger<TokenService> _logger = logger;
        private readonly IAuthorizationCodeRepository _authorizationCodeRepository = authorizationCodeRepository;

        /// <inheritdoc/>
        public async Task<TokenResult> ExchangeAuthorizationCodeAsync(TokenRequest request, CancellationToken ct)
        {
            // 1) Basic checks
            if (string.IsNullOrWhiteSpace(request.Code))
            {
                return TokenResult.InvalidRequest("Missing code");
            }

            if (request.RedirectUri is null || !request.RedirectUri.IsAbsoluteUri)
            {
                return TokenResult.InvalidRequest("redirect_uri must be absolute");
            }

            // 2) Authenticate client
            var (client, clientIdOrError) = await AuthenticateClientAsync(request.ClientAuth, ct);
            if (client is null)
            {
                return clientIdOrError; // an error TokenResult
            }

            // 3) Load authorization_code row
            AuthCodeRow? row = await _authorizationCodeRepository.GetAsync(request.Code, ct);
            if (row is null)
            {
                return TokenResult.InvalidGrant("Invalid code");
            }

            if (row.Used)
            {
                return TokenResult.InvalidGrant("Code already used");
            }

            if (row.ExpiresAt <= time.GetUtcNow())
            {
                return TokenResult.InvalidGrant("Code expired");
            }

            // Bindings
            if (!string.Equals(row.ClientId, client.ClientId, StringComparison.Ordinal))
            {
                return TokenResult.InvalidGrant("Code not issued to this client");
            }

            if (row.RedirectUri != request.RedirectUri)
            {
                return TokenResult.InvalidGrant("redirect_uri mismatch");
            }

            // 4) PKCE verification
            if (string.IsNullOrWhiteSpace(request.CodeVerifier))
            {
                return TokenResult.InvalidRequest("code_verifier required");
            }

            if (!Pkce.VerifyS256(row.CodeChallenge, request.CodeVerifier))
            {
                return TokenResult.InvalidGrant("Invalid PKCE code_verifier");
            }

            // 5) Issue tokens (ID + Access)
            DateTimeOffset now = time.GetUtcNow();
            var (accessToken, atExpires, atScope) = await tokenIssuer.CreateAccessTokenAsync(row, now, ct);

            string? idToken = await tokenIssuer.CreateIdTokenAsync(row, client, now, ct); // include nonce, acr, auth_time, sid
            int expiresIn = (int)Math.Max(0, (atExpires - now).TotalSeconds);

            // 6) Atomically mark code as used (and write jti etc. if needed)
            try
            {
                await _authorizationCodeRepository.ConsumeAsync(request.Code, now, ct);
            }
            catch (DBConcurrencyException)
            {
                // Lost race: treat as invalid_grant
                return TokenResult.InvalidGrant("Code already consumed");
            }

            return TokenResult.Success(accessToken, idToken, expiresIn, atScope);
        }

        private async Task<(OidcClient? client, TokenResult error)> AuthenticateClientAsync(TokenClientAuth auth, CancellationToken ct)
        {
            // Look up client
            OidcClient? client = null;

            switch (auth.Type)
            {
                case TokenClientAuthType.ClientSecretBasic:
                case TokenClientAuthType.ClientSecretPost:
                    if (string.IsNullOrWhiteSpace(auth.ClientId) || string.IsNullOrWhiteSpace(auth.ClientSecret))
                    {
                        return (null, TokenResult.InvalidClient("Missing client credentials"));
                    }

                    client = await clientRepo.GetClientAsync(auth.ClientId!, ct);
                    if (client is null)
                    {
                        return (null, TokenResult.InvalidClient("Unknown client_id"));
                    }

                    if (!client.SupportsClientSecret())
                    {
                        return (null, TokenResult.InvalidClient("Client not allowed to use client_secret"));
                    }

                    if (!ClientSecrets.Verify(client.ClientSecretHash, auth.ClientSecret!))
                    {
                        return (null, TokenResult.InvalidClient("Invalid client secret"));
                    }

                    break;

                case TokenClientAuthType.PrivateKeyJwt:
                    // TODO: verify assertion signature (jwks or jwks_uri), aud must equal exact token endpoint URL, iss=sub=client_id
                    client = await clientRepo.GetClientAsync(auth.ClientId!, ct);
                    if (client is null)
                    {
                        return (null, TokenResult.InvalidClient("Unknown client_id"));
                    }

                    // validate JWT here...
                    break;

                case TokenClientAuthType.None:
                    client = await clientRepo.GetClientAsync(auth.ClientId!, ct);
                    if (client is null)
                    {
                        return (null, TokenResult.InvalidClient("Unknown client_id"));
                    }

                    return (null, TokenResult.InvalidClient("Client authentication required"));
                   
                case TokenClientAuthType.Missing:
                    return (null, TokenResult.InvalidClient("Client authentication missing"));
            }

            return (client, TokenResult.Success(string.Empty, null, 0, null)); // ignore payload; caller uses returned client
        }
    }

}
