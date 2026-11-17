using System;
using System.Collections.Generic;
using MathHorde.Core.Problems;

namespace MathHorde.Core.Skills
{
    /// <summary>
    /// A player's full set of per-skill statistics (contract core-domain.md
    /// 4., spec 8.). <see cref="GetMasteryLevel"/> is the ONLY sanctioned way
    /// for MathEngine/GameLogic/Presentation to query skill knowledge — a
    /// raw <see cref="SkillStatistics.MasteryEstimate"/> comparison on the
    /// caller side is forbidden (ADR-010 rule 1; code-reviewer enforced).
    ///
    /// SCOPE NOTE (task F2-06, flagged for the architect / F4-00's
    /// contract-deepening pass rather than silently decided as the final
    /// word): this type's full design — translating a
    /// <c>SkillPrerequisiteSO</c> into <see cref="SkillMasteryThresholds"/>,
    /// and the actual mastery-ESTIMATION formula that fills
    /// <see cref="SkillStatistics.MasteryEstimate"/>/<c>Confidence</c> in
    /// the first place — is ADR-010/F4-02's own declared scope
    /// ("a kontrakt még vázlat, F4-00 mélyíti", F4-00's backlog note). This
    /// type had to be created now regardless, because
    /// <c>ISkillClassifier.IsEligible</c>'s contracted signature
    /// (math-engine.md 6.) already takes a <see cref="SkillProfile"/>
    /// parameter, and no earlier task had built one yet.
    ///
    /// <see cref="GetMasteryLevel"/> here is intentionally the simplest
    /// reading that satisfies ADR-010's table using ONLY this skill's own
    /// <see cref="SkillStatistics"/> — it does not attempt
    /// core-domain.md 4.'s "Locked: soha nem találkozott vele ÉS az
    /// előfeltételei sem teljesülnek" clause, because resolving the second
    /// half would require asking MathEngine's
    /// <c>ISkillClassifier.GetPrerequisites</c>, and Core may never depend
    /// on MathEngine (ARCHITECTURE.md layering — the dependency runs the
    /// other way). Per ADR-010's own F1-F3 fallback note ("a kapu minden
    /// Introduced fallbackkel működik"), an unattempted skill
    /// (<c>Attempts == 0</c>) is treated as <see cref="SkillMasteryLevel.Introduced"/>
    /// here; prerequisite-based LOCKING is applied one layer up, by
    /// <c>ISkillClassifier.IsEligible</c> walking
    /// <c>GetPrerequisites(skill)</c> and comparing each prerequisite's
    /// <see cref="GetMasteryLevel"/> against the classifier's own
    /// <c>PrerequisiteMinLevel</c> — this method never needs to return
    /// <see cref="SkillMasteryLevel.Locked"/> for that composition to work
    /// correctly.
    /// </summary>
    public sealed class SkillProfile
    {
        private readonly IReadOnlyDictionary<SkillTag, SkillStatistics> _skills;
        private readonly SkillMasteryThresholds _thresholds;

        public IReadOnlyDictionary<SkillTag, SkillStatistics> Skills => _skills;

        public DateTimeOffset UpdatedAt { get; }

        public SkillProfile(
            IReadOnlyDictionary<SkillTag, SkillStatistics> skills,
            DateTimeOffset updatedAt,
            SkillMasteryThresholds thresholds = null)
        {
            _skills = skills ?? new Dictionary<SkillTag, SkillStatistics>();
            UpdatedAt = updatedAt;
            _thresholds = thresholds ?? SkillMasteryThresholds.Default;
        }

        /// <summary>The skill's statistics, or a freshly-initialized zero-attempt record when it has never been recorded.</summary>
        public SkillStatistics GetOrEmpty(SkillTag tag)
        {
            if (_skills.TryGetValue(tag, out var stats))
            {
                return stats;
            }

            return new SkillStatistics(
                skillId: tag,
                difficultyLevel: 1,
                attempts: 0,
                correctAnswers: 0,
                averageResponseTimeSeconds: 0f,
                medianResponseTimeSeconds: 0f,
                lastPracticedAt: null,
                masteryEstimate: 0f,
                confidence: 0f,
                typicalErrors: new Dictionary<ErrorCategory, int>());
        }

        /// <summary>The ONE sanctioned knowledge query (ADR-010 rule 1) — see this type's class comment for the Locked/prerequisite composition note.</summary>
        public SkillMasteryLevel GetMasteryLevel(SkillTag tag)
        {
            var stats = GetOrEmpty(tag);

            // ADR-010 F1-F3 fallback: an unattempted skill is Introduced, not
            // Locked — see the class comment for why prerequisite-based
            // Locked is deliberately NOT decided here.
            if (stats.Attempts == 0)
            {
                return SkillMasteryLevel.Introduced;
            }

            // ADR-010's table lists MinAttemptsForMastery as one of the two
            // fields configuring the Mastered row (alongside MasteryThreshold)
            // — spec 8.'s "never from a single correct answer" guarantee
            // depends on this Attempts floor, not on Confidence alone (a
            // freshly-seen skill can start with a deceptively high estimate).
            if (stats.Attempts >= _thresholds.MinAttemptsForMastery
                && stats.MasteryEstimate >= _thresholds.MasteryThreshold
                && stats.Confidence >= _thresholds.MasteryConfidenceMin)
            {
                return SkillMasteryLevel.Mastered;
            }

            if (stats.Attempts >= _thresholds.MinAttemptsForMastery / 2 && stats.MasteryEstimate >= _thresholds.PracticedThreshold)
            {
                return SkillMasteryLevel.Practiced;
            }

            return SkillMasteryLevel.Introduced;
        }

        /// <summary>Mastered skills (spec 8.: never from a single correct answer — guaranteed by GetMasteryLevel's Confidence/Attempts gate, not by this property itself).</summary>
        public IReadOnlyCollection<SkillTag> MasteredSkills
        {
            get
            {
                var result = new List<SkillTag>();
                foreach (var tag in _skills.Keys)
                {
                    if (GetMasteryLevel(tag) == SkillMasteryLevel.Mastered)
                    {
                        result.Add(tag);
                    }
                }

                return result;
            }
        }

        /// <summary>
        /// Recorded, non-Mastered skills in priority order — lowest
        /// MasteryEstimate first, ties broken by SkillTag declaration order
        /// for determinism (no reliance on dictionary enumeration order).
        /// </summary>
        public IReadOnlyList<SkillTag> SkillsNeedingPractice
        {
            get
            {
                var candidates = new List<SkillTag>();
                foreach (var pair in _skills)
                {
                    if (pair.Value.Attempts > 0 && GetMasteryLevel(pair.Key) != SkillMasteryLevel.Mastered)
                    {
                        candidates.Add(pair.Key);
                    }
                }

                candidates.Sort((left, right) =>
                {
                    int cmp = _skills[left].MasteryEstimate.CompareTo(_skills[right].MasteryEstimate);
                    return cmp != 0 ? cmp : ((int)left).CompareTo((int)right);
                });

                return candidates;
            }
        }
    }
}
