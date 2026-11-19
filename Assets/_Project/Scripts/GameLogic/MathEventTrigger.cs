namespace MathHorde.GameLogic
{
    /// <summary>
    /// Implementation-level inputs for the ARCHITECTURE.md 5. state graph.
    /// The director supplies timing, zone entry, selection and reward notifications;
    /// the lifecycle itself neither calculates deadlines nor applies rewards.
    /// </summary>
    public enum MathEventTrigger
    {
        Schedule = 0,
        PreviewElapsed = 1,
        DecisionZoneEntered = 2,
        LaneSelected = 3,
        DeadlineReached = 4,
        RewardApplied = 5,
        SessionEnded = 6
    }
}
