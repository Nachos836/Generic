// ReSharper disable CheckNamespace

#nullable enable

using System;
using System.Diagnostics;

using static System.AttributeTargets;

namespace SerializableValueObjects.Attributes
{
    public enum DecimalFormatType
    {
        /// <summary>
        /// Only integer values allowed (no fractional part)
        /// </summary>
        Integers = 1
    }

    /// <summary>
    /// Specifies the format for displaying decimal values in the Inspector
    /// Example of custom format:<br/>
    /// "F0" - Fixed-point: 1234 <br/>
    /// "N0" - Number with thousands separator: 1,234 <br/>
    /// "G" - General (default): 1234 or 1234.5 <br/>
    /// "0" - Custom format: 1234 <br/>
    /// </summary>
    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(Field | Property)]
    public sealed class DecimalFormatAttribute : Attribute
    {
        internal DecimalFormatType? FormatType { get; }
        internal string CustomFormat { get; }

        public DecimalFormatAttribute(DecimalFormatType formatType)
        {
            FormatType = formatType;
            CustomFormat = string.Empty;
        }

        public DecimalFormatAttribute(string customFormat)
        {
            FormatType = null;
            CustomFormat = string.IsNullOrEmpty(customFormat) ? "G" : customFormat;
        }
    }
}
