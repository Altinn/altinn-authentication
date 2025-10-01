#nullable enable
using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Core.Helpers;
using Altinn.Platform.Authentication.Core.Models.Oidc;
using Altinn.Platform.Authentication.Core.RepositoryInterfaces;
using Altinn.Platform.Authentication.Core.Services.Interfaces;
using Altinn.Platform.Authentication.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
        ILogger<TokenService> logger,
        IOptions<GeneralSettings> generalSettings,
        IRefreshTokenRepository refreshTokenRepository,
        IOidcSessionRepository oidcSessionRepository) : ITokenService
    {
        private readonly ILogger<TokenService> _logger = logger;
        private readonly IAuthorizationCodeRepository _authorizationCodeRepository = authorizationCodeRepository;
        private readonly GeneralSettings _generalSettings = generalSettings.Value;
        private readonly IRefreshTokenRepository _refreshTokenRepository = refreshTokenRepository;
        private readonly IOidcSessionRepository _oidcSessionRepository = oidcSessionRepository;

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

            OidcSession? oidcSession = await _oidcSessionRepository.GetBySidAsync(row.SessionId, ct);

            // 5) Issue tokens (ID + Access)
            DateTimeOffset expiry = time.GetUtcNow().AddMinutes(_generalSettings.JwtValidityMinutes);

            string accessToken = await tokenIssuer.CreateAccessTokenAsync(row, expiry, ct);
            string? idToken = await tokenIssuer.CreateIdTokenAsync(row, client, expiry, ct); // include nonce, acr, auth_time, sid
 
            string? refreshToken = await TryIssueInitialRefreshAsync(row, oidcSession!, client, time, ct);

            // Now atomically consume
            if (!await _authorizationCodeRepository.TryConsumeAsync(row.Code, row.ClientId, row.RedirectUri, time.GetUtcNow(), ct))
            {
                return TokenResult.InvalidGrant("Code already used or expired");
            }

            return TokenResult.Success(accessToken, idToken, expiry.ToUnixTimeSeconds(), string.Join(" ", row.Scopes));
        }

        private async Task<(OidcClient? Client, TokenResult Error)> AuthenticateClientAsync(TokenClientAuth auth, CancellationToken ct)
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

        private async Task<string?> TryIssueInitialRefreshAsync(
            AuthCodeRow codeRow,
            OidcSession session,
            OidcClient client,
            TimeProvider time,
            CancellationToken ct)
        {
            // Gate by policy
            if (!client.AllowsRefresh)
            {
                return null;
            }

            // See if we need to have this stored in KeyVault or we can just set it on pod deployment

            byte[] serverPepper = Convert.FromBase64String(_generalSettings.OidcRefreshTokenPepper);

            //if (client.RequireOfflineAccessScope && !codeRow.Scopes.Contains("offline_access"))
            //{ // Do we really want to do this?
            //    return null;
            //}

            // Generate opaque token
            string refreshToken = CryptoHelpers.RandomBase64Url(48);

            // Fast lookup key + slow hash for storage
            byte[] lookupKey = RefreshTokenCrypto.ComputeLookupKey(refreshToken, serverPepper);
            var (hash, salt, iters) = RefreshTokenCrypto.HashForStorage(
                refreshToken, iterations: 600_000);

            var now = time.GetUtcNow();
            var sliding = now.AddDays(30);
            var absolute = now.AddDays(90);

            // One family per (client, subject, session) – makes revocation by session easy
            Guid familyId = await _refreshTokenRepository.GetOrCreateFamilyAsync(client.ClientId, codeRow.SubjectId, session.Sid, ct);

            await _refreshTokenRepository.InsertAsync(
                new RefreshTokenRow
                {
                    TokenId = Guid.NewGuid(),
                    FamilyId = familyId,
                    Status = "active",
                    LookupKey = lookupKey,
                    Hash = hash,
                    Salt = salt,
                    Iterations = iters,

                    ClientId = codeRow.ClientId,
                    SubjectId = codeRow.SubjectId,
                    OpSid = session.Sid,

                    Scopes = codeRow.Scopes.ToArray(),
                    Acr = session.Acr,
                    Amr = session.Amr,
                    AuthTime = session.AuthTime,

                    CreatedAt = now,
                    ExpiresAt = sliding,
                    AbsoluteExpiresAt = absolute
                },
                ct);

            return refreshToken; // return once; never store raw
        }

    }

}
