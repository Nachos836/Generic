using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Functional.Async;
using JetBrains.Annotations;
using NUnit.Framework;

namespace Generic.Core.FinalStateMachine.Tests
{
    internal sealed class StateMachineFunctionalTests
    {
        [UsedImplicitly] private sealed record Bootstrap;
        [UsedImplicitly] private sealed record Prefetch;
        [UsedImplicitly] private sealed record Activate;
        [UsedImplicitly] private sealed record Deactivate;
        [UsedImplicitly] private sealed record Unload;

        [Test]
        public async Task StateMachineImmutable_SceneLikeFlow_ShouldReturnSuccess()
        {
            var sharedState = new ValueReference<RawState>(RawState.Initial);

            var stateMachineFrozen = StateMachine.Immutable.CreateBuilder()
                .WithInitialTransition<Bootstrap>(to: new Unloaded(sharedState))
                .WithTransition<Prefetch, Unloaded>(to: new Prefetched(sharedState))
                .WithTransition<Activate, Prefetched>(to: new Activated(sharedState))
                .WithTransition<Deactivate, Activated>(to: new Deactivated(sharedState))
                .WithTransition<Unload, Deactivated, Unloaded>()
                .WithTransition<Unload, Activated, Unloaded>()
                .WithTransition<Unload, Prefetched, Unloaded>()
                .Build();

            var result = AsyncRichResult.Success;
            { sharedState.TryGetValue(out var state); Assert.AreEqual(RawState.Initial, state); }

            result = result.Combine(await stateMachineFrozen.TransitAsync<Bootstrap>());
            Assert.IsTrue(result.IsSuccessful);
            { sharedState.TryGetValue(out var state); Assert.AreEqual(RawState.Unloaded, state); }

            result = result.Combine(await stateMachineFrozen.TransitAsync<Prefetch>());
            Assert.IsTrue(result.IsSuccessful);
            { sharedState.TryGetValue(out var state); Assert.AreEqual(RawState.Prefetched, state); }

            result = result.Combine(await stateMachineFrozen.TransitAsync<Activate>());
            Assert.IsTrue(result.IsSuccessful);
            { sharedState.TryGetValue(out var state); Assert.AreEqual(RawState.Activated, state); }

            result = result.Combine(await stateMachineFrozen.TransitAsync<Deactivate>());
            Assert.IsTrue(result.IsSuccessful);
            { sharedState.TryGetValue(out var state); Assert.AreEqual(RawState.Deactivated, state); }

            result = result.Combine(await stateMachineFrozen.TransitAsync<Unload>());
            Assert.IsTrue(result.IsSuccessful);
            { sharedState.TryGetValue(out var state); Assert.AreEqual(RawState.Unloaded, state); }

            result = result.Combine(await stateMachineFrozen.TransitAsync<Prefetch>());
            Assert.IsTrue(result.IsSuccessful);
            { sharedState.TryGetValue(out var state); Assert.AreEqual(RawState.Prefetched, state); }

            result = result.Combine(await stateMachineFrozen.TransitAsync<Unload>());
            Assert.IsTrue(result.IsSuccessful);
            { sharedState.TryGetValue(out var state); Assert.AreEqual(RawState.Unloaded, state); }
        }

