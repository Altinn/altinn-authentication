using System.Threading.Tasks;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;
using Xunit.Abstractions;

namespace Altinn.Platform.Authentication.Tests.DataAccess;

/// <summary>
/// Test db
/// </summary>
public class PostgresTestStandalone : IAsyncLifetime
{
    // https://blog.jetbrains.com/dotnet/2023/10/24/how-to-use-testcontainers-with-dotnet-unit-tests
    private readonly ITestOutputHelper _testOutputHelper;

    public PostgresTestStandalone(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    private readonly PostgreSqlContainer container = new PostgreSqlBuilder().Build();

    public string ConnectionString => container.GetConnectionString();

    public string ContainerId => $"{container.Id}";

    public async Task InitializeAsync()
    {
        await using NpgsqlConnection connection = new() { ConnectionString = ConnectionString };

        const string Query = /*strpsql*/@"
            CREATE TABLE IF NOT EXISTS altinn_authentication.system_user_integration
            (
	            system_user_integration_id uuid DEFAULT gen_random_uuid() PRIMARY KEY,
	            integration_title varchar(255) NOT NULL,
	            integration_description varchar(255),
	            product_name varchar(255) NOT NULL,
	            owned_by_party_id varchar(255) NOT NULL,
	            supplier_name varchar(255),
	            supplier_org_no varchar(255),
	            client_id varchar(255),
	            is_deleted bool default False,
	            created timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
	            UNIQUE (owned_by_party_id, product_name)
            )   
            ";

        var command = connection.CreateCommand();
        command.CommandText = Query;
        NpgsqlDataReader reader = await command.ExecuteReaderAsync();

        await container.StartAsync();
    }

    public Task DisposeAsync() => container.DisposeAsync().AsTask();

    // [Fact]
    public async Task Database_Can_Run_Simple_Query()
    {
        await using NpgsqlConnection connection = new() { ConnectionString = ConnectionString };
    
        await connection.OpenAsync();

        const string Query = /*strpsql*/@"
        CREATE TABLE TEST
        test_text varchar(255);
        ";

        const int Expected = 1;
        
        var command = connection.CreateCommand();
        command.CommandText = Query;
        NpgsqlDataReader reader = await command.ExecuteReaderAsync();

        Assert.True(reader.GetInt32(0) == Expected);
    }   
}