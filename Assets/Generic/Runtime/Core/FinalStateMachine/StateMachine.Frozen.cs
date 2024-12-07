using System.Collections.Frozen;
using System.Collections.Generic;

namespace Generic.Core.FinalStateMachine
{
    public abstract partial class StateMachine
    {
        public sealed class Frozen : StateMachine
        {
            internal Frozen(IReadOnlyDictionary<Key, Transition> transitions)
            {
                Transitions = transitions.ToFrozenDictionary();
            }

            private protected override IReadOnlyDictionary<Key, Transition> Transitions { get; }

            public Mutable ToMutable() => new (Transitions);
        }
    }
}
