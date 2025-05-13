#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Functional.Core.Outcome;
using JetBrains.Annotations;

namespace Generic.Core
{
    [Obsolete("WIP: Use on your own risk!")]
    public sealed class MPMCEventBus : IDisposable
    {
        private readonly ConcurrentQueue<None> _eventQueue = new ();
        private readonly List<Action?> _subscribers = new ();

        [MustDisposeResource]
        public IDisposable Subscribe(Action whenHappened)
        {
            _subscribers.Add(whenHappened);
            var index = _subscribers.Count - 1;

            return Disposable.CreateWithState
            (
                state: new Subscription(index, _subscribers),
                disposeAction: subscription => subscription.List[subscription.Index] = null
            );
        }

        public void RaiseEvent()
        {
            _eventQueue.Enqueue(new None());

            while (_eventQueue.TryDequeue(out _))
            {
                foreach (var subscriber in _subscribers)
                {
                    subscriber?.Invoke();
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
            public readonly List<Action?> List;

            public Subscription(int index, List<Action?> list)
            {
                Index = index;
                List = list;
            }
        }
    }
}
