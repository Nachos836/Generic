using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Generic.Core.Sequencer.Tests
{
    public sealed class StateMachineEditorTests
    {
        private sealed record End : IStage;

        [Test]
        public async Task Sequencer_StartFromEnd_ReturnsSuccess()
        {
            // Arrange
            var sequencer = new Sequencer(initial: new End());

            // Act
            var result = await sequencer.StartAsync(CancellationToken.None);

            // Assert
            Assert.IsTrue(result.IsSuccessful);
        }
    }
}
