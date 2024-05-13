using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Altinn.Platform.Authentication.Tests.RepositoryDataAccess;

internal class AsyncConcurrencyLimiter
    : IDisposable
{
    private readonly SemaphoreSlim _semaphoreSlim;

    public AsyncConcurrencyLimiter(int maxConcurrency)
    {
        //Guard.IsGreaterThanOrEqualTo(maxConcurrency, 1);
        Assert.True(maxConcurrency > 0);

        _semaphoreSlim = new SemaphoreSlim(maxConcurrency, maxConcurrency);
    }

    public void Dispose()
    {
        _semaphoreSlim.Dispose();
    }

    /// <summary>
    /// Acquires a lock to access the resource thread-safe.
    /// </summary>
    /// <returns>An <see cref="IDisposable" /> that releases the lock on <see cref="IDisposable.Dispose" />.</returns>
    public async Task<IDisposable> Acquire()
    {
        await _semaphoreSlim.WaitAsync();
        return new Ticket(_semaphoreSlim);
    }

    /// <summary>
    /// A lock to synchronize threads.
    /// </summary>
    private sealed class Ticket : IDisposable
    {
        private SemaphoreSlim? _semaphoreSlim;

        /// <summary>
        /// Initializes a new instance of the <see cref="Ticket" /> class.
        /// </summary>
        /// <param name="semaphoreSlim">The semaphore slim to synchronize threads.</param>
        public Ticket(SemaphoreSlim semaphoreSlim)
        {
            _semaphoreSlim = semaphoreSlim;
        }

        ~Ticket()
        {
            if (_semaphoreSlim != null)
            {
                //ThrowHelper.ThrowInvalidOperationException("Lock not released.");
                throw new InvalidOperationException("Lock not released.");
            }
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            Interlocked.Exchange(ref _semaphoreSlim, null)?.Release();
        }
    }
}
