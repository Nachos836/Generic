using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;

namespace Generic.Core.FinalStateMachine
{
    public abstract partial class StateMachine
    {
        public sealed class Immutable : StateMachine
        {
            private Immutable(IReadOnlyDictionary<Key, Transition> transitions)
            {
                Transitions = transitions.ToFrozenDictionary();
            }

            private protected override IReadOnlyDictionary<Key, Transition> Transitions { get; }

            [Pure] // Prevent value negligence
            public static PlainBuilder CreateBuilder() => new ();

            public readonly struct PlainBuilder
            {
                public AdvancedBuilder<TBootstrap> WithInitialTransition<TBootstrap>(IState to) => new (to);

                public readonly struct AdvancedBuilder<TBootstrap>
                {
                    private readonly HashSet<IState> _states;
                    private readonly HashSet<IState> _referenceFilter;
                    private readonly Dictionary<Key, Transition> _transitions;

                    public AdvancedBuilder(IState first)
                    {
                        _states = new HashSet<IState> (StatesComparer.Instance) { first };
                        _referenceFilter = new HashSet<IState> (StatesTypeComparer.Instance) { first };

                        _transitions = new () {
                            {
                                Key.Create<TBootstrap>(InitialState.Instance),
                                new Transition(from: InitialState.Instance, to: first)
                            }
                        };
                    }

                    [Pure] // Prevent value negligence
                    public AdvancedBuilder<TBootstrap> WithTransition<TTrigger>(IState from, IState to)
                    {
                        if (_states.Contains(from) is false)
                        {
                            if (_referenceFilter.Add(from))
                            {
                                _states.Add(from);
                            }
                            else
                            {
                                throw new ArgumentException($"Already has state with type { from.GetType().Name }");
                            }
                        }

                        if (_states.Contains(to) is false)
                        {
                            if (_referenceFilter.Add(to))
                            {
                                _states.Add(to);
                            }
                            else
                            {
                                throw new ArgumentException($"Already has state with type { to.GetType().Name }");
                            }
                        }

                        var lookup = Key.Create<TTrigger>(from);
                        _transitions.Add(lookup, new Transition(from, to));

                        return this;
                    }

                    [Pure] // Prevent value negligence
                    public AdvancedBuilder<TBootstrap> WithTransition<TTrigger, TFrom>(IState to)
                        where TFrom : class, IState
                    {
                        if (_states.Contains(to) is false)
                        {
                            if (_referenceFilter.Add(to))
                            {
                                _states.Add(to);
                            }
                            else
                            {
                                throw new ArgumentException($"Already has state with type { to.GetType().Name }");
                            }
                        }

                        var from = _states.Single(static state => state.GetType() == typeof(TFrom));

                        var lookup = Key.Create<TTrigger>(from);
                        _transitions.Add(lookup, new Transition(from, to));

                        return this;
                    }

                    [Pure] // Prevent value negligence
                    public AdvancedBuilder<TBootstrap> WithTransition<TTrigger, TFrom, TTo>()
                        where TFrom : class, IState
                        where TTo : class, IState
                    {
                        var from = _states.Single(static state => state.GetType() == typeof(TFrom));
                        var to = _states.Single(static state => state.GetType() == typeof(TTo));

                        var lookup = Key.Create<TTrigger>(from);
                        _transitions.Add(lookup, new Transition(from, to));

                        return this;
                    }

                    [Pure] // Prevent value negligence
                    public Immutable Build()
                    {
                        _states.Clear();
                        _referenceFilter.Clear();

                        return new Immutable(_transitions);
                    }
                }

                [Pure] // Prevent value negligence
                public Immutable Build() => new (transitions: new Dictionary<Key, Transition>());
            }
        }
    }
}
