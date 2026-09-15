using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Cysharp.Threading.Tasks;
using Functional.Async;
using JetBrains.Annotations;

namespace Generic.Core.Sequencer
{
    [PublicAPI]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public interface IStage
    {
        [PublicAPI]
        public interface WithTransition : IStage
        {
            UniTask<AsyncResult<IStage>> TransitAsync(CancellationToken cancellation = default);
        }

        [PublicAPI]
        public interface WithDataReceiver<T> : IStage
        {
            void Receive(in T data);
        }
    }
}
