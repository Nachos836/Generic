using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;

namespace Generic.Core.FinalStateMachine
{
    public abstract partial class StateMachine
    {
        public sealed class Immutable : StateMachine
        {
            private Immutable(IEnumerable<KeyValuePair<Key, IState>> transitions)
            {
                Transitions = transitions.ToFrozenDictionary();
            }

            private protected override IReadOnlyDictionary<Key, IState> Transitions { get; }

            [MustUseReturnValue]
            public static PlainBuilder CreateBuilder() => new ();

            public readonly struct PlainBuilder
            {
                public AdvancedBuilder<TBootstrap> WithInitialTransition<TBootstrap>(IState to) => new (to);

                public readonly struct AdvancedBuilder<TBootstrap>
                {
                    private readonly HashSet<IState> _states;
                    private readonly HashSet<IState> _stateTypesFilter;
                    private readonly List<KeyValuePair<Key, IState>> _transitions;

                    internal AdvancedBuilder(IState first)
                    {
                        _states = new HashSet<IState> (StatesComparer.Instance) { first };
                        _stateTypesFilter = new HashSet<IState> (StatesTypeComparer.Instance) { first };

                        _transitions = new ()
                        {
                            new KeyValuePair<Key, IState>(Key.Create<TBootstrap>(InitialState.Instance), first)
                        };
                    }

                    [MustUseReturnValue]
                    public AdvancedBuilder<TBootstrap> WithTransition<TTrigger>(IState from, IState to)
                    {
                        if (TryAddState(from) is false) throw new ArgumentException($"Already has state with type { from.GetType().Name }");
                        if (TryAddState(to) is false) throw new ArgumentException($"Already has state with type { to.GetType().Name }");

                        var lookup = Key.Create<TTrigger>(from);
                        _transitions.Add(new (lookup, to));

                        return this;
                    }

                    [MustUseReturnValue]
                    public AdvancedBuilder<TBootstrap> WithTransition<TTrigger, TFrom>(IState to)
                        where TFrom : class, IState
                    {
                        var from = _states.Single(static state => state.GetType() == typeof(TFrom));
                        if (TryAddState(to) is false) throw new ArgumentException($"Already has state with type { to.GetType().Name }");

                        var lookup = Key.Create<TTrigger>(from);
                        _transitions.Add(new (lookup, to));

                        return this;
                    }

                    [MustUseReturnValue]
                    public AdvancedBuilder<TBootstrap> WithTransition<TTrigger, TFrom, TTo>()
                        where TFrom : class, IState
                        where TTo : class, IState
                    {
                        var from = _states.Single(static state => state.GetType() == typeof(TFrom));
                        var to = _states.Single(static state => state.GetType() == typeof(TTo));

                        var lookup = Key.Create<TTrigger>(from);
                        _transitions.Add(new (lookup, to));

                        return this;
                    }

                    [MustUseReturnValue]
                    public Immutable Build()
                    {
                        _states.Clear();
                        _stateTypesFilter.Clear();

                        return new Immutable(_transitions);
                    }

                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    private bool TryAddState(IState state)
                    {
                        if (_states.Contains(state)) return true;
                        if (_stateTypesFilter.Add(state) is false) return false;

                        _states.Add(state);

                        return true;
                    }
                }

                [MustUseReturnValue]
                public Immutable Build() => new (transitions: Enumerable.Empty<KeyValuePair<Key, IState>>());
            }
        }
    }
}
