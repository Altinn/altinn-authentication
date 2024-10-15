using System;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Core.Models.SystemUsers;
using Altinn.Platform.Authentication.Core.RepositoryInterfaces;
using Altinn.Platform.Authentication.Persistance.Extensions;
using Altinn.Platform.Persistence.Tests;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Altinn.Platform.Authentication.Tests.RepositoryDataAccess;

public class RequestRepositoryTest(DbFixture dbFixture)
    : DbTestBase(dbFixture)
{
    protected IRequestRepository Repository => Services.GetRequiredService<IRequestRepository>();

    protected NpgsqlDataSource DataSource => Services.GetRequiredService<NpgsqlDataSource>();

    protected override void ConfigureServices(IServiceCollection services)
    {
        services.AddPersistanceLayer();
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
}
