using System;

#if UNITY_EDITOR

#nullable enable

namespace Initializer
{
    partial class ServiceAsset : IEquatable<ServiceAsset>
    {
        public bool Equals(ServiceAsset other)
        {
            if (ReferenceEquals(this, other)) return true;

            return GetType() == other.GetType();
        }

        public override bool Equals(object? other)
        {
            if (other is ServiceAsset asset) return Equals(asset);

            return false;
        }

        public override int GetHashCode() => base.GetHashCode();
    }
}

#endif
