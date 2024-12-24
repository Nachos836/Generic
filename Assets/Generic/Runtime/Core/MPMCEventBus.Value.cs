#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Contracts;

namespace Generic.Core
{
    public sealed class MPMCEventBus<T> : IDisposable
    {
        private readonly ConcurrentQueue<T> _eventQueue = new ();
        private readonly List<Action<T>?> _subscribers = new ();

        [Pure] // Prevent value negligence
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

            while (_eventQueue.TryDequeue(out var payload))
            {
                foreach (var subscriber in _subscribers)
                {
                    subscriber?.Invoke(payload);
                }
            }
        }

        public void Dispose()
        {
            _subscribers.Clear();
            _eventQueue.Clear();
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
