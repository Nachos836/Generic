#nullable enable

using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Cysharp.Threading.Tasks;
using Functional.Async;
using JetBrains.Annotations;

namespace Generic.Core.FinalStateMachine
{
    [PublicAPI]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public interface IState
    {
        [PublicAPI]
        public interface WithEnterAction
        {
            UniTask<AsyncRichResult> OnEnterAsync(CancellationToken cancellation = default);
        }

        [PublicAPI]
        public interface WithEnterAction<in TTrigger>
        {
            UniTask<AsyncRichResult> OnEnterAsync(TTrigger trigger, CancellationToken cancellation = default);
        }

        [PublicAPI]
        public interface WithExitAction
        {
            UniTask<AsyncRichResult> OnExitAsync(CancellationToken cancellation = default);
        }
    }
}
