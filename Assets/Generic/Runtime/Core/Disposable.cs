﻿using System;
using JetBrains.Annotations;

namespace Generic.Core
{
    public static class Disposable
    {
        public static readonly IDisposable Empty = EmptyDisposable.Instance;

        [MustDisposeResource]
        public static IDisposable Create(Action disposeAction)
        {
            return new AnonymousDisposable(disposeAction);
        }

        [MustDisposeResource]
        public static IDisposable CreateWithState<TState>(TState state, Action<TState> disposeAction)
        {
            return new AnonymousDisposable<TState>(state, disposeAction);
        }

        [MustDisposeResource]
        private sealed class EmptyDisposable : IDisposable
        {
            public static readonly IDisposable Instance = new EmptyDisposable();

            private EmptyDisposable() { }

            void IDisposable.Dispose() { }
        }

        [MustDisposeResource]
        private sealed class AnonymousDisposable : IDisposable
        {
            private readonly Action _onDispose;

            private bool _isDisposed;

            public AnonymousDisposable(Action onDispose)
            {
                _onDispose = onDispose;
            }

            void IDisposable.Dispose()
            {
                if (_isDisposed) return;

                _isDisposed = true;
                _onDispose.Invoke();
            }
        }

        [MustDisposeResource]
        private sealed class AnonymousDisposable<T> : IDisposable
        {
            private readonly T _state;
            private readonly Action<T> _onDispose;

            private bool _isDisposed;

            public AnonymousDisposable(T state, Action<T> onDispose)
            {
                _state = state;
                _onDispose = onDispose;
            }

            void IDisposable.Dispose()
            {
                if (_isDisposed) return;

                _isDisposed = true;
                _onDispose.Invoke(_state);
            }
        }

        [MustDisposeResource]
        public sealed class Bag : IDisposable
        {
            private readonly IDisposable[] _disposables;

            private bool _isDisposed;

            private Bag(IDisposable[] disposables) => _disposables = disposables;

            [MustDisposeResource]
            public static IDisposable Create(params IDisposable[] disposables) => new Bag(disposables);

            void IDisposable.Dispose()
            {
                if (_isDisposed) return;

                _isDisposed = true;

                foreach (ref var disposable in _disposables.AsSpan())
                {
                    disposable.Dispose();
                }
            }
        }
    }
}
