using System;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Core.Models.SystemUsers;
using Altinn.Platform.Authentication.Core.RepositoryInterfaces;
using Altinn.Platform.Authentication.Persistance.Extensions;
using Altinn.Platform.Persistence.Tests;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Xunit;

namespace Altinn.Platform.Authentication.Tests.RepositoryDataAccess;

public class ChangeRequestRepositoryTest(DbFixture dbFixture)
    : DbTestBase(dbFixture)
{
    protected IChangeRequestRepository Repository => Services.GetRequiredService<IChangeRequestRepository>();

    protected NpgsqlDataSource DataSource => Services.GetRequiredService<NpgsqlDataSource>();

    protected override void ConfigureHost(IHostApplicationBuilder builder)
    {
        builder.AddPersistanceLayer();
        base.ConfigureHost(builder);
    }

    protected override void ConfigureServices(IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        base.ConfigureServices(services);
    }

    [Theory]
    [InlineData(256)]
    [InlineData(2048)]
    [InlineData(8192)]
    public async Task CreateChangeRequest_WithRedirectUrlLongerThan255Characters_IsPersisted(int urlLength)
    {
        // Verifies migration v0.27/01-alter-redirect-urls-length.sql which widens
        // business_application.change_request.redirect_urls from varchar(255) to text.
        // Multiple lengths exercise that the column is effectively unbounded.
        string longRedirectUrl = BuildLongRedirectUrl(urlLength);

        ChangeRequestResponse req = new()
        {
            Id = Guid.NewGuid(),
            ExternalRef = "test-change-long-url",
            SystemId = "test",
            SystemUserId = Guid.NewGuid(),
            PartyOrgNo = "123456789",
            RequiredRights = [],
            UnwantedRights = [],
            RequiredAccessPackages = [],
            UnwantedAccessPackages = [],
            Status = "new",
            RedirectUrl = longRedirectUrl
        };

        var create = await Repository.CreateChangeRequest(req);
        Assert.True(create.IsSuccess);

        var stored = await Repository.GetChangeRequestById(req.Id);
        Assert.NotNull(stored);
        Assert.Equal(longRedirectUrl, stored!.RedirectUrl);
        Assert.True(stored.RedirectUrl!.Length > 255);

        Assert.Equal("text", await GetColumnDataTypeAsync("change_request", "redirect_urls"));
    }

    private static string BuildLongRedirectUrl(int length)
    {
        const string Prefix = "https://example.com/callback?state=";
        return Prefix + new string('a', length - Prefix.Length);
    }

    private async Task<string> GetColumnDataTypeAsync(string tableName, string columnName)
    {
        const string Query = /*strpsql*/@"
            SELECT data_type
            FROM information_schema.columns
            WHERE table_schema = 'business_application'
              AND table_name = @table_name
              AND column_name = @column_name;";

        await using NpgsqlCommand command = DataSource.CreateCommand(Query);
        command.Parameters.AddWithValue("table_name", tableName);
        command.Parameters.AddWithValue("column_name", columnName);

        var result = await command.ExecuteScalarAsync();
        Assert.NotNull(result);
        return (string)result!;
    }
}
