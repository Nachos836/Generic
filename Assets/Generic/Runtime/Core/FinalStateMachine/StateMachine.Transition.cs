namespace Generic.Core.FinalStateMachine
{
    public abstract partial class StateMachine
    {
        protected internal readonly struct Transition
        {
            public readonly IState From;
            public readonly IState To;

            public Transition(IState from, IState to)
            {
                From = from;
                To = to;
            }
        }
    }
}
