using System;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Functional.Async;
using NUnit.Framework;
using UnityEditor.PackageManager;

namespace Generic.Core.FinalStateMachine.Tests
{
    public sealed class StateMachineEditorTests
    {
        private abstract record ValidTrigger;
        private abstract record InvalidTrigger;
        private abstract record FirstTrigger;
        private abstract record SecondTrigger;
        private abstract record ThirdTrigger;

        private sealed record ValidState : IState;
        private sealed record FirstState : IState;
        private sealed record SecondState : IState;
        private sealed record ThirdState : IState;

        private sealed record EnterActionState : IState, IState.WithEnterAction
        {
            UniTask<AsyncRichResult> IState.WithEnterAction.OnEnterAsync(CancellationToken _) => UniTask.FromResult(AsyncRichResult.Success);
        }
        private sealed record ExitActionState : IState, IState.WithExitAction
        {
            UniTask<AsyncRichResult> IState.WithExitAction.OnExitAsync(CancellationToken _) => UniTask.FromResult(AsyncRichResult.Success);
        }
        private sealed record FailedExitActionState : IState, IState.WithExitAction
        {
            UniTask<AsyncRichResult> IState.WithExitAction.OnExitAsync(CancellationToken _) => UniTask.FromResult(AsyncRichResult.Failure);
        }
        private sealed record EnterAndExitActionState : IState, IState.WithEnterAction, IState.WithExitAction
        {
            UniTask<AsyncRichResult> IState.WithEnterAction.OnEnterAsync(CancellationToken _) => UniTask.FromResult(AsyncRichResult.Success);
            UniTask<AsyncRichResult> IState.WithExitAction.OnExitAsync(CancellationToken _) => UniTask.FromResult(AsyncRichResult.Success);
        }
        private sealed record ValueOnEnterActionState : IState, IState.WithEnterAction<int>
        {
            UniTask<AsyncRichResult> IState.WithEnterAction<int>.OnEnterAsync(int trigger, CancellationToken cancellation)
            {
                if (cancellation.IsCancellationRequested) return UniTask.FromResult<AsyncRichResult>(cancellation);
                if (trigger > 42) return UniTask.FromResult<AsyncRichResult>(new ArgumentOutOfRangeException(nameof(trigger)));
                if (trigger < 42) return UniTask.FromResult<AsyncRichResult>(new Functional.Core.Outcome.Expected.Failure(nameof(trigger)));

                Assert.That(trigger, Is.EqualTo(42));
                return UniTask.FromResult(AsyncRichResult.Success);
            }
        }

        [Test]
        public async Task StateMachineImmutable_CancelledTransition_ReturnsCancellation()
        {
            // Arrange
            var builder = StateMachine.Immutable.CreateBuilder();
            var stateMachine = builder.Build();
            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            // Act
            var result = await stateMachine.TransitAsync<ValidTrigger>(cancellationTokenSource.Token);

            // Assert
            Assert.IsTrue(result.IsCancellation);
        }

        [Test]
        public async Task StateMachineImmutable_InvalidTrigger_ReturnsError()
        {
            // Arrange
            var builder = StateMachine.Immutable.CreateBuilder();
            var stateMachine = builder.Build();

            // Act
            var result = await stateMachine.TransitAsync<InvalidTrigger>(CancellationToken.None);

            // Assert
            Assert.IsTrue(result.IsError);
        }

        [Test]
        public async Task StateMachineImmutable_TransitionWithWrongInitial_ReturnsError()
        {
            // Arrange
            var builder = StateMachine.Immutable.CreateBuilder();
            var stateMachine = builder
                .WithInitialTransition<ValidTrigger>(new ValidState())
                .Build();

            // Act
            var result = await stateMachine.TransitAsync<InvalidTrigger>(CancellationToken.None);

            // Assert
            Assert.IsTrue(result.IsError);
        }

        [Test]
        public async Task StateMachineImmutable_TransitionWithInvalidTransitionTo_ReturnsError()
        {
            // Arrange
            var builder = StateMachine.Immutable.CreateBuilder();
            var stateMachine = builder.Build();

            // Act
            var result = await stateMachine.TransitAsync<ValidTrigger>(CancellationToken.None);

            // Assert
            Assert.IsTrue(result.IsError);
        }

        [Test]
        public async Task StateMachineImmutable_InitialValidTrigger_ReturnsSuccess()
        {
            // Arrange
            var stateMachine = StateMachine.Immutable.CreateBuilder()
                .WithInitialTransition<ValidTrigger>(new ValidState())
                .Build();

            // Act
            var result = await stateMachine.TransitAsync<ValidTrigger>();

            // Assert
            Assert.IsTrue(result.IsSuccessful);
        }

