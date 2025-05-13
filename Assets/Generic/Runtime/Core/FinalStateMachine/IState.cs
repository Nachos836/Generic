#nullable enable

using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Cysharp.Threading.Tasks;
using Functional.Async;

namespace Generic.Core.FinalStateMachine
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public interface IState
    {
        public interface WithEnterAction
        {
            UniTask<AsyncRichResult> OnEnterAsync(CancellationToken cancellation = default);
        }

        public interface WithEnterAction<in TTrigger>
        {
            UniTask<AsyncRichResult> OnEnterAsync(TTrigger trigger, CancellationToken cancellation = default);
        }

        public interface WithExitAction
        {
            UniTask<AsyncRichResult> OnExitAsync(CancellationToken cancellation = default);
        }
    }
}
