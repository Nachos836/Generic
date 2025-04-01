#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace Generic.Core.FinalStateMachine
{
    public abstract partial class StateMachine
    {
        public sealed class Mutable : StateMachine
        {
            private readonly ConcurrentDictionary<Key, IState> _transitions = new ();

            private Mutable()
            {
                Transitions = _transitions;
            }

            internal Mutable(IState current, IReadOnlyDictionary<Key, IState> transitions)
            {
                _current = current;
                _transitions = new (transitions.AsEnumerable());

                Transitions = _transitions;
            }

            private protected override IReadOnlyDictionary<Key, IState> Transitions { get; }

            [MustUseReturnValue]
            public static Mutable Create<TBootstrap>(IState startingWith)
            {
                return new Mutable()
                    .AddTransition<TBootstrap>(InitialState.Instance, startingWith);
            }

            public Mutable AddTransition<TTrigger>(IState from, IState to)
            {
                var key = Key.Create<TTrigger>(from);
                if (_transitions.TryAdd(key, to) is false) throw new ArgumentException($"Transition {typeof(TTrigger).Name} is already added!");

                return this;
            }
        }
    }
}