        [Test]
        public void StateMachineImmutable_AddingDuplicatedGenericReferenceStates_ThrowsArgumentException()
        {
            // Arrange
            var first = new FirstState();
            var second = new SecondState();
            var third = new FirstState();

            // Act & Assert
            Assert.Throws<ArgumentException>
            (
                () => _ = StateMachine.Immutable.CreateBuilder()
                    .WithInitialTransition<FirstTrigger>(first)
                    .WithTransition<SecondTrigger>(from: first, to: second)
                    .WithTransition<ThirdTrigger, SecondState>(to: third)
                    .Build()
            );
        }

        [Test]
        public void StateMachineImmutable_AddingDuplicatedConcreteReferenceStates_ThrowsArgumentException()
        {
            // Arrange
            var first = new FirstState();
            var second = new SecondState();
            var third = new FirstState();

            // Act & Assert
            Assert.Throws<ArgumentException>
            (
                () => _ = StateMachine.Immutable.CreateBuilder()
                    .WithInitialTransition<FirstTrigger>(first)
                    .WithTransition<SecondTrigger>(from: first, to: second)
                    .WithTransition<ThirdTrigger>(from: third, to: second)
                    .Build()
            );
        }

        [Test]
        public void StateMachineImmutable_AddTwoInstancesOfTheSameType_ThrowsArgumentException()
        {
            // Arrange
            var first = new FirstState();
            var second = new FirstState();

            // Act & Assert
            Assert.Throws<ArgumentException>
            (
                () => _ = StateMachine.Immutable.CreateBuilder()
                    .WithInitialTransition<FirstTrigger>(first)
                    .WithTransition<FirstTrigger>(from: first, to: second)
                    .Build()
            );
        }

        [Test]
        public async Task StateMachineImmutable_OneTransitionToUnreachableState_ReturnsFailure()
        {
            // Arrange
            var stateMachine = StateMachine.Immutable.CreateBuilder()
                .WithInitialTransition<FirstTrigger>(new FirstState())
                .WithTransition<SecondTrigger, FirstState>(new SecondState())
                .Build();

            var result = AsyncRichResult.Success;

            // Act
            result = result.Combine(await stateMachine.TransitAsync<SecondTrigger>());

            // Assert
            Assert.IsTrue(result.IsFailure);
        }

        [Test]
        public async Task StateMachineImmutable_ThreeTransitionsToUnreachableState_ReturnsFailure()
        {
            // Arrange
            var stateMachine = StateMachine.Immutable.CreateBuilder()
                .WithInitialTransition<FirstTrigger>(new FirstState())
                .WithTransition<SecondTrigger, FirstState>(new SecondState())
                .WithTransition<ThirdTrigger, SecondState>(new ThirdState())
                .Build();

            var result = AsyncRichResult.Success;

            // Act
            result = result.Combine(await stateMachine.TransitAsync<FirstTrigger>());
            result = result.Combine(await stateMachine.TransitAsync<ThirdTrigger>());

            // Assert
            Assert.IsTrue(result.IsFailure);
        }

        [Test]
        public async Task StateMachineImmutable_InstanceAndTypeStatesCreation_ReturnsSuccess()
        {
            {
                // Arrange
                var first = new FirstState();
                var second = new SecondState();
                var third = new ThirdState();

                var stateMachine = StateMachine.Immutable.CreateBuilder()
                    .WithInitialTransition<FirstTrigger>(first)
                    .WithTransition<SecondTrigger>(from: first, to: second)
                    .WithTransition<ThirdTrigger>(from: second, to: third)
                    .WithTransition<FirstTrigger>(from: third, to: first)
                    .Build();

                var result = AsyncRichResult.Success;

                // Act
                result = result.Combine(await stateMachine.TransitAsync<FirstTrigger>());
                result = result.Combine(await stateMachine.TransitAsync<SecondTrigger>());
                result = result.Combine(await stateMachine.TransitAsync<ThirdTrigger>());
                result = result.Combine(await stateMachine.TransitAsync<FirstTrigger>());

                // Assert
                Assert.IsTrue(result.IsSuccessful);
            }

            {
                // Arrange
                var stateMachine = StateMachine.Immutable.CreateBuilder()
                    .WithInitialTransition<FirstTrigger>(new FirstState())
                    .WithTransition<SecondTrigger, FirstState>(new SecondState())
                    .WithTransition<ThirdTrigger, SecondState>(new ThirdState())
                    .WithTransition<FirstTrigger, ThirdState, FirstState>()
                    .Build();

                var result = AsyncRichResult.Success;

                // Act
                result = result.Combine(await stateMachine.TransitAsync<FirstTrigger>());
                result = result.Combine(await stateMachine.TransitAsync<SecondTrigger>());
                result = result.Combine(await stateMachine.TransitAsync<ThirdTrigger>());
                result = result.Combine(await stateMachine.TransitAsync<FirstTrigger>());

                // Assert
                Assert.IsTrue(result.IsSuccessful);
            }
        }

