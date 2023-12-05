using System.Threading.Tasks;
using Testcontainers.PostgreSql;
using Xunit;

namespace Altinn.Platform.Authentication.Tests.DataAccess;

/// <summary>
/// Xunit setup class for Testcontainer
/// </summary>
public class DatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer container =
        new PostgreSqlBuilder().Build();

    public string ConnectionString => container.GetConnectionString();

    public string ContainerId => $"{container.Id}";

    public Task InitializeAsync() => container.StartAsync();

    public Task DisposeAsync() => container.DisposeAsync().AsTask();
}
