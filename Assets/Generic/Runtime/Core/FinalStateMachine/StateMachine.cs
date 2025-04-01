using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Functional.Async;
using Functional.Core.Outcome;
using JetBrains.Annotations;

namespace Generic.Core.FinalStateMachine
{
    [SuppressMessage("ReSharper", "SuspiciousTypeConversion.Global")]
    public abstract partial class StateMachine
    {
        private IState _current = InitialState.Instance;

        private protected abstract IReadOnlyDictionary<Key, IState> Transitions { get; }

        [MustUseReturnValue]
        public async UniTask<AsyncRichResult> TransitAsync<TTrigger>(CancellationToken cancellation = default)
        {
            if (cancellation.IsCancellationRequested) return AsyncRichResult.Cancel;

            var key = Key.Create<TTrigger>(from: _current);
            if (Transitions.TryGetValue(key, out var to) is false)
            {
                if (Transitions.Keys.Any(candidate => candidate.TriggerHash == key.TriggerHash))
                {
                    return new Expected.Failure($"Can't transit from { _current } by { typeof(TTrigger).Name }");
                }

                return new ArgumentException($"Can't find valid Transition by given Trigger { typeof(TTrigger).Name }");
            }

            var result = AsyncRichResult.Success;

            if (_current is IState.IWithExitAction exit)
            {
                result = result.Combine(await exit.OnExitAsync(cancellation));

                if (result.IsSuccessful is not true) return result;
            }

            _current = to;

            if (_current is IState.IWithEnterAction enter)
            {
                result = result.Combine(await enter.OnEnterAsync(cancellation));
            }

            return result;
        }

        [MustUseReturnValue]
        public Frozen ToFrozen() => new (_current, Transitions);
    }
}
