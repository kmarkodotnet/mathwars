namespace MathHorde.GameLogic.AppFlow
{
    /// <summary>Payload of IAppStateMachine.Transitioned — one completed edge.</summary>
    public readonly struct AppStateTransition
    {
        public AppState From { get; }
        public AppState To { get; }
        public AppTrigger Trigger { get; }

        public AppStateTransition(AppState from, AppState to, AppTrigger trigger)
        {
            From = from;
            To = to;
            Trigger = trigger;
        }
    }
}
