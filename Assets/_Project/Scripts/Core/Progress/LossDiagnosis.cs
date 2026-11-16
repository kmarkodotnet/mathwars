namespace MathHorde.Core.Progress
{
    /// <summary>
    /// Contract core-domain.md 6. — the Phase-4 adaptive-difficulty engine's
    /// INPUT: a measured diagnosis of why Health reached zero, NOT a game
    /// rule (see FailureReason, which IS the rule — "a lőszerhiány és az
    /// ellenség-áttörés NEM önálló vereség-ok", spec 1.3). GameSessionService
    /// always reports None in Phase 1-3 (core-domain.md 6.: "Fázis 4;
    /// F1–F3-ban None") — the measurement heuristics themselves are F4 scope.
    /// Explicit numeric values per the enum rule (core-domain.md 0.).
    /// </summary>
    public enum LossDiagnosis
    {
        None = 0,
        AmmoStarvation = 1,
        EnemyBreakthrough = 2,
        MathAccuracyShortfall = 3,
        BossPhaseStall = 4
    }
}
