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

public class RequestRepositoryTest(DbFixture dbFixture)
    : DbTestBase(dbFixture)
{
    protected IRequestRepository Repository => Services.GetRequiredService<IRequestRepository>();

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

    [Fact]
    public async Task Archiver_DryRun_OK()
    {
        // Should be no request to archive        
        var test = await Repository.CopyOldRequestsToArchive(1);
        Assert.Equal(0, test);
    }

    [Fact]
    public async Task Archiver_Delete_and_Copy_OK()
    {
        RequestSystemResponse req = new()
        {
            Id = Guid.NewGuid(),
            ExternalRef = "test",
            SystemId = "test",
            PartyOrgNo = "123456789",
            Rights = [],
            Status = "new",
            RedirectUrl = "http://test.com"
        };

        // Arrange
        var create = await Repository.CreateRequest(req);
        Assert.True(create.IsSuccess);

        var find = await Repository.GetRequestByInternalId(req.Id);
        Assert.NotNull(find);

        var del = await Repository.SetDeleteTimedoutRequests(0);
        Assert.Equal(1, del);

        // Should be 1 request to archive        
        var test = await Repository.CopyOldRequestsToArchive(0);
        Assert.Equal(1, test);

        var notfind = await Repository.GetRequestByInternalId(req.Id);
        Assert.Null(notfind);

        var archived = await Repository.GetArchivedRequestByInternalId(req.Id);
        Assert.NotNull(archived);
        Assert.Equal(req.Id, archived.Id);
    }

    [Theory]
    [InlineData(256)]
    [InlineData(2048)]
    [InlineData(8192)]
    public async Task CreateRequest_WithRedirectUrlLongerThan255Characters_IsPersisted(int urlLength)
    {
        // Verifies migration v0.27/01-alter-redirect-urls-length.sql which widens
        // business_application.request.redirect_urls from varchar(255) to text.
        // Multiple lengths exercise that the column is effectively unbounded.
        string longRedirectUrl = BuildLongRedirectUrl(urlLength);

        RequestSystemResponse req = new()
        {
            Id = Guid.NewGuid(),
            ExternalRef = "test-long-url",
            SystemId = "test",
            PartyOrgNo = "123456789",
            Rights = [],
            Status = "new",
            RedirectUrl = longRedirectUrl
        };

        var create = await Repository.CreateRequest(req);
        Assert.True(create.IsSuccess);

        var stored = await Repository.GetRequestByInternalId(req.Id);
        Assert.NotNull(stored);
        Assert.Equal(longRedirectUrl, stored!.RedirectUrl);
        Assert.True(stored.RedirectUrl!.Length > 255);

        Assert.Equal("text", await GetColumnDataTypeAsync("request", "redirect_urls"));
    }

    [Theory]
    [InlineData(256)]
    [InlineData(2048)]
    [InlineData(8192)]
    public async Task CopyOldRequestsToArchive_PreservesRedirectUrlLongerThan255Characters(int urlLength)
    {
        // Verifies migration v0.27/01-alter-redirect-urls-length.sql for the
        // business_application.request_archive.redirect_urls column.
        string longRedirectUrl = BuildLongRedirectUrl(urlLength);

        RequestSystemResponse req = new()
        {
            Id = Guid.NewGuid(),
            ExternalRef = "test-archive-long-url",
            SystemId = "test",
            PartyOrgNo = "123456789",
            Rights = [],
            Status = "new",
            RedirectUrl = longRedirectUrl
        };

        var create = await Repository.CreateRequest(req);
        Assert.True(create.IsSuccess);

        var del = await Repository.SetDeleteTimedoutRequests(0);
        Assert.Equal(1, del);

        var copied = await Repository.CopyOldRequestsToArchive(0);
        Assert.Equal(1, copied);

        var archived = await Repository.GetArchivedRequestByInternalId(req.Id);
        Assert.NotNull(archived);
        Assert.Equal(longRedirectUrl, archived!.RedirectUrl);
        Assert.True(archived.RedirectUrl!.Length > 255);

        Assert.Equal("text", await GetColumnDataTypeAsync("request_archive", "redirect_urls"));
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
