using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Cysharp.Threading.Tasks;
using Functional.Async;

namespace Generic.Core.Sequencer
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public interface IStage
    {
        public interface WithTransition
        {
            UniTask<AsyncResult<IStage>> TransitAsync(CancellationToken cancellation = default);
        }
    }
}
