#nullable enable

using System.Diagnostics.Contracts;
using System.Threading;
using Cysharp.Threading.Tasks;
using Functional.Async;

namespace Generic.Core.FinalStateMachine
{
    public interface IState
    {
        public interface IWithEnterAction
        {
            /// Marked as pure to prevent value negligence
            [Pure] UniTask<AsyncRichResult> OnEnterAsync(CancellationToken cancellation = default);
        }

        public interface IWithExitAction
        {
            /// Marked as pure to prevent value negligence
            [Pure] UniTask<AsyncRichResult> OnExitAsync(CancellationToken cancellation = default);
        }
    }
}
