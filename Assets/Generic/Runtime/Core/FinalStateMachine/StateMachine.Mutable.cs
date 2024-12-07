#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;

namespace Generic.Core.FinalStateMachine
{
    public abstract partial class StateMachine
    {
        public sealed class Mutable : StateMachine
        {
            private readonly ConcurrentDictionary<Key, Transition> _transitions = new ();

            private Mutable()
            {
                Transitions = _transitions;
            }

            internal Mutable(IState current, IReadOnlyDictionary<Key, Transition> transitions)
            {
                _current = current;
                _transitions = new (transitions.AsEnumerable());

                Transitions = _transitions;
            }

            private protected override IReadOnlyDictionary<Key, Transition> Transitions { get; }

            [Pure] // Prevent value negligence
            public static Mutable Create<TBootstrap>(IState startingWith)
            {
                return new Mutable()
                    .AddTransition<TBootstrap>(InitialState.Instance, startingWith);
            }

            public Mutable AddTransition<TTrigger>(IState from, IState to)
            {
                var key = Key.Create<TTrigger>(from);
                if (_transitions.TryAdd(key, new Transition(from, to)) is false) throw new ArgumentException($"Transition {typeof(TTrigger).Name} is already added!");

                return this;
            }
        }
    }
}
