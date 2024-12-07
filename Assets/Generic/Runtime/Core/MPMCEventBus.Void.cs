using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Functional.Core.Outcome;

namespace Generic.Core
{
    public sealed class MPMCEventBus : IDisposable
    {
        private readonly ConcurrentQueue<None> _eventQueue = new ();
        private readonly List<Action> _subscribers = new ();

        [Pure] // Prevent value negligence
        public IDisposable Subscribe(Action whenHappened)
        {
            _subscribers.Add(whenHappened);

            return Disposable.CreateWithState
            (
                state: new Subscription(whenHappened, _subscribers),
                disposeAction: subscription => subscription.List.Remove(subscription.Current)
            );
        }

        public void RaiseEvent()
        {
            _eventQueue.Enqueue(new None());

            while (_eventQueue.TryDequeue(out _))
            {
                foreach (var subscriber in _subscribers)
                {
                    subscriber.Invoke();
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
            public readonly Action Current;
            public readonly List<Action> List;

            public Subscription(Action current, List<Action> list)
            {
                Current = current;
                List = list;
            }
        }
    }
}
