using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Altinn.Platform.Authentication.Tests.RepositoryDataAccess;

internal static class Singleton
{
    public static Task<Ref<T>> Get<T>()
        where T : class, IAsyncLifetime, new()
        => Ref<T>.Get();

    public sealed class Ref<T>
        : IAsyncDisposable
        where T : class, IAsyncLifetime, new()
    {
        public static async Task<Ref<T>> Get()
            => new(await Impl<T>.Instance.GetAsync());

        private int _disposed = 0;
        private T _value;

        public T Value => _value;

        private Ref(T value)
        {
            _value = value;
        }

        ~Ref()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                //ThrowHelper.ThrowInvalidOperationException($"Singleton.Ref<{typeof(T).Name}> was not disposed");
                throw new InvalidOperationException($"Singleton.Ref<{typeof(T).Name}> was not disposed");
            }
        }

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _value = null!;
                GC.SuppressFinalize(this);
                return Impl<T>.Instance.DisposeAsync();
            }

            return ValueTask.CompletedTask;
        }
    }

    private sealed class Impl<T>
        : IAsyncDisposable
        where T : class, IAsyncLifetime, new()
    {
        public static readonly Impl<T> Instance = new();

        private readonly AsyncLock _lock = new();

        private T? _value;
        private int _referenceCount;

        public async ValueTask DisposeAsync()
        {
            using var guard = await _lock.Acquire();
            if (--_referenceCount == 0)
            {
                // we just went from 1 reference to 0, so we need to dispose the value
                await _value!.DisposeAsync();
            }
        }

        public async Task<T> GetAsync()
        {
            using var guard = await _lock.Acquire();
            if (_referenceCount++ == 0)
            {
                // we just went from 0 references to 1, so we need to create the value
                _value = new T();
                await _value.InitializeAsync();
            }

            return _value!;
        }
    }
}
