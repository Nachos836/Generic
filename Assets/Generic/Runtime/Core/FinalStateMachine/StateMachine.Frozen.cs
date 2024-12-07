using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics.Contracts;

namespace Generic.Core.FinalStateMachine
{
    public abstract partial class StateMachine
    {
        public sealed class Frozen : StateMachine
        {
            internal Frozen(IState current, IReadOnlyDictionary<Key, Transition> transitions)
            {
                _current = current;
                Transitions = transitions.ToFrozenDictionary();
            }

            private protected override IReadOnlyDictionary<Key, Transition> Transitions { get; }

            [Pure] // Prevent value negligence
            public Mutable ToMutable() => new (_current, Transitions);
        }
    }
}