        [Test]
        public async Task StateMachineMutable_SceneLikeFlow_ShouldReturnSuccess()
        {
            var sharedState = new ValueReference<RawState>(RawState.Initial);

            var unloaded = new Unloaded(sharedState);
            var prefetched = new Prefetched(sharedState);
            var activated = new Activated(sharedState);
            var deactivated = new Deactivated(sharedState);

            var stateMachineMutable = StateMachine.Mutable.Create<Bootstrap>(unloaded)
                .AddTransition<Prefetch>(from: unloaded, to: prefetched)
                .AddTransition<Unload>(from: prefetched, to: unloaded);

            var result = AsyncRichResult.Success;

            { sharedState.TryGetValue(out var state); Assert.AreEqual(RawState.Initial, state); }

            result = result.Combine(await stateMachineMutable.TransitAsync<Bootstrap>());
            Assert.IsTrue(result.IsSuccessful);
            { sharedState.TryGetValue(out var state); Assert.AreEqual(RawState.Unloaded, state); }

            result = result.Combine(await stateMachineMutable.TransitAsync<Prefetch>());
            Assert.IsTrue(result.IsSuccessful);
            { sharedState.TryGetValue(out var state); Assert.AreEqual(RawState.Prefetched, state); }

            var stateMachineFrozen = stateMachineMutable
                .AddTransition<Activate>(from: prefetched, to: activated)
                .AddTransition<Deactivate>(from: activated, to: deactivated)
                .AddTransition<Unload>(from: deactivated, to: unloaded)
                .AddTransition<Unload>(from: activated, to: unloaded)
                .ToFrozen();

            result = result.Combine(await stateMachineFrozen.TransitAsync<Activate>());
            Assert.IsTrue(result.IsSuccessful);
            { sharedState.TryGetValue(out var state); Assert.AreEqual(RawState.Activated, state); }

            result = result.Combine(await stateMachineFrozen.TransitAsync<Deactivate>());
            Assert.IsTrue(result.IsSuccessful);
            { sharedState.TryGetValue(out var state); Assert.AreEqual(RawState.Deactivated, state); }

            result = result.Combine(await stateMachineFrozen.TransitAsync<Unload>());
            Assert.IsTrue(result.IsSuccessful);
            { sharedState.TryGetValue(out var state); Assert.AreEqual(RawState.Unloaded, state); }

            result = result.Combine(await stateMachineFrozen.TransitAsync<Prefetch>());
            Assert.IsTrue(result.IsSuccessful);
            { sharedState.TryGetValue(out var state); Assert.AreEqual(RawState.Prefetched, state); }

            result = result.Combine(await stateMachineFrozen.TransitAsync<Unload>());
            Assert.IsTrue(result.IsSuccessful);
            { sharedState.TryGetValue(out var state); Assert.AreEqual(RawState.Unloaded, state); }
        }


        ////////////////////////////////////////////////////////////////////


        private sealed record Unloaded : IState, IState.WithEnterAction
        {
            private readonly ValueReference<RawState> _sharedState;

            public Unloaded(ValueReference<RawState> sharedState)
            {
                _sharedState = sharedState;
            }

            UniTask<AsyncRichResult> IState.WithEnterAction.OnEnterAsync(CancellationToken cancellation)
            {
                if (cancellation.IsCancellationRequested) return UniTask.FromCanceled<AsyncRichResult>(cancellation);

                _sharedState.Value = RawState.Unloaded;

                return UniTask.FromResult(AsyncRichResult.Success);
            }
        }

        private sealed record Prefetched : IState, IState.WithEnterAction
        {
            private readonly ValueReference<RawState> _sharedState;

            public Prefetched(ValueReference<RawState> sharedState)
            {
                _sharedState = sharedState;
            }

            UniTask<AsyncRichResult> IState.WithEnterAction.OnEnterAsync(CancellationToken cancellation)
            {
                if (cancellation.IsCancellationRequested) return UniTask.FromCanceled<AsyncRichResult>(cancellation);

                _sharedState.Value = RawState.Prefetched;

                return UniTask.FromResult(AsyncRichResult.Success);
            }
        }

        private sealed record Activated : IState, IState.WithEnterAction
        {
            private readonly ValueReference<RawState> _sharedState;

            public Activated(ValueReference<RawState> sharedState)
            {
                _sharedState = sharedState;
            }

            UniTask<AsyncRichResult> IState.WithEnterAction.OnEnterAsync(CancellationToken cancellation)
            {
                if (cancellation.IsCancellationRequested) return UniTask.FromCanceled<AsyncRichResult>(cancellation);

                _sharedState.Value = RawState.Activated;

                return UniTask.FromResult(AsyncRichResult.Success);
            }
        }

        private sealed record Deactivated : IState, IState.WithEnterAction
        {
            private readonly ValueReference<RawState> _sharedState;

            public Deactivated(ValueReference<RawState> sharedState)
            {
                _sharedState = sharedState;
            }

            UniTask<AsyncRichResult> IState.WithEnterAction.OnEnterAsync(CancellationToken cancellation)
            {
                if (cancellation.IsCancellationRequested) return UniTask.FromCanceled<AsyncRichResult>(cancellation);

                _sharedState.Value = RawState.Deactivated;

                return UniTask.FromResult(AsyncRichResult.Success);
            }
        }

        private enum RawState
        {
            Initial = 69,
            Prefetched,
            Activated,
            Deactivated,
            Unloaded
        }
    }
}
