namespace Altinn.Platform.Authentication.Tests.RepositoryDataAccess;

internal sealed class AsyncLock()
: AsyncConcurrencyLimiter(1)
{
}
