using System;

namespace MathHorde.Core.Skills
{
    /// <summary>
    /// The configurable thresholds <see cref="SkillProfile.GetMasteryLevel"/>
    /// uses to turn a skill's raw <see cref="SkillStatistics"/> into a
    /// discrete <see cref="SkillMasteryLevel"/> (ADR-010). Core never reads
    /// ScriptableObject configuration directly (math-engine.md 0. P7, same
    /// rule mirrored for Core) — ADR-010's own "Következmények" section
    /// names exactly this value-object pattern as the way a future
    /// <c>SkillPrerequisiteSO</c>-backed caller (contract
    /// scriptable-object-schema.md 9., task F4) feeds in the real,
    /// balance-tuned numbers; until then, <see cref="Default"/> carries the
    /// literal defaults ADR-010's table documents, so no caller hand-types
    /// its own threshold (spec 16.: "nincs kódba égetett szám").
    ///
    /// NOTE (flagged for the architect / F4-00's contract-deepening pass,
    /// not silently decided): <see cref="MasteryConfidenceMin"/> is
    /// ADR-010's own "Confidence &gt;= 0.6" condition for the Mastered row,
    /// but the <c>SkillPrerequisiteSO</c> field table
    /// (scriptable-object-schema.md 9.) does not expose it as its own asset
    /// field — only <c>MasteryThreshold</c>/<c>PracticedThreshold</c>/
    /// <c>MinAttemptsForMastery</c>/<c>MinSamplesForConfidence</c>/
    /// <c>ReviewIntervalDays</c> are listed there. It is kept as a
    /// constructor-overridable value here (ADR-010's literal 0.6 as the
    /// default) rather than hand-typed as a magic number inside
    /// <see cref="SkillProfile"/>, which is the probable eventual fix for
    /// that apparent SO-schema gap.
    /// </summary>
    public sealed class SkillMasteryThresholds
    {
        /// <summary>MasteryEstimate floor for Mastered (scriptable-object-schema.md 9.: 0.5 - 1.0, default 0.8).</summary>
        public float MasteryThreshold { get; }

        /// <summary>MasteryEstimate floor for Practiced (scriptable-object-schema.md 9.: 0.2 - 0.8, default 0.5).</summary>
        public float PracticedThreshold { get; }

        /// <summary>Attempts needed for Mastered; Practiced needs half of this (scriptable-object-schema.md 9.: 3 - 50, default 8).</summary>
        public int MinAttemptsForMastery { get; }

        /// <summary>Confidence floor for Mastered (ADR-010 table; see the class-level NOTE above). Default 0.6.</summary>
        public float MasteryConfidenceMin { get; }

        public SkillMasteryThresholds(
            float masteryThreshold = 0.8f,
            float practicedThreshold = 0.5f,
            int minAttemptsForMastery = 8,
            float masteryConfidenceMin = 0.6f)
        {
            if (masteryThreshold < 0.5f || masteryThreshold > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(masteryThreshold), masteryThreshold, "MasteryThreshold 0.5..1.0 kötelező.");
            }

            if (practicedThreshold < 0.2f || practicedThreshold > 0.8f)
            {
                throw new ArgumentOutOfRangeException(nameof(practicedThreshold), practicedThreshold, "PracticedThreshold 0.2..0.8 kötelező.");
            }

            if (practicedThreshold >= masteryThreshold)
            {
                throw new ArgumentException(
                    "PracticedThreshold < MasteryThreshold kötelező (scriptable-object-schema.md 9. validáció).",
                    nameof(practicedThreshold));
            }

            if (minAttemptsForMastery < 3 || minAttemptsForMastery > 50)
            {
                throw new ArgumentOutOfRangeException(nameof(minAttemptsForMastery), minAttemptsForMastery, "MinAttemptsForMastery 3..50 kötelező.");
            }

            if (masteryConfidenceMin < 0f || masteryConfidenceMin > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(masteryConfidenceMin), masteryConfidenceMin, "MasteryConfidenceMin 0..1 kötelező.");
            }

            MasteryThreshold = masteryThreshold;
            PracticedThreshold = practicedThreshold;
            MinAttemptsForMastery = minAttemptsForMastery;
            MasteryConfidenceMin = masteryConfidenceMin;
        }

        /// <summary>ADR-010's literal defaults (0.8 / 0.5 / 8 / 0.6) — used whenever a caller does not yet have a SkillPrerequisiteSO to translate.</summary>
        public static SkillMasteryThresholds Default { get; } = new SkillMasteryThresholds();
    }
}
