using System.Collections.Generic;
using Altinn.Platform.Authentication.Core.Models.Oidc;
using Altinn.Platform.Authentication.Tests.Models;

namespace Altinn.Platform.Authentication.Tests.Helpers
{   
    public static class OidcServerTestUtils
    {
        public static Dictionary<string, string> GetRefreshForm(OidcTestScenario testScenario, OidcClientCreate create, string refreshToken)
        {
            var refreshForm = new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
                ["client_id"] = create.ClientId,
                ["client_secret"] = testScenario.ClientSecret // assuming your test client has this
            };
            return refreshForm;
        }

        public static OidcClientCreate NewClientCreate(OidcTestScenario testScenario) =>
            new()
            {
                ClientId = testScenario.DownstreamClientId,
                ClientName = "Test Client",
                ClientType = ClientType.Confidential,
                TokenEndpointAuthMethod = TokenEndpointAuthMethod.ClientSecretBasic,
                RedirectUris = testScenario.RedirectUris,
                AllowedScopes = testScenario.AllowedScopes,
                ClientSecretHash = testScenario.HashedClientSecret,
                ClientSecretExpiresAt = null,
                SecretRotationAt = null,
                JwksUri = null,
                JwksJson = null
            };

        public static Dictionary<string, string> BuildTokenRequestForm(OidcTestScenario testScenario, string code)
        {
            Dictionary<string, string> tokenForm = new()
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = testScenario.DownstreamClientCallbackUrl,
                ["client_id"] = testScenario.DownstreamClientId,
                ["client_secret"] = testScenario.ClientSecret!,
                ["code_verifier"] = testScenario.GetDownstreamCodeVerifier(),
            };
            return tokenForm;
        }
    }
}
