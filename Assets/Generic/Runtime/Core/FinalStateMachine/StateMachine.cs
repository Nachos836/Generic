#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
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

        public async UniTask<AsyncRichResult> TransitAsync<TTrigger>(CancellationToken cancellation = default)
        {
            if (cancellation.IsCancellationRequested) return AsyncRichResult.Cancel;

            if (TryGetDestinationState<TTrigger>(Transitions, _current, out var to, out var error) is false) return error.Value;

            var result = AsyncRichResult.Success;

            if (_current is IState.WithExitAction exit)
            {
                result = result.Combine(await exit.OnExitAsync(cancellation));

                if (result.IsSuccessful is not true) return result;
            }

            _current = to;

            return _current switch
            {
                IState.WithEnterAction enter => result.Combine(await enter.OnEnterAsync(cancellation)),
                _ => result
            };
        }

        public async UniTask<AsyncRichResult> TransitAsync<TTrigger>(TTrigger trigger, CancellationToken cancellation = default)
        {
            if (cancellation.IsCancellationRequested) return AsyncRichResult.Cancel;

            if (TryGetDestinationState<TTrigger>(Transitions, _current, out var to, out var error) is false) return error.Value;

            var result = AsyncRichResult.Success;

            if (_current is IState.WithExitAction exit)
            {
                result = result.Combine(await exit.OnExitAsync(cancellation));

                if (result.IsSuccessful is not true) return result;
            }

            _current = to;

            return _current switch
            {
                IState.WithEnterAction enter => result.Combine(await enter.OnEnterAsync(cancellation)),
                IState.WithEnterAction<TTrigger> passTrigger => result.Combine(await passTrigger.OnEnterAsync(trigger, cancellation)),
                _ => result
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryGetDestinationState<TTrigger>
        (
            IReadOnlyDictionary<Key,IState> transitions,
            IState current,
            [NotNullWhen(returnValue: true)] out IState? to,
            [NotNullWhen(returnValue: false)] out AsyncRichResult? error
        ) {
            var key = Key.Create<TTrigger>(from: current);
            if (transitions.TryGetValue(key, out to) is false)
            {
                if (transitions.Keys.Any(candidate => candidate.TriggerHash == key.TriggerHash))
                {
                    error = new Expected.Failure($"Can't transit from { current } by { typeof(TTrigger).Name }");
                    return false;
                }

                error = new ArgumentException($"Can't find valid Transition by given Trigger { typeof(TTrigger).Name }");
                return false;
            }

            error = AsyncRichResult.Success;
            return true;
        }

        [MustUseReturnValue]
        public Frozen ToFrozen() => new (_current, Transitions);
    }
}
