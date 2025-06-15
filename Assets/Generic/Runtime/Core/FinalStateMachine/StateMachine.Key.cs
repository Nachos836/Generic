#nullable enable

using System;
using System.Collections.Generic;

namespace Generic.Core.FinalStateMachine
{
    public abstract partial class StateMachine
    {
        internal readonly struct Key : IEquatable<Key>
        {
            public static readonly IEqualityComparer<Key> TriggersComparer = new TriggersComparator();
            internal static Key Create<T>(IState from) => new (UniqueId<T>.Value, from);

            private readonly int _triggerHash;
            private readonly int _raw;

            private Key(int rawTrigger, IState from)
            {
                _triggerHash = rawTrigger;
                _raw = HashCode.Combine(rawTrigger, from);
            }

            public bool Equals(Key other) => _raw == other._raw;
            public override int GetHashCode() => _raw;

            internal sealed class TriggersComparator : IEqualityComparer<Key>
            {
                bool IEqualityComparer<Key>.Equals(Key first, Key second) => first._triggerHash == second._triggerHash;
                int IEqualityComparer<Key>.GetHashCode(Key income) => income._triggerHash;
            }
        }

        private protected static class UniqueId<T>
        {
            public static int Value { get; } = typeof(T).GetHashCode();
        }
    }
}
