using System;
using System.Threading;
using System.Threading.Tasks;
using Functional.Async;
using NUnit.Framework;

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

        [Test]
        public async Task StateMachine_CancelledTransition_ShouldReturnCancellation()
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
        public async Task StateMachine_InvalidTrigger_ShouldReturnError()
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
        public async Task StateMachine_TransitionWithWrongInitial_ShouldReturnError()
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
        public async Task StateMachine_TransitionWithInvalidTransitionTo_ShouldReturnError()
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
        public async Task StateMachineImmutable_InitialValidTrigger_ShouldReturnSuccess()
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
        public async Task StateMachineMutable_InitialValidTrigger_ShouldReturnSuccess()
        {
            // Arrange
            var stateMachine = StateMachine.Mutable.Create<ValidTrigger>(new ValidState());

            // Act
            var result = await stateMachine.TransitAsync<ValidTrigger>();

            // Assert
            Assert.IsTrue(result.IsSuccessful);
        }

        [Test]
        public async Task StateMachineFrozen_InitialValidTrigger_ShouldReturnSuccess()
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
        public void StateMachineImmutable_AddTwoInstancesOfTheSameType_ThrowsArgumentException()
        {
            // Arrange
            var first = new FirstState();
            var second = new FirstState();

            // Act & Assert
            Assert.Throws<ArgumentException>
            (
                () => StateMachine.Immutable.CreateBuilder()
                    .WithInitialTransition<FirstTrigger>(first)
                    .WithTransition<FirstTrigger>(from: first, to: second)
                    .Build()
            );
        }

        [Test]
        public async Task StateMachineImmutable_InstanceAndTypeStatesCreation_ShouldReturnSuccess()
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
    }
}
