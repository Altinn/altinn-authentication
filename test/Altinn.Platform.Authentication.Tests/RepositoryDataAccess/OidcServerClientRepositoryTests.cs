using System;
using System.Linq;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Core.Models.Oidc;
using Altinn.Platform.Authentication.Core.RepositoryInterfaces;
using Altinn.Platform.Authentication.Persistance.Extensions;
using Altinn.Platform.Authentication.Services.Interfaces;
using Altinn.Platform.Persistence.Tests;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using NpgsqlTypes;
using Xunit;

namespace Altinn.Platform.Authentication.Tests.RepositoryDataAccess
{
    public class OidcServerClientRepositoryTests : DbTestBase
    {
        public OidcServerClientRepositoryTests(DbFixture dbFixture) : base(dbFixture)
        {
        }

        protected IOidcServerClientRepository Repository => Services.GetRequiredService<IOidcServerClientRepository>();

        protected NpgsqlDataSource DataSource => Services.GetRequiredService<NpgsqlDataSource>();

        protected override void ConfigureServices(IServiceCollection services)
        {
            services.AddPersistanceLayer();              // your extension
            services.TryAddSingleton(TimeProvider.System);
            base.ConfigureServices(services);
            services.AddTransient<IOidcProvider, Mocks.OidcProviderAdvancedMock>();
        }

        // ---------- Helpers ----------

        private async Task DeleteClientAsync(string clientId)
        {
            const string SQL = @"DELETE FROM oidcserver.client WHERE client_id = @client_id;";
            await using var cmd = DataSource.CreateCommand(SQL);
            cmd.Parameters.AddWithValue("client_id", clientId);
            await cmd.ExecuteNonQueryAsync();
        }

        private static OidcClientCreate NewClientCreate(string? id = null) =>
            new()
            {
                ClientId = id ?? $"client-{Guid.NewGuid():N}",
                ClientName = "Test Client",
                ClientType = ClientType.Confidential,
                TokenEndpointAuthMethod = TokenEndpointAuthMethod.ClientSecretBasic,
                RedirectUris = new[] { new Uri("https://app.example.com/auth/cb") },
                AllowedScopes = new[] { "openid", "profile", "email" },
                ClientSecretHash = "argon2id$v=19$m=65536,t=3,p=1$dummy$salthash", // test-only
                ClientSecretExpiresAt = null,
                SecretRotationAt = null,
                JwksUri = null,
                JwksJson = null
            };

        // ---------- Tests ----------

        [Fact]
        public async Task InsertClient_Then_GetClient_Roundtrip_Succeeds()
        {
            // Arrange
            var create = NewClientCreate();
            await DeleteClientAsync(create.ClientId); // ensure clean slate

            // Act
            var inserted = await Repository.InsertClientAsync(create);
            var fetched = await Repository.GetClientAsync(create.ClientId);

            // Assert: inserted basics
            Assert.NotNull(inserted);
            Assert.Equal(create.ClientId, inserted.ClientId);
            Assert.Equal(create.ClientName, inserted.ClientName);
            Assert.Equal(create.TokenEndpointAuthMethod, inserted.TokenEndpointAuthMethod);
            Assert.Equal(create.ClientType, inserted.ClientType);

            // Assert: redirect URIs persisted and are absolute
            Assert.Single(inserted.RedirectUris);
            Assert.True(inserted.RedirectUris.First().IsAbsoluteUri);

            // Assert: scopes normalized (lowercased/distinct handled in repo)
            Assert.Subset(
                inserted.AllowedScopes.ToHashSet(),
                create.AllowedScopes.Select(s => s.ToLowerInvariant()).ToHashSet());

            // Assert: fetched equals the inserted (key fields)
            Assert.NotNull(fetched);
            Assert.Equal(inserted.ClientId, fetched!.ClientId);
            Assert.Equal(inserted.ClientName, fetched.ClientName);
            Assert.Equal(inserted.TokenEndpointAuthMethod, fetched.TokenEndpointAuthMethod);
            Assert.Equal(
                inserted.RedirectUris.Select(u => u.ToString()),
                fetched.RedirectUris.Select(u => u.ToString()));
        }

        [Fact]
        public async Task GetClient_ReturnsNull_When_NotFound()
        {
            // Arrange
            var missingId = $"missing-{Guid.NewGuid():N}";
            await DeleteClientAsync(missingId);

            // Act
            var client = await Repository.GetClientAsync(missingId);

            // Assert
            Assert.Null(client);
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("\t")]
        public async Task GetClient_Throws_ArgumentException_On_EmptyClientId(string badId)
        {
            await Assert.ThrowsAsync<ArgumentException>(() => Repository.GetClientAsync(badId));
        }

        //[Fact]
        //public async Task InsertClient_Throws_On_NonAbsoluteRedirectUri()
        //{
        //    // Arrange
        //    var create = NewClientCreate();
        //    create = create with
        //    {
        //        RedirectUris = new[] { new Uri("/relative-cb", UriKind.Relative) } // invalid
        //    };

        //    // Act + Assert
        //    await Assert.ThrowsAsync<ArgumentException>(() => Repository.InsertClientAsync(create));
        //}

        // Enable this if client.client_id is UNIQUE/PK in your schema
        [Fact]
        public async Task InsertClient_DuplicateClientId_Throws_UniqueViolation()
        {
            // Arrange
            var id = $"dup-{Guid.NewGuid():N}";
            var first = NewClientCreate(id);
            var second = NewClientCreate(id);
            await DeleteClientAsync(id);

            await Repository.InsertClientAsync(first);

            // Act + Assert
            var ex = await Assert.ThrowsAsync<PostgresException>(() => Repository.InsertClientAsync(second));
            Assert.Equal(PostgresErrorCodes.UniqueViolation, ex.SqlState);
        }
    }
}
