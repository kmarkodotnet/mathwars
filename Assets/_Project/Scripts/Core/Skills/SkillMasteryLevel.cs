namespace MathHorde.Core.Skills
{
    /// <summary>
    /// Discrete skill-mastery tiers (ADR-010, contract core-domain.md 4.,
    /// spec 7.2/7.4/8.). Monotonic by design — Locked &lt; Introduced &lt;
    /// Practiced &lt; Mastered — so every caller compares with
    /// <c>&gt;=</c> (ADR-010 rule 3) instead of branching on each value.
    ///
    /// Explicit numeric values per the enum rule (core-domain.md 0.): a
    /// persisted <see cref="SkillMasteryLevel"/>-derived decision is not
    /// itself stored (ADR-010's "Negatív" consequence: it is always
    /// recomputed from <see cref="SkillStatistics"/>, never persisted), but
    /// the enum follows the project-wide convention anyway so a future
    /// persisted use never silently reorders.
    /// </summary>
    public enum SkillMasteryLevel
    {
        /// <summary>The skill's prerequisites are not met — the generator must not offer it (ISkillClassifier.IsEligible, F2-06).</summary>
        Locked = 0,

        /// <summary>Prerequisites are met; the generator may offer it. Attempts == 0 also lands here (ADR-010).</summary>
        Introduced = 1,

        /// <summary>Attempts &gt;= MinAttemptsForMastery / 2 (alap 4) and MasteryEstimate &gt;= PracticedThreshold (alap 0.5).</summary>
        Practiced = 2,

        /// <summary>MasteryEstimate &gt;= MasteryThreshold (alap 0.8) and Confidence &gt;= 0.6 — never from a single correct answer (spec 8.).</summary>
        Mastered = 3
    }
}
