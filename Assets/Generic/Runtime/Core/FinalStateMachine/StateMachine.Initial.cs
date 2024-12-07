namespace Generic.Core.FinalStateMachine
{
    public abstract partial class StateMachine
    {
        private sealed class InitialState : IState
        {
            public static IState Instance { get; } = new InitialState();
        }
    }
}
