using System.Collections.Frozen;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace Generic.Core.FinalStateMachine
{
    public abstract partial class StateMachine
    {
        public sealed class Frozen : StateMachine
        {
            internal Frozen(IState current, IReadOnlyDictionary<Key, IState> transitions)
            {
                _current = current;
                Transitions = transitions.ToFrozenDictionary();
            }

            private protected override IReadOnlyDictionary<Key, IState> Transitions { get; }

            [MustUseReturnValue]
            public Mutable ToMutable() => new (_current, Transitions);
        }
    }
}
