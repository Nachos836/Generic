using System;
using System.Collections.Immutable;

namespace CustomAttributesEditorGenerator.Components;

internal sealed class ButtonModel
{
    public required string Name { get; init; }
    public required string Label { get; init; }
}

internal sealed class DrawerModel : IEquatable<DrawerModel>
{
    public required string Identity { get; init; }
    public required string NamespaceName { get; init; }
    public required string OwnerName { get; init; }
    public required bool RequiresCustomUI { get; init; }
    public required ImmutableArray<ButtonModel> Buttons { get; init; }

    public bool Equals(DrawerModel? other)
    {
        if (ReferenceEquals(this, other)) return true;
        if (other is null) return false;

        if (Identity != other.Identity
        || NamespaceName != other.NamespaceName
        || OwnerName != other.OwnerName
        || RequiresCustomUI != other.RequiresCustomUI
        || Buttons.Length != other.Buttons.Length)
            return false;

        for (var index = 0; index < Buttons.Length; index++)
        {
            var left = Buttons[index];
            var right = other.Buttons[index];

            if (left.Name != right.Name || left.Label != right.Label) return false;
        }

        return true;
    }

    public override bool Equals(object? obj) => obj is DrawerModel other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = StringComparer.Ordinal.GetHashCode(Identity);
            hash = hash * 31 + StringComparer.Ordinal.GetHashCode(NamespaceName);
            hash = hash * 31 + StringComparer.Ordinal.GetHashCode(OwnerName);
            hash = hash * 31 + RequiresCustomUI.GetHashCode();

            foreach (var button in Buttons)
            {
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(button.Name);
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(button.Label);
            }

            return hash;
        }
    }
}
