// ReSharper disable CheckNamespace

#nullable enable

using System;
using System.Diagnostics;

using static System.AttributeTargets;

namespace SerializableValueObjects.Attributes
{
    /// <summary>
    /// Restricts decimal values to a specified range in the Inspector with a slider <br/>
    /// NOTE: Limited to float values due to Unity's limitations
    /// </summary>
    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(Field | Property)]
    public sealed class DecimalRangeAttribute : Attribute
    {
        internal static DecimalRangeAttribute None { get; } = new ();

        internal bool IsNeeded { get; }

        internal float Min { get; }
        internal float Max { get; }

        public DecimalRangeAttribute(int min, int max)
        {
            IsNeeded = true;

            Min = min;
            Max = max;
        }

        public DecimalRangeAttribute(float min, float max)
        {
            IsNeeded = true;

            Min = min;
            Max = max;
        }

        private DecimalRangeAttribute()
        {
            IsNeeded = false;
            Min = default;
            Max = default;
        }
    }
}
