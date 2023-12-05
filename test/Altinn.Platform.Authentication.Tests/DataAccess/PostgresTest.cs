using System;
using System.Threading.Tasks;
using Npgsql;
using Xunit;
using Xunit.Abstractions;

namespace Altinn.Platform.Authentication.Tests.DataAccess;

public static class PostgresTest
{
    [CollectionDefinition(nameof(PostgresTestcontainerCollection))]
    public class First : IDisposable
    {
        private readonly DatabaseFixture _fixture;
        private readonly ITestOutputHelper _output;
        
        public First(DatabaseFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
        }

        [Fact]
        public async Task Database_Can_Run_Query()
        {
            await using NpgsqlConnection connection = new(_fixture.ConnectionString);
            await connection.OpenAsync();

            // const int expected = 1;

            // var actual = await connection.
        }

        public void Dispose() => _output.WriteLine(_fixture.ContainerId);
    }
}
