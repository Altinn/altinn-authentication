#nullable enable
using System;
using System.Diagnostics;
using System.Linq;
using System.Security.Claims;
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
using Microsoft.IdentityModel.Abstractions;

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
            (TokenResult? validationErrorResult, OidcClient? client, AuthCodeRow? row) = await ValidateTokenRequest(request,ct);
            if (validationErrorResult != null)
            {
                return validationErrorResult;
            }

            Debug.Assert(client != null);
            Debug.Assert(row != null);

            OidcSession? oidcSession = await _oidcSessionRepository.GetBySidAsync(row.SessionId, ct);

            // 2) Issue tokens (ID + Access) + refresh token
            DateTimeOffset exchangeTime = time.GetUtcNow();
            DateTimeOffset expiry = exchangeTime.AddMinutes(_generalSettings.JwtValidityMinutes);
            ClaimsPrincipal idTokenPrincipal = ClaimsPrincipalBuilder.GetClaimsPrincipal(row, _generalSettings.PlatformEndpoint, true);
            ClaimsPrincipal accessTokenPrincipal = ClaimsPrincipalBuilder.GetClaimsPrincipal(row, _generalSettings.PlatformEndpoint, false);

            string? idToken = await tokenIssuer.CreateIdTokenAsync(idTokenPrincipal, client, expiry, ct);
            string accessToken = await tokenIssuer.CreateAccessTokenAsync(accessTokenPrincipal, expiry, ct);
            string? refreshToken = await TryIssueInitialRefreshAsync(row, oidcSession!, client, exchangeTime, ct);

            // Now atomically consume
            if (!await _authorizationCodeRepository.TryConsumeAsync(row.Code, row.ClientId, row.RedirectUri, time.GetUtcNow(), ct))
            {
                return TokenResult.InvalidGrant("Code already used or expired");
            }

            // 3) Update OP session (slide expiry, touch last seen)
            await _oidcSessionRepository.SlideExpiryToAsync(oidcSession!.Sid, exchangeTime.AddMinutes(_generalSettings.JwtValidityMinutes), ct);
            
            return TokenResult.Success(accessToken, idToken, _generalSettings.JwtValidityMinutes * 60, string.Join(" ", row.Scopes), refreshToken, _generalSettings.JwtValidityMinutes * 60);
        }

        /// <inheritdoc/>
        public async Task<TokenResult> RefreshAsync(RefreshTokenRequest request, CancellationToken ct)
        {
            // 1) Basic checks
            DateTimeOffset now = time.GetUtcNow();
           
            (TokenResult? value, OidcClient? client, byte[]? serverPepper, RefreshTokenRow? row, string[]? resultingScopes) = await ValidateRefreshRequest(request, now, ct);
            if (value != null)
            {
                return value;
            }

            Debug.Assert(client != null);
            Debug.Assert(serverPepper != null);
            Debug.Assert(row != null);
            Debug.Assert(resultingScopes != null);

            // 7) Rotate refresh token (every use)
            string newRefreshToken = CryptoHelpers.RandomBase64Url(48);
            byte[] newLookupKey = RefreshTokenCrypto.ComputeLookupKey(newRefreshToken, serverPepper);
            var (newHash, newSalt, newIters) = RefreshTokenCrypto.HashForStorage(newRefreshToken, iterations: row.Iterations);

            var newRow = new RefreshTokenRow
            {
                TokenId = Guid.NewGuid(),
                FamilyId = row.FamilyId,
                Status = "active",
                LookupKey = newLookupKey,
                Hash = newHash,
                Salt = newSalt,
                Iterations = newIters,

                ClientId = row.ClientId,
                SubjectId = row.SubjectId,
                ExternalId = row.ExternalId,
                SubjectPartyId = row.SubjectPartyId,
                SubjectPartyUuid = row.SubjectPartyUuid,
                SubjectUserId = row.SubjectUserId,
                OpSid = row.OpSid,

                Scopes = resultingScopes,
                Acr = row.Acr,
                Amr = row.Amr,
                AuthTime = row.AuthTime,

                CreatedAt = now,
                ExpiresAt = now.AddMinutes(_generalSettings.JwtValidityMinutes),            // sliding window (tune policy)
                AbsoluteExpiresAt = row.AbsoluteExpiresAt, // do NOT extend hard cap,
                SessionId = row.SessionId,
                ProviderClaims = row.ProviderClaims,
            };

            await _refreshTokenRepository.InsertAsync(newRow, ct);
            await _refreshTokenRepository.MarkUsedAsync(row.TokenId, newRow.TokenId, ct);
            
            // 8) Mint new access token (+ optional ID token)
            DateTimeOffset atExpiry = now.AddMinutes(_generalSettings.JwtValidityMinutes);
            await _oidcSessionRepository.SlideExpiryToAsync(row.OpSid, atExpiry, ct);

            ClaimsPrincipal accessPrincial = ClaimsPrincipalBuilder.GetClaimsPrincipal(row, _generalSettings.PlatformEndpoint, isIDToken: false);

            // Preferred: use issuer overloads that take the pieces directly (clean)
            string accessToken = await tokenIssuer.CreateAccessTokenAsync(
                accessPrincial,
                expiry: atExpiry,
                cancellationToken: ct);

            string? idToken = null;
            if (resultingScopes.Contains("openid"))
            {
                ClaimsPrincipal idtokenPrincipal = ClaimsPrincipalBuilder.GetClaimsPrincipal(row, _generalSettings.PlatformEndpoint, isIDToken: true);
                idToken = await tokenIssuer.CreateIdTokenAsync(
                    idtokenPrincipal,
                    client,
                    now: atExpiry,
                    cancellationToken: ct);
            }

            // 9) Compose response
            return TokenResult.Success(
                accessToken: accessToken,
                idToken: idToken,
                expiresIn: _generalSettings.JwtValidityMinutes * 60,
                scope: string.Join(' ', resultingScopes),
                refreshToken: newRefreshToken,
                _generalSettings.JwtValidityMinutes * 60);
        }

        /// <inheritdoc/>
        public Task<string> CreateCookieToken(OidcSession oidcSession, CancellationToken ct)
        {
           ClaimsPrincipal principal = ClaimsPrincipalBuilder.GetClaimsPrincipal(oidcSession, _generalSettings.PlatformEndpoint, isIDToken: false, isAuthCookie: true);
           return tokenIssuer.CreateAccessTokenAsync(principal, time.GetUtcNow().AddMinutes(_generalSettings.JwtValidityMinutes),  cancellationToken: ct);
        }

        private async Task<(TokenResult? Value, OidcClient? Client, byte[]? ServerPepper, RefreshTokenRow? Row, string[]? ResultingScopes)> ValidateRefreshRequest(RefreshTokenRequest request, DateTimeOffset now, CancellationToken ct)
        {
            byte[] serverPepper;
            RefreshTokenRow? row;
            if (string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                return (Value: TokenResult.InvalidRequest("Missing refresh_token"), null, null, null, null);
            }

            // 2) Authenticate client (supports Basic, post, private_key_jwt)
            var (client, clientAuthErr) = await AuthenticateClientAsync(request.ClientAuth, ct);
            if (client is null)
            {
                return (Value: clientAuthErr, null, null, null, null); // InvalidClient or InvalidRequest
            }

            try
            {
                serverPepper = Convert.FromBase64String(_generalSettings.OidcRefreshTokenPepper);
            }
            catch
            {
                _logger.LogError("Refresh token pepper is not configured or invalid.");
                return (Value: TokenResult.ServerError("server configuration error"), null, null, null, null);
            }

            byte[] lookupKey = RefreshTokenCrypto.ComputeLookupKey(request.RefreshToken, serverPepper);
            row = await _refreshTokenRepository.GetByLookupKeyAsync(lookupKey, ct);
            if (row is null)
            {
                // RFC: invalid_grant for unknown/invalid
                return (Value: TokenResult.InvalidGrant("Invalid refresh_token"), null, null, null, null);
            }

            if (!RefreshTokenCrypto.Verify(request.RefreshToken, row.Salt, row.Iterations, row.Hash))
            {
                return (Value: TokenResult.InvalidGrant("Invalid refresh_token"), null, null, null, null);
            }

            // 4) Status & expiry
            if (!string.Equals(row.Status, "active", StringComparison.Ordinal))
            {
                // Reuse detection → revoke entire family
                await _refreshTokenRepository.RevokeFamilyAsync(row.FamilyId, "reuse-detected", ct);
                return (Value: TokenResult.InvalidGrant("Invalid refresh_token"), null, null, null, null);
            }

            if (now > row.AbsoluteExpiresAt)
            {
                await _refreshTokenRepository.RevokeFamilyAsync(row.FamilyId, "absolute-expired", ct);
                return (Value: TokenResult.InvalidGrant("Invalid refresh_token"), null, null, null, null);
            }

            if (now > row.ExpiresAt)
            {
                await _refreshTokenRepository.RevokeAsync(row.TokenId, "expired", ct);
                return (Value: TokenResult.InvalidGrant("Invalid refresh_token"), null, null, null, null);
            }

            // 5) Binding checks
            if (!string.Equals(row.ClientId, client.ClientId, StringComparison.Ordinal))
            {
                await _refreshTokenRepository.RevokeFamilyAsync(row.FamilyId, "client-mismatch", ct);
                return (Value: TokenResult.InvalidGrant("Invalid refresh_token"), null, null, null, null);
            }

            // Optional but recommended: ensure OP session not ended/expired
            var session = await _oidcSessionRepository.GetBySidAsync(row.OpSid, ct);
            if (session is null)
            {
                await _refreshTokenRepository.RevokeFamilyAsync(row.FamilyId, "session-missing", ct);
                return (Value: TokenResult.InvalidGrant("Invalid refresh_token"), null, null, null, null);
            }

            if (session.ExpiresAt is DateTimeOffset exp && now > exp)
            {
                await _refreshTokenRepository.RevokeFamilyAsync(row.FamilyId, "session-expired", ct);
                return (Value: TokenResult.InvalidGrant("Invalid refresh_token"), null, null, null, null);
            }

            // 6) Down-scope (if requested); must be subset of original scopes
            string[] resultingScopes = row.Scopes;
            if (!string.IsNullOrWhiteSpace(request.Scope))
            {
                var requested = request.Scope.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (requested.Length == 0)
                {
                    return (Value: TokenResult.InvalidRequest("scope must be non-empty when provided"), null, null, null, null);
                }

                resultingScopes = requested.Intersect(row.Scopes, StringComparer.Ordinal).ToArray();

                // If any requested scope is not in the original set, reject (strict subset)
                if (resultingScopes.Length != requested.Length)
                {
                    return (Value: TokenResult.InvalidGrant("Requested scope exceeds originally granted scope"), null, null, null, null);
                }
            }

            return (Value: null, client, serverPepper, row, resultingScopes);
        }

        private async Task<(TokenResult? Value, OidcClient? Client, AuthCodeRow? Row)> ValidateTokenRequest(TokenRequest request, CancellationToken ct)
        {
            OidcClient? client;
            AuthCodeRow? row;
            if (string.IsNullOrWhiteSpace(request.Code))
            {
                return (Value: TokenResult.InvalidRequest("Missing code"), Client: null, Row: null);
            }

            if (request.RedirectUri is null || !request.RedirectUri.IsAbsoluteUri)
            {
                return (Value: TokenResult.InvalidRequest("redirect_uri must be absolute"), null, null);
            }

            // 2) Authenticate client
            (client, TokenResult clientIdOrError) = await AuthenticateClientAsync(request.ClientAuth, ct);
            if (client is null)
            {
                return (Value: clientIdOrError, null, null); // an error TokenResult
            }

            // 3) Load authorization_code row
            row = await _authorizationCodeRepository.GetAsync(request.Code, ct);
            if (row is null)
            {
                return (Value: TokenResult.InvalidGrant("Invalid code"), null, null);
            }

            if (row.Used)
            {
                return (Value: TokenResult.InvalidGrant("Code already used"), null, null);
            }

            if (row.ExpiresAt <= time.GetUtcNow())
            {
                return (Value: TokenResult.InvalidGrant("Code expired"), null, null);
            }

            // Bindings
            if (!string.Equals(row.ClientId, client.ClientId, StringComparison.Ordinal))
            {
                return (Value: TokenResult.InvalidGrant("Code not issued to this client"), null, null);
            }

            if (row.RedirectUri != request.RedirectUri)
            {
                return (Value: TokenResult.InvalidGrant("redirect_uri mismatch"), null, null);
            }

            // 4) PKCE verification
            if (string.IsNullOrWhiteSpace(request.CodeVerifier))
            {
                return (Value: TokenResult.InvalidRequest("code_verifier required"), null, null);
            }

            if (!Pkce.VerifyS256(row.CodeChallenge, request.CodeVerifier))
            {
                return (Value: TokenResult.InvalidGrant("Invalid PKCE code_verifier"), null, null);
            }

            return (Value: null, client, row);
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

                    // We dont support public clients. Client is required to authenticate
                    return (null, TokenResult.InvalidClient("Client authentication required"));
                   
                case TokenClientAuthType.Missing:
                    return (null, TokenResult.InvalidClient("Client authentication missing"));
            }

            return (client, TokenResult.Success(string.Empty, null, 0, null, null, null)); // ignore payload; caller uses returned client
        }

        private async Task<string?> TryIssueInitialRefreshAsync(
            AuthCodeRow codeRow,
            OidcSession session,
            OidcClient client,
            DateTimeOffset now,
            CancellationToken ct)
        {
            // Gate by policy
            if (!client.AllowsRefresh)
            {
                return null;
            }

            // See if we need to have this stored in KeyVault or we can just set it on pod deployment

            byte[] serverPepper;
            try
            {
                serverPepper = Convert.FromBase64String(_generalSettings.OidcRefreshTokenPepper);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Refresh token pepper is not configured or invalid.");
                return null; // fail closed: issue no refresh token, still return access/id token
            }

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

            DateTimeOffset sliding = now.AddMinutes(_generalSettings.JwtValidityMinutes);
            DateTimeOffset absolute = now.AddMinutes(_generalSettings.MaxSessionTimeInMinutes);

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
                    ExternalId = codeRow.ExternalId,
                    SubjectPartyId = codeRow.SubjectPartyId,
                    SubjectPartyUuid = codeRow.SubjectPartyUuid,
                    SubjectUserId = codeRow.SubjectUserId,
                    OpSid = session.Sid,

                    Scopes = codeRow.Scopes.ToArray(),
                    Acr = session.Acr,
                    Amr = session.Amr,
                    AuthTime = session.AuthTime,

                    CreatedAt = now,
                    ExpiresAt = sliding,
                    AbsoluteExpiresAt = absolute,
                    SessionId = session.Sid,
                    ProviderClaims = session.ProviderClaims
                },
                ct);

            return refreshToken; // return once; never store raw
        }
    }
}
