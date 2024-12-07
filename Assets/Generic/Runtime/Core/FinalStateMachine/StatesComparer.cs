#nullable enable

using System.Collections.Generic;

namespace Generic.Core.FinalStateMachine
{
    internal sealed class StatesComparer : IEqualityComparer<IState>
    {
        public static IEqualityComparer<IState> Instance { get; } = new StatesComparer();

        bool IEqualityComparer<IState>.Equals(IState first, IState second)
        {
            return ReferenceEquals(first, second);
        }

        int IEqualityComparer<IState>.GetHashCode(IState income)
        {
            return income.GetHashCode();
        }
    }

    internal sealed class StatesTypeComparer : IEqualityComparer<IState>
    {
        public static IEqualityComparer<IState> Instance { get; } = new StatesTypeComparer();

        bool IEqualityComparer<IState>.Equals(IState first, IState second)
        {
            return first.GetType() == second.GetType();
        }

        int IEqualityComparer<IState>.GetHashCode(IState income)
        {
            return income.GetType().GetHashCode();
        }
    }
}
