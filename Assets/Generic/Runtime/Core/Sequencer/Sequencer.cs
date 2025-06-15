using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Cysharp.Threading.Tasks;
using Functional.Async;

namespace Generic.Core.Sequencer
{
    [SuppressMessage("ReSharper", "SuspiciousTypeConversion.Global")]
    public sealed class Sequencer
    {
        private readonly IStage _initial;

        public Sequencer(IStage initial) => _initial = initial;

        public async UniTask<AsyncResult> StartAsync(CancellationToken cancellation = default)
        {
            if (cancellation.IsCancellationRequested) return AsyncResult.Cancel;

            var current = new WeakReference<IStage>(_initial);
            while (current.TryGetTarget(out var stage) && stage is IStage.WithTransition candidate)
            {
                if (cancellation.IsCancellationRequested) return AsyncResult.Cancel;

                var outcome = await candidate.TransitAsync(cancellation);
                var goingForNextStage = outcome.Attach(current).Run(static (next, current, token) =>
                {
                    if (token.IsCancellationRequested) return AsyncResult.Cancel;

                    current.SetTarget(next);

                    return AsyncResult.Success;

                }, cancellation).IsSuccessful;

                if (goingForNextStage is false) return outcome.Match
                (
                    success: static _ => AsyncResult.Impossible,
                    cancellation: static () => AsyncResult.Cancel,
                    error: static exception => AsyncResult.FromException(exception)
                );
            }

            return AsyncResult.Success;
        }
    }
}
