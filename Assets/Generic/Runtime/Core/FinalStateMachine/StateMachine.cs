using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Threading;
using Cysharp.Threading.Tasks;
using Functional.Async;
using Functional.Core.Outcome;

namespace Generic.Core.FinalStateMachine
{
    [SuppressMessage("ReSharper", "SuspiciousTypeConversion.Global")]
    public abstract partial class StateMachine
    {
        public static StateMachine Empty { get; } = Immutable.CreateBuilder().Build();

        private IState _current = InitialState.Instance;

        private protected abstract IReadOnlyDictionary<Key, Transition> Transitions { get; }

        /// <summary>
        /// NOTE: Marked as Pure to prevent value negligence
        /// </summary>
        /// <param name="cancellation"></param>
        /// <typeparam name="TTrigger"></typeparam>
        /// <returns></returns>
        [Pure] public async UniTask<AsyncRichResult> TransitAsync<TTrigger>(CancellationToken cancellation = default)
        {
            if (cancellation.IsCancellationRequested) return AsyncRichResult.Cancel;

            var key = Key.Create<TTrigger>(from: _current);
            if (Transitions.TryGetValue(key, out var transition) is false) return new ArgumentException($"Can't find valid Transition by given Trigger { typeof(TTrigger).Name }");
            if (transition.From != _current) return new Expected.Failure($"Can't transit from { transition.From } to { transition.To }");

            var result = AsyncRichResult.Success;

            if (_current is IState.IWithExitAction exit)
            {
                result = result.Combine(await exit.OnExitAsync(cancellation));

                if (result.IsSuccessful is not true) return result;
            }

            _current = transition.To;

            if (_current is IState.IWithEnterAction enter)
            {
                result = result.Combine(await enter.OnEnterAsync(cancellation));
            }

            return result;
        }

        [Pure] // Prevent value negligence
        public Frozen ToFrozen() => new (_current, Transitions);
    }
}
