#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;

namespace Generic.Core.FinalStateMachine
{
    public abstract partial class StateMachine
    {
        internal readonly struct Key : IEquatable<Key>
        {
            internal static Key Create<T>(IState from) => new (UniqueId<T>.Value, from);

            public readonly int TriggerHash;
            private readonly int _raw;

            private Key(uint rawTrigger, IState from)
            {
                TriggerHash = (int) rawTrigger;
                _raw = HashCode.Combine(rawTrigger, from);
            }

            public bool Equals(Key other) => _raw == other._raw;
            public override int GetHashCode() => _raw;
        }

        [SuppressMessage("ReSharper", "StaticMemberInGenericType")]
        [SuppressMessage("ReSharper", "UnusedTypeParameter")]
        private protected static class UniqueId<T>
        {
            public static uint Value { get; } = UniqueNumberHolder.Value++;
        }

        private static class UniqueNumberHolder
        {
            public static uint Value;
        }
    }
}
