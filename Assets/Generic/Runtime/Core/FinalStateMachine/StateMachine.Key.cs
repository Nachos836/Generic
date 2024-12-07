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

            private readonly int _raw;

            private Key(uint raw, IState from)
            {
                _raw = HashCode.Combine(raw, from);
            }

            public bool Equals(Key other) => _raw == other._raw;
            public override bool Equals(object? income) => income is Key other && Equals(other);
            public override int GetHashCode() => _raw;
        }

        private static class UniqueNumberHolder
        {
            public static uint Value;
        }

        [SuppressMessage("ReSharper", "StaticMemberInGenericType")]
        [SuppressMessage("ReSharper", "UnusedTypeParameter")]
        private protected static class UniqueId<T>
        {
            public static uint Value { get; } = UniqueNumberHolder.Value++;
        }
    }
}
