#nullable enable

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Generic.Samples.GameplayInventory.Concrete;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using static System.Runtime.CompilerServices.MethodImplOptions;

// ReSharper disable once CheckNamespace
namespace Generic.Samples.GameplayInventory
{
/// <summary>
    /// Visitor for exhaustive matching over the <see cref="Item"/> tagged union.
    /// Adding a new <see cref="IItem"/> type forces every implementation to handle it.
    /// </summary>
    [CompilerGenerated]
    public interface IItemVisitor<out TResult>
    {
        TResult Visit(in Prize prize);
        TResult Visit(in Money money);
        TResult Visit(in Gem gem);
    }

    /// <summary>
    /// Void-returning visitor (side effects only).
    /// </summary>
    [CompilerGenerated]
    public interface IItemVisitor
    {
        void Visit(in Prize prize);
        void Visit(in Money money);
        void Visit(in Gem gem);
    }

    [BurstCompile]
    [StructLayout(LayoutKind.Explicit)]
    [CompilerGenerated]
    public readonly partial struct Item : IItem
    {
        internal static readonly int TypeIdCount = Enum.GetValues(typeof(PrivateTypeId)).Length;

        [FieldOffset(0)] private readonly PrivateTypeId CurrentPrivateTypeId;

        [FieldOffset(sizeof(PrivateTypeId))] private readonly Prize _prize;
        [FieldOffset(sizeof(PrivateTypeId))] private readonly Money _money;
        [FieldOffset(sizeof(PrivateTypeId))] private readonly Gem _gem;

        internal Item(Prize prize)
        {
            CurrentPrivateTypeId = PrivateTypeId.Prize;
            _money = default;
            _gem = default;
            _prize = prize;
        }

        internal Item(Money money)
        {
            CurrentPrivateTypeId = PrivateTypeId.Money;
            _prize = default;
            _gem = default;
            _money = money;
        }

        internal Item(Gem gem)
        {
            CurrentPrivateTypeId = PrivateTypeId.Gem;
            _prize = default;
            _money = default;
            _gem = gem;
        }

        [MethodImpl(AggressiveInlining)]
        internal static TypeId GetType<TItem>() where TItem : unmanaged, IItem
        {
            if (typeof(TItem) == typeof(Prize))
            {
                return new TypeId((ulong) PrivateTypeId.Prize);
            }
            else if (typeof(TItem) == typeof(Money))
            {
                return new TypeId((ulong) PrivateTypeId.Money);
            }
            else if (typeof(TItem) == typeof(Gem))
            {
                return new TypeId((ulong) PrivateTypeId.Gem);
            }
            else
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS && DEBUG
                throw new ArgumentOutOfRangeException(typeof(TItem).Name, "Income is not supported!");
#else
                return default;
#endif
            }
        }

        [MethodImpl(AggressiveInlining)]
        internal static TypeId GetType<TItem>(in TItem income) where TItem : unmanaged, IItem
        {
            return new TypeId((ulong) income.ToUnion().CurrentPrivateTypeId);
        }

        [MethodImpl(AggressiveInlining)]
        internal unsafe ref readonly TItem Cast<TItem>() where TItem : unmanaged, IItem
        {
            if (typeof(TItem) == typeof(Prize))
            {
                fixed (void* raw = &_prize)
                {
                    return ref UnsafeUtility.ArrayElementAsRef<TItem>(raw, index: 0);
                }
            }
            else if (typeof(TItem) == typeof(Money))
            {
                fixed (void* raw = &_money)
                {
                    return ref UnsafeUtility.ArrayElementAsRef<TItem>(raw, index: 0);
                }
            }
            else if (typeof(TItem) == typeof(Gem))
            {
                fixed (void* raw = &_gem)
                {
                    return ref UnsafeUtility.ArrayElementAsRef<TItem>(raw, index: 0);
                }
            }
            else
            {
                #if ENABLE_UNITY_COLLECTIONS_CHECKS && DEBUG
                   throw new ArgumentOutOfRangeException(typeof(TItem).Name, "Income is not supported!");
                #else
                   return default;
                #endif
            }
        }

        [MethodImpl(AggressiveInlining)]
        public TResult Accept<TResult>(IItemVisitor<TResult> visitor)
        {
            return CurrentPrivateTypeId switch
            {
                PrivateTypeId.Prize => visitor.Visit(in _prize),
                PrivateTypeId.Money => visitor.Visit(in _money),
                PrivateTypeId.Gem => visitor.Visit(in _gem),

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                _ => throw new ArgumentOutOfRangeException(nameof(CurrentPrivateTypeId), CurrentPrivateTypeId, "Unknown item type")
#else
                _ => default!
#endif
            };
        }

        [MethodImpl(AggressiveInlining)]
        public void Accept(IItemVisitor visitor)
        {
            switch (CurrentPrivateTypeId)
            {
                case PrivateTypeId.Prize: visitor.Visit(in _prize); break;
                case PrivateTypeId.Money: visitor.Visit(in _money); break;
                case PrivateTypeId.Gem: visitor.Visit(in _gem);   break;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                default: throw new ArgumentOutOfRangeException(nameof(CurrentPrivateTypeId), CurrentPrivateTypeId, "Unknown item type");
#endif
            }
        }

        private enum PrivateTypeId : ulong
        {
            Prize = 1,
            Money = 2,
            Gem = 3
        }

        [BurstCompile]
        public readonly struct TypeId : IEquatable<TypeId>
        {
            private readonly ulong _privateTypeId;

            internal TypeId(ulong privateTypeId) => _privateTypeId = privateTypeId;

            public bool Equals(TypeId other) => _privateTypeId == other._privateTypeId;
            public override bool Equals(object? obj) => throw new Exception("Should not be used");
            public override int GetHashCode() => (int) _privateTypeId;
            public override string ToString() => _privateTypeId.ToString();
        }
    }

    [CompilerGenerated]
    internal static class ItemExtensions
    {
        [MethodImpl(AggressiveInlining)]
        public static Item ToUnion<TItem>(this TItem income) where TItem : unmanaged, IItem
        {
            if (typeof(TItem) == typeof(Prize))
            {
                return new Item(UnsafeUtility.As<TItem, Prize>(ref income));
            }
            else if (typeof(TItem) == typeof(Money))
            {
                return new Item(UnsafeUtility.As<TItem, Money>(ref income));
            }
            else if (typeof(TItem) == typeof(Gem))
            {
                return new Item(UnsafeUtility.As<TItem, Gem>(ref income));
            }
            else
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS && DEBUG
                throw new ArgumentOutOfRangeException(typeof(TItem).Name, "Income is not supported!");
#else
                return default;
#endif
            }
        }
    }
}
