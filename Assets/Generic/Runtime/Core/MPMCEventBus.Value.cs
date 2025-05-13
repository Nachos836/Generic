#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace Generic.Core
{
    [Obsolete("WIP: Use on your own risk!")]
    public sealed class MPMCEventBus<T> : IDisposable
    {
        private readonly ConcurrentQueue<T> _eventQueue = new ();
        private readonly List<Action<T>?> _subscribers = new ();

        [MustUseReturnValue]
        public IDisposable Subscribe(Action<T> whenHappened)
        {
            _subscribers.Add(whenHappened);
            var index = _subscribers.Count - 1;

            return Disposable.CreateWithState
            (
                state: new Subscription(index, _subscribers),
                disposeAction: subscription => subscription.List[subscription.Index] = null
            );
        }

        public void PublishEvent(T income)
        {
            _eventQueue.Enqueue(income);

            if (_eventQueue.TryDequeue(out var candidate))
            {
                foreach (var subscriber in _subscribers)
                {
                    subscriber?.Invoke(candidate);
                }
            }
            else
            {
                T payload;

                while (_eventQueue.TryDequeue(out payload) is not true) { }

                foreach (var subscriber in _subscribers)
                {
                    subscriber?.Invoke(payload);
                }
            }

            _subscribers.RemoveAll(static subscriber => subscriber is null);
        }

        public void Dispose()
        {
            _subscribers.Clear();
            while (_eventQueue.Count != 0)
            {
                _eventQueue.TryDequeue(out _);
            }
        }

        private readonly struct Subscription
        {
            public readonly int Index;
            public readonly List<Action<T>?> List;

            public Subscription(int index, List<Action<T>?> list)
            {
                Index = index;
                List = list;
            }
        }
    }
}
