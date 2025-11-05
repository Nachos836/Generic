#nullable enable

using System;
using System.Diagnostics.Contracts;
using UnityEngine;

// ReSharper disable CheckNamespace
namespace SerializableValueObjects
{
    [Serializable]
    public struct SerializableDecimal
    {
        internal const string LoPartName = nameof(_lo);
        internal const string MidPartName = nameof(_mid);
        internal const string HiPartName = nameof(_hi);
        internal const string FlagsPartName = nameof(_flags);

        [SerializeField] private int _lo;
        [SerializeField] private int _mid;
        [SerializeField] private int _hi;
        [SerializeField] private int _flags;

        [Pure] public readonly decimal Value => new (_lo, _mid, _hi, (_flags & 0x80000000) != 0, (byte)((_flags & 0x00FF0000) >> 16));

        public static implicit operator decimal(SerializableDecimal input) => input.Value;

        public static implicit operator SerializableDecimal(decimal input)
        {
            var bits = decimal.GetBits(input);
            return new SerializableDecimal
            {
                _lo = bits[0],
                _mid = bits[1],
                _hi = bits[2],
                _flags = bits[3]
            };
        }
    }
}
