namespace MathHorde.Core.Progress
{
    /// <summary>
    /// Contract core-domain.md 6. Needed by the SessionEnded event payload
    /// (F1-05, MathHorde.GameLogic.Events). "A lőszer kifogyása önmagában nem
    /// vereség" (game-logic.md 8.) — there is deliberately no AmmoDepleted
    /// value here; ammo running out never ends the session by itself.
    /// </summary>
    public enum FailureReason
    {
        None,
        HealthDepleted,
        TimeLimit,
        PlayerQuit,
        MathMinimumNotMet
    }
}
