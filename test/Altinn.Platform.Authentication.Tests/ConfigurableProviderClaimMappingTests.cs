using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Core.Services.Interfaces;
using Altinn.Platform.Authentication.Enum;
using Altinn.Platform.Authentication.Helpers;
using Altinn.Platform.Authentication.Model;
using Altinn.Platform.Authentication.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace Altinn.Platform.Authentication.Tests
{
    /// <summary>
    /// Covers providers whose tokens do not follow ID-porten's claim conventions, using HelseID
    /// as the worked example: no <c>acr</c> claim, level in
    /// <c>helseid://claims/identity/security_level</c>, pid under a URI-style claim name, and an
    /// <c>amr</c> of <c>["pwd"]</c> that says nothing about the actual eID.
    /// </summary>
    public class ConfigurableProviderClaimMappingTests
    {
        private static OidcProvider HelseId() => new()
        {
            IssuerKey = "helseid",
            Issuer = "https://helseid-sts.test.nhn.no",
            ClaimMappings = new OidcClaimMappings
            {
                Pid = "helseid://claims/identity/pid",
                AuthLevel = "helseid://claims/identity/security_level",
                AuthMethod = "idp"
            },
            AuthLevels =
            [
                new() { Acr = "helseid-loa-high", Level = SecurityLevel.VerySensitive, UpstreamAcrValues = "Level4", ClaimValues = ["4"] },
                new() { Acr = "helseid-loa-substantial", Level = SecurityLevel.Sensitive, UpstreamAcrValues = "High", ClaimValues = ["3"] },
            ],
            AuthMethodMappings = new Dictionary<string, string>
            {
                ["bankid-oidc"] = "BankID",
                ["buypass-oidc"] = "BuyPass"
            },
            DefaultAuthenticationMethod = "NotDefined"
        };

        private static OidcProvider IdPorten() => new()
        {
            IssuerKey = "idporten",
            Issuer = "https://idporten.no"
        };

        private static JwtSecurityToken Token(params (string Type, string Value)[] claims)
        {
            List<Claim> list = [];
            foreach ((string type, string value) in claims)
            {
                list.Add(new Claim(type, value));
            }

            return new JwtSecurityToken(issuer: "https://helseid-sts.test.nhn.no", claims: list);
        }

        private static IAcrValueCatalog Catalog(params (string Key, OidcProvider Provider)[] providers)
        {
            OidcProviderSettings settings = [];
            foreach ((string key, OidcProvider provider) in providers)
            {
                settings.Add(key, provider);
            }

            return new OidcAcrValueCatalog(Options.Create(settings));
        }

        [Fact]
        public void GetUserFromToken_HelseId_MapsSecurityLevelClaimToAuthenticationLevel()
        {
            JwtSecurityToken token = Token(
                ("helseid://claims/identity/pid", "01017012345"),
                ("helseid://claims/identity/security_level", "4"),
                ("amr", "pwd"),
                ("idp", "bankid-oidc"));

            var result = AuthenticationHelper.GetUserFromToken(token, HelseId());

            // This is the defect the change exists to fix: previously no 'acr' claim meant the
            // level silently stayed at SelfIdentifed (0) for a user authenticated with BankID.
            Assert.Equal(SecurityLevel.VerySensitive, result.AuthenticationLevel);
            Assert.Equal("helseid-loa-high", result.Acr);
            Assert.Equal("01017012345", result.SSN);
        }

        [Fact]
        public void GetUserFromToken_HelseId_ResolvesMethodFromConfiguredClaim()
        {
            JwtSecurityToken token = Token(
                ("helseid://claims/identity/security_level", "4"),
                ("idp", "bankid-oidc"));

            var result = AuthenticationHelper.GetUserFromToken(token, HelseId());

            Assert.Equal(AuthenticationMethod.BankID, result.AuthenticationMethod);
        }

        [Fact]
        public void GetUserFromToken_IdPorten_BehaviourUnchanged()
        {
            JwtSecurityToken token = Token(
                ("pid", "01017012345"),
                ("acr", "idporten-loa-high"),
                ("amr", "BankID"));

            var result = AuthenticationHelper.GetUserFromToken(token, IdPorten());

            Assert.Equal(SecurityLevel.VerySensitive, result.AuthenticationLevel);
            Assert.Equal("idporten-loa-high", result.Acr);
            Assert.Equal("01017012345", result.SSN);
            Assert.Equal(AuthenticationMethod.BankID, result.AuthenticationMethod);
        }

        [Fact]
        public void GetUserFromToken_UnknownLevelValue_StaysAtLowestLevel()
        {
            JwtSecurityToken token = Token(("helseid://claims/identity/security_level", "99"));

            var result = AuthenticationHelper.GetUserFromToken(token, HelseId());

            Assert.Equal(SecurityLevel.SelfIdentifed, result.AuthenticationLevel);
        }

        [Fact]
        public void GetUserFromToken_HelseIdWithoutPid_LeavesNoIdentifierForSessionCreation()
        {
            // A HelseID token whose pid claim is missing — wrong ClaimMappings.Pid, or the
            // helseid://scopes/identity/pid scope not granted. None of the identifiers
            // IdentifyOrCreateAltinnUser branches on are populated, so it falls through to the
            // guard that now aborts the sign-in rather than creating a session with a null subject.
            JwtSecurityToken token = Token(
                ("helseid://claims/identity/security_level", "4"),
                ("idp", "bankid-oidc"));

            var result = AuthenticationHelper.GetUserFromToken(token, HelseId());

            Assert.Null(result.SSN);
            Assert.Null(result.ExternalIdentity);
            Assert.Null(result.Email);
            Assert.NotEqual("selfregistered-email", result.Acr);
        }

        [Fact]
        public void Catalog_AllowsConfiguredAcrValuesFromEveryProvider()
        {
            IAcrValueCatalog catalog = Catalog(("idporten", IdPorten()), ("helseid", HelseId()));

            Assert.Contains("idporten-loa-high", catalog.AllowedAcrValues);
            Assert.Contains("helseid-loa-high", catalog.AllowedAcrValues);

            // 'level4' is a claim value for idporten-loa-high, not a requestable acr.
            Assert.DoesNotContain("level4", catalog.AllowedAcrValues);
        }

        [Fact]
        public void Catalog_RoutesAcrToTheProviderThatOffersIt()
        {
            IAcrValueCatalog catalog = Catalog(("idporten", IdPorten()), ("helseid", HelseId()));

            Assert.Equal("helseid", catalog.ResolveProviderKey(["helseid-loa-high"]));
            Assert.Equal("idporten", catalog.ResolveProviderKey(["idporten-loa-high"]));
            Assert.Null(catalog.ResolveProviderKey(["something-bogus"]));
        }

        [Fact]
        public void Catalog_TranslatesAcrToTheTargetProvidersVocabularyOnly()
        {
            IAcrValueCatalog catalog = Catalog(("idporten", IdPorten()), ("helseid", HelseId()));

            Assert.Equal("Level4", catalog.GetUpstreamAcrValues("helseid", ["helseid-loa-high"]));

            // ID-porten's vocabulary must never be forwarded to HelseID, which does not know it.
            Assert.Null(catalog.GetUpstreamAcrValues("helseid", ["idporten-loa-high", "selfregistered-email"]));
        }

        [Fact]
        public void NeedAcrUpgrade_SessionWithoutResolvableAcr_StepsUpInsteadOfPassing()
        {
            IAcrValueCatalog catalog = Catalog(("idporten", IdPorten()), ("helseid", HelseId()));

            // Previously this returned false — a session carrying no acr satisfied a request for
            // any level, which is fail-open for every provider that does not emit acr.
            Assert.True(AuthenticationHelper.NeedAcrUpgrade(null, ["idporten-loa-high"], catalog));
        }

        [Fact]
        public void NeedAcrUpgrade_ComparesLevelsAcrossProviders()
        {
            IAcrValueCatalog catalog = Catalog(("idporten", IdPorten()), ("helseid", HelseId()));

            // Both are level 4, so a HelseID session already satisfies a request for ID-porten high.
            Assert.False(AuthenticationHelper.NeedAcrUpgrade("helseid-loa-high", ["idporten-loa-high"], catalog));

            // Level 3 does not satisfy a request for level 4.
            Assert.True(AuthenticationHelper.NeedAcrUpgrade("helseid-loa-substantial", ["idporten-loa-high"], catalog));
        }

        [Fact]
        public void NeedAcrUpgrade_NothingRequested_ReusesSession()
        {
            IAcrValueCatalog catalog = Catalog(("idporten", IdPorten()));

            Assert.False(AuthenticationHelper.NeedAcrUpgrade(null, [], catalog));
            Assert.False(AuthenticationHelper.NeedAcrUpgrade("idporten-loa-substantial", [], catalog));
        }
    }
}
