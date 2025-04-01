#nullable enable

using System.Threading;
using Cysharp.Threading.Tasks;
using Functional.Async;
using JetBrains.Annotations;

namespace Generic.Core.FinalStateMachine
{
    public interface IState
    {
        public interface IWithEnterAction
        {
            [MustUseReturnValue]
            UniTask<AsyncRichResult> OnEnterAsync(CancellationToken cancellation = default);
        }

        public interface IWithExitAction
        {
            [MustUseReturnValue]
            UniTask<AsyncRichResult> OnExitAsync(CancellationToken cancellation = default);
        }
    }
}
