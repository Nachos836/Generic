#nullable enable

using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Threading;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;

namespace Pooling
{
    public sealed class AsyncPool<T> : IDisposable
    {
        private readonly Func<CancellationToken, UniTask<T>> _toCreate;
        private readonly Func<T, CancellationToken, UniTask> _whenGet;
        private readonly Func<T, UniTaskVoid> _whenRelease;
        private readonly Action<T> _whenDestroy;

        private readonly ConcurrentStack<T> _items = new ();
        private readonly SemaphoreSlim _semaphore = new (0, int.MaxValue);

        public AsyncPool
        (
            Func<CancellationToken, UniTask<T>> toCreate,
            Func<T, CancellationToken, UniTask> whenGet,
            Func<T, UniTaskVoid> whenRelease,
            Action<T> whenDestroy
        ) {
            _toCreate = toCreate;
            _whenGet = whenGet;
            _whenRelease = whenRelease;
            _whenDestroy = whenDestroy;
        }

        public UniTask WarmupAsync<TState>
        (
            int amount,
            Func<TState, int, CancellationToken, UniTask<T[]>> bulkCreate,
            TState state,
            CancellationToken cancellation = default
        ) {
            var items = _items;

            return bulkCreate.Invoke(state, amount, cancellation)
                .ContinueWith(results => items.PushRange(results));
        }

        public async UniTask<Pooled> GetAsync
        (
            CancellationToken cancellation = default,
            bool configureAwait = false
        ) {
            if (_items.TryPop(out var item))
            {
                await _whenGet(item, cancellation);

                return new Pooled(this, item);
            }

            await _semaphore.WaitAsync(cancellation)
                .AsUniTask(!configureAwait);
            try
            {
                if (_items.TryPop(out item) == false)
                {
                    item = await _toCreate(cancellation);
                }
            }
            finally
            {
                _semaphore.Release();
            }

            await _whenGet(item, cancellation);

            return new Pooled(this, item);
        }

        public async UniTask<Pooled[]> GetAsync<TState>
        (
            int amount,
            TState state,
            Func<TState, int, CancellationToken, UniTask<T[]>>? bulkCreate = null,
            CancellationToken cancellation = default
        ) {
            if (amount <= 0) return Array.Empty<Pooled>();

            var buffer = new T[amount];
            var obtained = _items.TryPopRange(buffer);
            var remain = amount - obtained;

            if (remain > 0)
            {
                await _semaphore.WaitAsync(cancellation)
                    .AsUniTask();

                try
                {
                    var additionallyObtained = _items.TryPopRange(buffer, obtained, remain);
                    obtained += additionallyObtained;
                    remain -= additionallyObtained;

                    if (remain > 0)
                    {
                        bulkCreate ??= async (_, count, token) =>
                        {
                            var created = new T[count];
                            for (var i = 0; i < count; ++i)
                            {
                                created[i] = await _toCreate(token);
                            }
                            return created;
                        };

                        var created = await bulkCreate(state, remain, cancellation);
                        Array.Copy(created, 0, buffer, obtained, created.Length);
                    }
                }
                finally
                {
                    _semaphore.Release();
                }
            }

            var pooled = new Pooled[amount];
            for (var i = 0; i < amount; i++)
            {
                await _whenGet(buffer[i], cancellation);
                pooled[i] = new Pooled(this, buffer[i]);
            }

            return pooled;
        }

        public IUniTaskAsyncEnumerable<Pooled> GetAsync<TState>
        (
            int amount,
            TState state,
            Func<TState, int, IUniTaskAsyncEnumerable<T>>? bulkCreate = null
        ) {
            return UniTaskAsyncEnumerable.Create<Pooled>(async (writer, cancellation) =>
            {
                if (amount <= 0) return;

                var rent = ArrayPool<T>.Shared.Rent(amount);
                var obtained = _items.TryPopRange(rent);
                try
                {
                    for (var i = 0; i < obtained; ++i)
                    {
                        await _whenGet(rent[i], cancellation);
                        await writer.YieldAsync(new Pooled(this, rent[i]));
                    }

                    var left = amount - obtained;
                    if (left == 0) return;

                    bulkCreate ??= (_, count) => UniTaskAsyncEnumerable.Create<T>(async (localWriter, token) =>
                    {
                        for (var i = 0; i < count && token.IsCancellationRequested == false; ++i)
                        {
                            var candidate = await _toCreate(token);
                            await localWriter.YieldAsync(candidate);
                        }
                    });

                    var creator = bulkCreate(state, left)
                        .GetAsyncEnumerator(cancellation);

                    try
                    {
                        while (left > 0)
                        {
                            if (_items.TryPop(out var pooled))
                            {
                                await _whenGet(pooled, cancellation);
                                await writer.YieldAsync(new Pooled(this, pooled));
                                --left;
                                continue;
                            }

                            await _semaphore.WaitAsync(cancellation);

                            try
                            {
                                if (_items.TryPop(out pooled) == false)
                                {
                                    if (await creator.MoveNextAsync() == false) throw new InvalidOperationException($"bulkCreate produced fewer items than requested ({left} left).");

                                    pooled = creator.Current;
                                }

                                await _whenGet(pooled, cancellation);
                                await writer.YieldAsync(new Pooled(this, pooled));
                                --left;
                            }
                            catch
                            {
                                _semaphore.Release();
                                throw;
                            }
                        }
                    }
                    finally
                    {
                        await creator.DisposeAsync();
                    }
                }
                finally
                {
                    ArrayPool<T>.Shared.Return(rent);
                }
            });
        }

        public void Dispose()
        {
            _semaphore.Dispose();

            foreach (var item in _items)
            {
                _whenDestroy.Invoke(item);
            }
            _items.Clear();
        }

        private UniTaskVoid ReturnAsync(T item)
        {
            _items.Push(item);

            return _whenRelease.Invoke(item);
        }

        public struct Pooled : IDisposable
        {
            private readonly AsyncPool<T> _pool;
            private bool _disposed;

            public readonly T Value;

            internal Pooled(AsyncPool<T> pool, T value)
            {
                _disposed = false;
                _pool = pool;
                Value = value;
            }

            public void Dispose()
            {
                if (_disposed) return;

                _disposed = true;

                _pool.ReturnAsync(Value)
                    .Forget();
            }
        }
    }
}
