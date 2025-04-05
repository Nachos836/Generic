using System;
using System.Diagnostics.Contracts;
using UnityEngine;

namespace SerializableValueObjects
{
    using static SerializableTimeSpan.Unit;

    [Serializable]
    public struct SerializableTimeSpan
    {
        [Pure] public static SerializableTimeSpan Zero { get; } = new() { _ticks = TimeSpan.Zero.Ticks, _displayUnit = Seconds };

        [SerializeField] internal Unit _displayUnit;
        [SerializeField] internal long _ticks;

        [Pure] public static SerializableTimeSpan FromTicks(long ticks) => new() { _ticks = TimeSpan.FromTicks(ticks).Ticks, _displayUnit = Ticks };
        [Pure] public static SerializableTimeSpan FromMilliseconds(double time) => new() { _ticks = TimeSpan.FromMilliseconds(time).Ticks, _displayUnit = Milliseconds };
        [Pure] public static SerializableTimeSpan FromSeconds(double time) => new() { _ticks = TimeSpan.FromSeconds(time).Ticks, _displayUnit = Seconds };
        [Pure] public static SerializableTimeSpan FromMinutes(double time) => new() { _ticks = TimeSpan.FromMinutes(time).Ticks, _displayUnit = Minutes };
        [Pure] public static SerializableTimeSpan FromHours(double time) => new() { _ticks = TimeSpan.FromHours(time).Ticks, _displayUnit = Hours };
        [Pure] public static SerializableTimeSpan FromDays(double time) => new() { _ticks = TimeSpan.FromDays(time).Ticks, _displayUnit = Days };

        [Pure] public readonly TimeSpan Value => TimeSpan.FromTicks(_ticks);

        public static implicit operator TimeSpan(SerializableTimeSpan input) => TimeSpan.FromTicks(input._ticks);
        public static implicit operator SerializableTimeSpan(TimeSpan input) => FromTicks(input.Ticks);

        internal enum Unit
        {
            Ticks,
            Milliseconds,
            Seconds,
            Minutes,
            Hours,
            Days
        }
    }
}
