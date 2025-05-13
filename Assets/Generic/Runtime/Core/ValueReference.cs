using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace Generic.Core
{
    public sealed class ValueReference<T> where T : struct
    {
        private T? _value;

        public ValueReference(T value)
        {
            Value = value;
        }

        public ValueReference()
        {
            Value = null;
        }

        [MustUseReturnValue]
        public bool TryGetValue([NotNullWhen(returnValue: true)] out T? value)
        {
            if (_value.HasValue is false)
            {
                value = null;
                return false;
            }

            value = _value!.Value;

            return true;
        }

        public T? Value { set => _value = value; }
    }
}