        [Test]
        public async Task StateMachineFrozen_InitialValidTrigger_ReturnsSuccess()
        {
            // Arrange
            var stateMachine = StateMachine.Mutable.Create<ValidTrigger>(new ValidState())
                .ToFrozen();

            // Act
            var result = await stateMachine.TransitAsync<ValidTrigger>();

            // Assert
            Assert.IsTrue(result.IsSuccessful);
        }

        [Test]
        public async Task StateMachineFrozen_ToMutableAndTrigger_ReturnsSuccess()
        {
            // Arrange
            var frozen = StateMachine.Immutable.CreateBuilder()
                .WithInitialTransition<ValidTrigger>(new ValidState())
                .Build()
                .ToFrozen();
            var mutable = frozen.ToMutable();

            // Act
            var resultFrozen = await frozen.TransitAsync<ValidTrigger>();
            var resultMutable = await mutable.TransitAsync<ValidTrigger>();

            // Assert
            Assert.IsTrue(resultFrozen.Combine(resultMutable).IsSuccessful);
        }

        [Test]
        public async Task StateMachineMutable_InitialValidTrigger_ReturnsSuccess()
        {
            // Arrange
            var stateMachine = StateMachine.Mutable.Create<ValidTrigger>(new ValidState());

            // Act
            var result = await stateMachine.TransitAsync<ValidTrigger>();

            // Assert
            Assert.IsTrue(result.IsSuccessful);
        }

        [Test]
        public void StateMachineMutable_AddDuplicatedTrigger_ThrowsArgumentException()
        {
            // Arrange
            var first = new FirstState();
            var second = new SecondState();
            var third = new ThirdState();

            // Act & Assert
            Assert.Throws<ArgumentException>
            (
                () => _ = StateMachine.Mutable.Create<FirstTrigger>(first)
                    .AddTransition<SecondTrigger>(from: first, to: second)
                    .AddTransition<SecondTrigger>(from: first, to: third)
            );
        }

        [Test]
        public async Task StateMachine_EnterActionValidTrigger_ReturnsSuccess()
        {
            // Arrange
            StateMachine stateMachine = StateMachine.Immutable.CreateBuilder()
                .WithInitialTransition<ValidTrigger>(new EnterActionState())
                .Build();

            // Act
            var result = await stateMachine.TransitAsync<ValidTrigger>();

            // Assert
            Assert.IsTrue(result.IsSuccessful);
        }

        [Test]
        public async Task StateMachine_ExitActionValidTrigger_ReturnsSuccess()
        {
            // Arrange
            StateMachine stateMachine = StateMachine.Immutable.CreateBuilder()
                .WithInitialTransition<FirstTrigger>(new ExitActionState())
                .WithTransition<SecondTrigger, ExitActionState>(new ValidState())
                .Build();

            // Act
            var result = await stateMachine.TransitAsync<FirstTrigger>();
            result = result.Combine(await stateMachine.TransitAsync<SecondTrigger>());

            // Assert
            Assert.IsTrue(result.IsSuccessful);
        }

        [Test]
        public async Task StateMachine_EnterAndExitActionValidTrigger_ReturnsSuccess()
        {
            // Arrange
            StateMachine stateMachine = StateMachine.Immutable.CreateBuilder()
                .WithInitialTransition<FirstTrigger>(new EnterAndExitActionState())
                .WithTransition<SecondTrigger, EnterAndExitActionState>(new ValidState())
                .Build();

            // Act
            var result = await stateMachine.TransitAsync<FirstTrigger>();
            result = result.Combine(await stateMachine.TransitAsync<SecondTrigger>());

            // Assert
            Assert.IsTrue(result.IsSuccessful);
        }

        [Test]
        public async Task StateMachine_EnterEnterValueAction_ReturnsSuccess()
        {
            // Arrange
            StateMachine stateMachine = StateMachine.Immutable.CreateBuilder()
                .WithInitialTransition<int>(new ValueOnEnterActionState())
                .Build();

            // Act
            var result = await stateMachine.TransitAsync(42);

            // Assert
            Assert.IsTrue(result.IsSuccessful);
        }

        [Test]
        public async Task StateMachine_FailedExitActionValidTrigger_ReturnsFailure()
        {
            // Arrange
            StateMachine stateMachine = StateMachine.Immutable.CreateBuilder()
                .WithInitialTransition<FirstTrigger>(new FailedExitActionState())
                .WithTransition<SecondTrigger, FailedExitActionState>(new ValidState())
                .Build();

            // Act
            var result = await stateMachine.TransitAsync<FirstTrigger>();
            result = result.Combine(await stateMachine.TransitAsync<SecondTrigger>());

            // Assert
            Assert.IsTrue(result.IsFailure);
        }
    }
}
