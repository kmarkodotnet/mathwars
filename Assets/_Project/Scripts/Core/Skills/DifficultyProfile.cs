using System;
using System.Collections.Generic;
using System.Linq;
using MathHorde.Core.Expressions;
using MathHorde.Core.Problems;

namespace MathHorde.Core.Skills
{
    /// <summary>
    /// The three difficulty dimensions (spec 7.3) plus the global challenge
    /// level — contract core-domain.md 4. Engine-free, immutable
    /// (core-domain.md 0.): this is the ONLY shape in which the math engine
    /// (math-engine.md P7: "Az engine nem olvas konfigurációt: minden korlát a
    /// DifficultyProfile-ból és a request mezőiből jön") and the GameLogic
    /// (game-logic.md 11. IConfigurationProvider.GetDifficultyForLevel) see the
    /// 10-row difficulty table. The table itself lives in
    /// MathDifficultyTableSO (Infrastructure, scriptable-object-schema.md 5.)
    /// and is translated here by GameConfigurationRepository — nothing in this
    /// type may be hard-coded by a consumer (spec 7.2: "A táblázat
    /// konfigurálható legyen, ne kerüljön beégetésre a kódba").
    ///
    /// MaxResultValue is an independent configured limit, never derived from
    /// MaxOperandValue (ADR-012, core-domain.md 4., math-engine.md 5.).
    ///
    /// CONTRACT NOTE 2 — UnknownPlacement: the per-level UnknownPlacement
    /// column (scriptable-object-schema.md 5., L8-L9 RightOperand, L10
    /// EitherSide) CANNOT be carried here: the enum is declared in the
    /// MathEngine layer (math-engine.md 3.) and Core must not reference
    /// MathEngine. It stays readable on MathDifficultyTableSO.
    ///
    /// ResourceCapacity belongs to MathProblem (ADR-013), not this profile:
    /// it captures a resource pool maximum rather than a difficulty value.
    /// </summary>
    public sealed class DifficultyProfile
    {
        /// <summary>1..10 — the globally visible level shown on the HUD.</summary>
        public int ChallengeLevel { get; }

        /// <summary>1..10 — the mathematical-content dimension (spec 7.3).</summary>
        public int MathComplexity { get; }

        /// <summary>1..10 — the time-pressure dimension (spec 7.3).</summary>
        public int TimePressure { get; }

        /// <summary>1..10 — the combat dimension (spec 7.3).</summary>
        public int CombatDifficulty { get; }

        /// <summary>
        /// Only these operators may be generated — the intersection of the
        /// parent setting and the skill prerequisite gate (spec 7.2).
        /// </summary>
        public IReadOnlyCollection<BinaryOperator> AllowedOperators { get; }

        /// <summary>
        /// Skills the generator may draw on. Widening this set may happen ONLY
        /// through the SkillProfile (spec 7.2/7.4): raising the global level
        /// never by itself unlocks an untaught operation. The final set is the
        /// intersection of WorldDefinition.IntroducedSkills and the skills at
        /// SkillMasteryLevel.Introduced or above (worlds.md 1., ADR-010).
        /// </summary>
        public IReadOnlyCollection<SkillTag> UnlockedSkills { get; }

        public int MinOperandValue { get; }

        public int MaxOperandValue { get; }

        /// <summary>Upper bound of the FINAL result; see CONTRACT NOTE 1.</summary>
        public int MaxResultValue { get; }

        public int MinOperandCount { get; }

        public int MaxOperandCount { get; }

        public bool AllowParentheses { get; }

        /// <summary>
        /// 0 ⇒ no parentheses, 1 ⇒ one level (CL7-9), 2 ⇒ nested (CL10).
        /// Invariant: AllowParentheses == (MaxParenthesisDepth &gt; 0).
        /// </summary>
        public int MaxParenthesisDepth { get; }

        public bool AllowUnknownOperand { get; }

        /// <summary>
        /// How many UnknownNodes may appear in the tree (0 or 1 in the MVP).
        /// Invariant: AllowUnknownOperand == (UnknownOperandCount &gt; 0).
        /// </summary>
        public int UnknownOperandCount { get; }

        public bool AllowNegativeResults { get; }

        /// <summary>2..4 (core-domain.md 4.).</summary>
        public int AnswerOptionCount { get; }

        public float BaseThinkTimeSeconds { get; }

        /// <summary>
        /// Minimum difference between the two gate results of a
        /// ResourceOperationComparison (invariant I10; CL1-3: 8, CL4+: 4 —
        /// GAME_DESIGN 3.5/1.).
        /// </summary>
        public int MinGateGap { get; }

        public bool AutoAdaptationEnabled { get; }

