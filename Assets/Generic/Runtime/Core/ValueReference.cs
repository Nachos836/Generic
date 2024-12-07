using System.Diagnostics.Contracts;

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

        [Pure] // Prevent value negligence
        public bool TryGetValue(out T value)
        {
            if (_value.HasValue is false)
            {
                value = default;
                return false;
            }

            value = _value!.Value;

            return true;
        }

        public T? Value { set => _value = value; }
    }
}
