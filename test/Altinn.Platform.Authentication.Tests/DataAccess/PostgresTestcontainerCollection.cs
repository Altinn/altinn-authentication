using Xunit;

namespace Altinn.Platform.Authentication.Tests.DataAccess;

/// <summary>
/// Collection of Testcontainer tests
/// </summary>
[CollectionDefinition(nameof(PostgresTestcontainerCollection))]
public class PostgresTestcontainerCollection : ICollectionFixture<DatabaseFixture> 
{
}