        public DifficultyProfile(
            int challengeLevel,
            int mathComplexity,
            int timePressure,
            int combatDifficulty,
            IReadOnlyCollection<BinaryOperator> allowedOperators,
            IReadOnlyCollection<SkillTag> unlockedSkills,
            int minOperandValue,
            int maxOperandValue,
            int maxResultValue,
            int minOperandCount,
            int maxOperandCount,
            bool allowParentheses,
            int maxParenthesisDepth,
            bool allowUnknownOperand,
            int unknownOperandCount,
            bool allowNegativeResults,
            int answerOptionCount,
            float baseThinkTimeSeconds,
            int minGateGap,
            bool autoAdaptationEnabled)
        {
            RequireLevel(challengeLevel, nameof(challengeLevel));
            RequireLevel(mathComplexity, nameof(mathComplexity));
            RequireLevel(timePressure, nameof(timePressure));
            RequireLevel(combatDifficulty, nameof(combatDifficulty));

            if (allowedOperators == null)
            {
                throw new ArgumentNullException(nameof(allowedOperators));
            }

            if (allowedOperators.Count == 0)
            {
                throw new ArgumentException("AllowedOperators nem lehet üres.", nameof(allowedOperators));
            }

            if (unlockedSkills == null)
            {
                throw new ArgumentNullException(nameof(unlockedSkills));
            }

            if (minOperandValue < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(minOperandValue), minOperandValue, "MinOperandValue nem lehet negatív.");
            }

            if (maxOperandValue <= minOperandValue)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxOperandValue), maxOperandValue, "MaxOperandValue > MinOperandValue kötelező.");
            }

            if (maxResultValue < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxResultValue), maxResultValue, "MaxResultValue > 0 kötelező.");
            }

            if (minOperandCount < 2)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(minOperandCount), minOperandCount, "MinOperandCount >= 2 kötelező.");
            }

            if (maxOperandCount < minOperandCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxOperandCount), maxOperandCount, "MaxOperandCount >= MinOperandCount kötelező.");
            }

            if (maxOperandCount > MaxSupportedOperandCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxOperandCount), maxOperandCount,
                    $"MaxOperandCount <= {MaxSupportedOperandCount} kötelező (MVP).");
            }

            if (maxParenthesisDepth < 0 || maxParenthesisDepth > MaxSupportedParenthesisDepth)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxParenthesisDepth), maxParenthesisDepth,
                    $"MaxParenthesisDepth tartománya [0, {MaxSupportedParenthesisDepth}].");
            }

            if (allowParentheses != maxParenthesisDepth > 0)
            {
                throw new ArgumentException(
                    "AllowParentheses == (MaxParenthesisDepth > 0) invariáns sérül (core-domain.md 4.).",
                    nameof(allowParentheses));
            }

            if (unknownOperandCount < 0 || unknownOperandCount > MaxSupportedUnknownOperandCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(unknownOperandCount), unknownOperandCount,
                    $"UnknownOperandCount tartománya [0, {MaxSupportedUnknownOperandCount}] (MVP).");
            }

            if (allowUnknownOperand != unknownOperandCount > 0)
            {
                throw new ArgumentException(
                    "AllowUnknownOperand == (UnknownOperandCount > 0) invariáns sérül (core-domain.md 4.).",
                    nameof(allowUnknownOperand));
            }

            if (answerOptionCount < MinAnswerOptionCount || answerOptionCount > MaxAnswerOptionCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(answerOptionCount), answerOptionCount,
                    $"AnswerOptionCount tartománya [{MinAnswerOptionCount}, {MaxAnswerOptionCount}].");
            }

            if (float.IsNaN(baseThinkTimeSeconds) || float.IsInfinity(baseThinkTimeSeconds)
                || baseThinkTimeSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(baseThinkTimeSeconds), baseThinkTimeSeconds,
                    "BaseThinkTimeSeconds véges, pozitív szám kell legyen.");
            }

            if (minGateGap < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(minGateGap), minGateGap, "MinGateGap >= 1 kötelező (I10).");
            }

            ChallengeLevel = challengeLevel;
            MathComplexity = mathComplexity;
            TimePressure = timePressure;
            CombatDifficulty = combatDifficulty;
            // Defensive copies: an immutable profile must not change under the
            // engine's feet if the caller keeps mutating its own list.
            if (allowedOperators.Any(op => !Enum.IsDefined(typeof(BinaryOperator), op)))
            {
                throw new ArgumentException("AllowedOperators ismeretlen operátort tartalmaz.", nameof(allowedOperators));
            }

            if (unlockedSkills.Any(skill => !Enum.IsDefined(typeof(SkillTag), skill)))
            {
                throw new ArgumentException("UnlockedSkills ismeretlen készséget tartalmaz.", nameof(unlockedSkills));
            }

            AllowedOperators = Array.AsReadOnly(allowedOperators.Distinct().ToArray());
            UnlockedSkills = Array.AsReadOnly(unlockedSkills.Distinct().ToArray());
            MinOperandValue = minOperandValue;
            MaxOperandValue = maxOperandValue;
            MaxResultValue = maxResultValue;
            MinOperandCount = minOperandCount;
            MaxOperandCount = maxOperandCount;
            AllowParentheses = allowParentheses;
            MaxParenthesisDepth = maxParenthesisDepth;
            AllowUnknownOperand = allowUnknownOperand;
            UnknownOperandCount = unknownOperandCount;
            AllowNegativeResults = allowNegativeResults;
            AnswerOptionCount = answerOptionCount;
            BaseThinkTimeSeconds = baseThinkTimeSeconds;
            MinGateGap = minGateGap;
            AutoAdaptationEnabled = autoAdaptationEnabled;
        }

        /// <summary>MVP ceiling for MaxOperandCount (core-domain.md 4.).</summary>
        public const int MaxSupportedOperandCount = 5;

        /// <summary>CL10's nested-parentheses ceiling (core-domain.md 4.).</summary>
        public const int MaxSupportedParenthesisDepth = 2;

        /// <summary>MVP ceiling for UnknownOperandCount (core-domain.md 4.).</summary>
        public const int MaxSupportedUnknownOperandCount = 1;

        public const int MinAnswerOptionCount = 2;

        public const int MaxAnswerOptionCount = 4;

        public bool IsOperatorAllowed(BinaryOperator op) => AllowedOperators.Contains(op);

        /// <summary>
        /// Whether the generator may use this skill at all (the
        /// SKILL_NOT_UNLOCKED validator code, math-engine.md 5.).
        /// </summary>
        public bool IsSkillUnlocked(SkillTag skill) => UnlockedSkills.Contains(skill);

        /// <summary>
        /// Returns a copy with the three dimensions and the challenge level
        /// replaced — the shape IAdaptiveDifficultyService.Recalculate needs
        /// (game-logic.md 10.) without having to restate all 20 arguments.
        /// Everything else (the configured per-level math content) is unchanged,
        /// because that content comes from the table, not from adaptation.
        /// </summary>
        public DifficultyProfile WithDimensions(
            int challengeLevel, int mathComplexity, int timePressure, int combatDifficulty)
        {
            return new DifficultyProfile(
                challengeLevel,
                mathComplexity,
                timePressure,
                combatDifficulty,
                AllowedOperators,
                UnlockedSkills,
                MinOperandValue,
                MaxOperandValue,
                MaxResultValue,
                MinOperandCount,
                MaxOperandCount,
                AllowParentheses,
                MaxParenthesisDepth,
                AllowUnknownOperand,
                UnknownOperandCount,
                AllowNegativeResults,
                AnswerOptionCount,
                BaseThinkTimeSeconds,
                MinGateGap,
                AutoAdaptationEnabled);
        }

        /// <summary>
        /// Returns a copy whose UnlockedSkills is narrowed to the skills the
        /// world has introduced and the child has reached SkillMasteryLevel.Introduced on
        /// (spec 7.2/7.4, ADR-010). Narrowing only — a skill absent from the
        /// table's level content is never added by this call, so a rising
        /// global level can never unlock an untaught operation.
        ///
        /// The world set is required: SkillProfile.GetMasteryLevel cannot return Locked yet
        /// (its documented F1-F3 fallback reports an unattempted skill as
        /// Introduced). A higher table level alone must therefore never be the
        /// source of the introduced-world set; it comes from world progression.
        /// The prerequisite-based Locked decision is F4-02's.
        /// </summary>
        public DifficultyProfile WithUnlockedSkillsFrom(
            SkillProfile skills, IReadOnlyCollection<SkillTag> introducedWorldSkills)
        {
            if (skills == null)
            {
                throw new ArgumentNullException(nameof(skills));
            }

            if (introducedWorldSkills == null)
            {
                throw new ArgumentNullException(nameof(introducedWorldSkills));
            }

            SkillTag[] allowed = UnlockedSkills
                .Where(tag => introducedWorldSkills.Contains(tag)
                    && skills.GetMasteryLevel(tag) >= SkillMasteryLevel.Introduced)
                .ToArray();

            return new DifficultyProfile(
                ChallengeLevel,
                MathComplexity,
                TimePressure,
                CombatDifficulty,
                AllowedOperators,
                allowed,
                MinOperandValue,
                MaxOperandValue,
                MaxResultValue,
                MinOperandCount,
                MaxOperandCount,
                AllowParentheses,
                MaxParenthesisDepth,
                AllowUnknownOperand,
                UnknownOperandCount,
                AllowNegativeResults,
                AnswerOptionCount,
                BaseThinkTimeSeconds,
                MinGateGap,
                AutoAdaptationEnabled);
        }

        private static void RequireLevel(int value, string parameterName)
        {
            if (value < 1 || value > 10)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName, value, $"{parameterName} tartománya [1, 10] (core-domain.md 4.).");
            }
        }
    }
}
