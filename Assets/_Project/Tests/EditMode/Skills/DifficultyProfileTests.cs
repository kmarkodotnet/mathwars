// DifficultyProfileTests.cs
// F2-10 — Core.Skills.DifficultyProfile (contract core-domain.md 4.). EditMode,
// so the mirror dotnet solution runs them too (ADR-003): the type is
// Unity-free by construction.
using System;
using System.Collections.Generic;
using MathHorde.Core.Expressions;
using MathHorde.Core.Problems;
using MathHorde.Core.Skills;
using NUnit.Framework;

namespace MathHorde.Tests.EditMode.Skills
{
    [TestFixture]
    public class DifficultyProfileTests
    {
        private static DifficultyProfile Create(
            int challengeLevel = 1,
            int mathComplexity = 1,
            int timePressure = 1,
            int combatDifficulty = 1,
            IReadOnlyCollection<BinaryOperator> allowedOperators = null,
            IReadOnlyCollection<SkillTag> unlockedSkills = null,
            int minOperandValue = 1,
            int maxOperandValue = 10,
            int maxResultValue = 10,
            int minOperandCount = 2,
            int maxOperandCount = 2,
            bool allowParentheses = false,
            int maxParenthesisDepth = 0,
            bool allowUnknownOperand = false,
            int unknownOperandCount = 0,
            bool allowNegativeResults = false,
            int answerOptionCount = 2,
            float baseThinkTimeSeconds = 14.0f,
            int minGateGap = 8,
            bool autoAdaptationEnabled = true)
        {
            return new DifficultyProfile(
                challengeLevel,
                mathComplexity,
                timePressure,
                combatDifficulty,
                allowedOperators ?? new[] { BinaryOperator.Addition, BinaryOperator.Subtraction },
                unlockedSkills ?? new[] { SkillTag.Addition, SkillTag.Subtraction },
                minOperandValue,
                maxOperandValue,
                maxResultValue,
                minOperandCount,
                maxOperandCount,
                allowParentheses,
                maxParenthesisDepth,
                allowUnknownOperand,
                unknownOperandCount,
                allowNegativeResults,
                answerOptionCount,
                baseThinkTimeSeconds,
                minGateGap,
                autoAdaptationEnabled);
        }

        [Test]
        public void Constructor_WithValidLevelOneValues_ExposesEveryField()
        {
            DifficultyProfile profile = Create(
                challengeLevel: 7,
                mathComplexity: 7,
                timePressure: 6,
                combatDifficulty: 5,
                maxOperandValue: 50,
                maxResultValue: 200,
                minOperandCount: 3,
                maxOperandCount: 3,
                allowParentheses: true,
                maxParenthesisDepth: 1,
                answerOptionCount: 4,
                baseThinkTimeSeconds: 7.0f,
                minGateGap: 4,
                autoAdaptationEnabled: false);

            Assert.That(profile.ChallengeLevel, Is.EqualTo(7));
            Assert.That(profile.MathComplexity, Is.EqualTo(7));
            Assert.That(profile.TimePressure, Is.EqualTo(6));
            Assert.That(profile.CombatDifficulty, Is.EqualTo(5));
            Assert.That(profile.MinOperandValue, Is.EqualTo(1));
            Assert.That(profile.MaxOperandValue, Is.EqualTo(50));
            Assert.That(profile.MaxResultValue, Is.EqualTo(200));
            Assert.That(profile.MinOperandCount, Is.EqualTo(3));
            Assert.That(profile.MaxOperandCount, Is.EqualTo(3));
            Assert.That(profile.AllowParentheses, Is.True);
            Assert.That(profile.MaxParenthesisDepth, Is.EqualTo(1));
            Assert.That(profile.AllowUnknownOperand, Is.False);
            Assert.That(profile.UnknownOperandCount, Is.EqualTo(0));
            Assert.That(profile.AllowNegativeResults, Is.False);
            Assert.That(profile.AnswerOptionCount, Is.EqualTo(4));
            Assert.That(profile.BaseThinkTimeSeconds, Is.EqualTo(7.0f));
            Assert.That(profile.MinGateGap, Is.EqualTo(4));
            Assert.That(profile.AutoAdaptationEnabled, Is.False);
        }

        [Test]
        public void Constructor_CopiesTheCollections_SoLaterMutationCannotLeakIn()
        {
            var operators = new List<BinaryOperator> { BinaryOperator.Addition };
            var skills = new List<SkillTag> { SkillTag.Addition };

            DifficultyProfile profile = Create(allowedOperators: operators, unlockedSkills: skills);

            operators.Add(BinaryOperator.Multiplication);
            skills.Add(SkillTag.Multiplication);

            Assert.That(profile.AllowedOperators.Count, Is.EqualTo(1));
            Assert.That(profile.UnlockedSkills.Count, Is.EqualTo(1));
            Assert.That(profile.IsOperatorAllowed(BinaryOperator.Multiplication), Is.False);
            Assert.That(profile.IsSkillUnlocked(SkillTag.Multiplication), Is.False);
        }

        [Test]
        public void Constructor_DeduplicatesTheCollections()
        {
            DifficultyProfile profile = Create(
                allowedOperators: new[] { BinaryOperator.Addition, BinaryOperator.Addition },
                unlockedSkills: new[] { SkillTag.Addition, SkillTag.Addition });

            Assert.That(profile.AllowedOperators.Count, Is.EqualTo(1));
            Assert.That(profile.UnlockedSkills.Count, Is.EqualTo(1));
        }

        [Test]
        public void Collections_CannotBeMutatedThroughADowncast()
        {
            DifficultyProfile profile = Create();

            Assert.That(
                () => ((IList<BinaryOperator>)profile.AllowedOperators)[0] = BinaryOperator.Multiplication,
                Throws.TypeOf<NotSupportedException>());
            Assert.That(
                () => ((IList<SkillTag>)profile.UnlockedSkills)[0] = SkillTag.Multiplication,
                Throws.TypeOf<NotSupportedException>());
            Assert.That(profile.IsOperatorAllowed(BinaryOperator.Multiplication), Is.False);
            Assert.That(profile.IsSkillUnlocked(SkillTag.Multiplication), Is.False);
        }

        [Test]
        public void Constructor_RejectsUndefinedOperatorAndSkillValues()
        {
            Assert.That(
                () => Create(allowedOperators: new[] { (BinaryOperator)999 }),
                Throws.TypeOf<ArgumentException>());
            Assert.That(
                () => Create(unlockedSkills: new[] { (SkillTag)999 }),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void IsOperatorAllowed_AndIsSkillUnlocked_FollowTheConfiguredSets()
        {
            DifficultyProfile profile = Create(
                allowedOperators: new[] { BinaryOperator.Addition, BinaryOperator.Division },
                unlockedSkills: new[] { SkillTag.Addition, SkillTag.Division });

            Assert.That(profile.IsOperatorAllowed(BinaryOperator.Addition), Is.True);
            Assert.That(profile.IsOperatorAllowed(BinaryOperator.Division), Is.True);
            Assert.That(profile.IsOperatorAllowed(BinaryOperator.Modulo), Is.False);
            Assert.That(profile.IsSkillUnlocked(SkillTag.Division), Is.True);
            Assert.That(profile.IsSkillUnlocked(SkillTag.Parentheses), Is.False);
        }

        [TestCase(0)]
        [TestCase(11)]
        [TestCase(-1)]
        public void Constructor_WithChallengeLevelOutsideOneToTen_Throws(int challengeLevel)
        {
            Assert.That(
                () => Create(challengeLevel: challengeLevel),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [TestCase(0)]
        [TestCase(11)]
        public void Constructor_WithMathComplexityOutsideOneToTen_Throws(int mathComplexity)
        {
            Assert.That(
                () => Create(mathComplexity: mathComplexity),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [TestCase(0)]
        [TestCase(11)]
        public void Constructor_WithTimePressureOutsideOneToTen_Throws(int timePressure)
        {
            Assert.That(
                () => Create(timePressure: timePressure),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [TestCase(0)]
        [TestCase(11)]
        public void Constructor_WithCombatDifficultyOutsideOneToTen_Throws(int combatDifficulty)
        {
            Assert.That(
                () => Create(combatDifficulty: combatDifficulty),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void Constructor_WithNullAllowedOperators_Throws()
        {
            Assert.That(
                () => new DifficultyProfile(
                    1, 1, 1, 1, null, new[] { SkillTag.Addition },
                    1, 10, 10, 2, 2, false, 0, false, 0, false, 2, 14.0f, 8, true),
                Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void Constructor_WithEmptyAllowedOperators_Throws()
        {
            Assert.That(
                () => Create(allowedOperators: Array.Empty<BinaryOperator>()),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void Constructor_WithNullUnlockedSkills_Throws()
        {
            Assert.That(
                () => new DifficultyProfile(
                    1, 1, 1, 1, new[] { BinaryOperator.Addition }, null,
                    1, 10, 10, 2, 2, false, 0, false, 0, false, 2, 14.0f, 8, true),
                Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void Constructor_WithEmptyUnlockedSkills_IsAllowed()
        {
            // A child who has not been introduced to ANY skill of this level is a
            // legitimate state (the generator then has nothing to offer and the
            // SKILL_NOT_UNLOCKED path handles it, math-engine.md 5.) — unlike an
            // empty operator set, which is a misconfigured table.
            DifficultyProfile profile = Create(unlockedSkills: Array.Empty<SkillTag>());

            Assert.That(profile.UnlockedSkills.Count, Is.EqualTo(0));
        }

        [Test]
        public void Constructor_WithNegativeMinOperandValue_Throws()
        {
            Assert.That(
                () => Create(minOperandValue: -1),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void Constructor_WithMaxOperandValueNotAboveMin_Throws()
        {
            Assert.That(
                () => Create(minOperandValue: 10, maxOperandValue: 10),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void Constructor_WithMaxResultValueBelowMaxOperandValue_PreservesIndependentBounds()
        {
            DifficultyProfile profile = Create(maxOperandValue: 50, maxResultValue: 20);

            Assert.That(profile.MaxOperandValue, Is.EqualTo(50));
            Assert.That(profile.MaxResultValue, Is.EqualTo(20));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void Constructor_WithNonPositiveMaxResultValue_Throws(int maxResultValue)
        {
            Assert.That(
                () => Create(maxResultValue: maxResultValue),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void Constructor_WithMinOperandCountBelowTwo_Throws()
        {
            Assert.That(
                () => Create(minOperandCount: 1, maxOperandCount: 2),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void Constructor_WithMaxOperandCountBelowMin_Throws()
        {
            Assert.That(
                () => Create(minOperandCount: 3, maxOperandCount: 2),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void Constructor_WithMaxOperandCountAboveMvpCeiling_Throws()
        {
            Assert.That(
                () => Create(minOperandCount: 2, maxOperandCount: DifficultyProfile.MaxSupportedOperandCount + 1),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [TestCase(-1)]
        [TestCase(3)]
        public void Constructor_WithParenthesisDepthOutsideRange_Throws(int depth)
        {
            Assert.That(
                () => Create(allowParentheses: depth > 0, maxParenthesisDepth: depth),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [TestCase(true, 0)]
        [TestCase(false, 1)]
        public void Constructor_WithParenthesisFlagAndDepthDisagreeing_Throws(bool allow, int depth)
        {
            Assert.That(
                () => Create(allowParentheses: allow, maxParenthesisDepth: depth),
                Throws.TypeOf<ArgumentException>());
        }

        [TestCase(-1)]
        [TestCase(2)]
        public void Constructor_WithUnknownOperandCountOutsideMvpRange_Throws(int count)
        {
            Assert.That(
                () => Create(allowUnknownOperand: count > 0, unknownOperandCount: count),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [TestCase(true, 0)]
        [TestCase(false, 1)]
        public void Constructor_WithUnknownFlagAndCountDisagreeing_Throws(bool allow, int count)
        {
            Assert.That(
                () => Create(allowUnknownOperand: allow, unknownOperandCount: count),
                Throws.TypeOf<ArgumentException>());
        }

        [TestCase(1)]
        [TestCase(5)]
        public void Constructor_WithAnswerOptionCountOutsideTwoToFour_Throws(int count)
        {
            Assert.That(
                () => Create(answerOptionCount: count),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [TestCase(0f)]
        [TestCase(-0.5f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void Constructor_WithNonPositiveThinkTime_Throws(float thinkTime)
        {
            // THINK_TIME_NON_POSITIVE (math-engine.md 5.).
            Assert.That(
                () => Create(baseThinkTimeSeconds: thinkTime),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void Constructor_WithMinGateGapBelowOne_Throws()
        {
            Assert.That(
                () => Create(minGateGap: 0),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void WithDimensions_ReplacesOnlyTheFourLevelNumbers()
        {
            DifficultyProfile profile = Create(
                challengeLevel: 5,
                mathComplexity: 5,
                timePressure: 5,
                combatDifficulty: 5,
                maxOperandValue: 100,
                maxResultValue: 100,
                answerOptionCount: 3,
                baseThinkTimeSeconds: 8.0f,
                minGateGap: 4);

            DifficultyProfile adjusted = profile.WithDimensions(6, 6, 5, 6);

            Assert.That(adjusted.ChallengeLevel, Is.EqualTo(6));
            Assert.That(adjusted.MathComplexity, Is.EqualTo(6));
            Assert.That(adjusted.TimePressure, Is.EqualTo(5));
            Assert.That(adjusted.CombatDifficulty, Is.EqualTo(6));
            Assert.That(adjusted.MaxOperandValue, Is.EqualTo(100));
            Assert.That(adjusted.MaxResultValue, Is.EqualTo(100));
            Assert.That(adjusted.AnswerOptionCount, Is.EqualTo(3));
            Assert.That(adjusted.BaseThinkTimeSeconds, Is.EqualTo(8.0f));
            Assert.That(adjusted.MinGateGap, Is.EqualTo(4));
            Assert.That(adjusted.UnlockedSkills.Count, Is.EqualTo(profile.UnlockedSkills.Count));
            // The original is untouched — the profile is immutable.
            Assert.That(profile.ChallengeLevel, Is.EqualTo(5));
        }

        [Test]
        public void WithDimensions_StillEnforcesTheOneToTenRange()
        {
            DifficultyProfile profile = Create();

            Assert.That(
                () => profile.WithDimensions(11, 1, 1, 1),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void WithUnlockedSkillsFrom_NeverWidensTheSet()
        {
            DifficultyProfile profile = Create(
                unlockedSkills: new[] { SkillTag.Addition, SkillTag.Subtraction });
            var skills = new SkillProfile(
                new Dictionary<SkillTag, SkillStatistics>(), DateTimeOffset.UtcNow);

            DifficultyProfile narrowed = profile.WithUnlockedSkillsFrom(
                skills, new[] { SkillTag.Addition, SkillTag.Subtraction, SkillTag.Multiplication });

            // An unattempted skill reports Introduced (ADR-010's documented F1-F3
            // fallback in SkillProfile.GetMasteryLevel), so nothing is removed here —
            // and crucially, Multiplication is NOT added just because the child could
            // technically do it: a rising level never unlocks untaught content
            // (spec 7.2/7.4).
            Assert.That(narrowed.UnlockedSkills.Count, Is.EqualTo(2));
            Assert.That(narrowed.IsSkillUnlocked(SkillTag.Multiplication), Is.False);
        }

        [Test]
        public void WithUnlockedSkillsFrom_WithNullProfile_Throws()
        {
            DifficultyProfile profile = Create();

            Assert.That(
                () => profile.WithUnlockedSkillsFrom(null, Array.Empty<SkillTag>()),
                Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void WithUnlockedSkillsFrom_RequiresWorldIntroductionsDespiteSkillFallback()
        {
            DifficultyProfile profile = Create(
                unlockedSkills: new[] { SkillTag.Addition, SkillTag.Multiplication });
            var skills = new SkillProfile(
                new Dictionary<SkillTag, SkillStatistics>(), DateTimeOffset.UtcNow);

            DifficultyProfile narrowed = profile.WithUnlockedSkillsFrom(skills, new[] { SkillTag.Addition });

            Assert.That(narrowed.UnlockedSkills.Count, Is.EqualTo(1));
            Assert.That(narrowed.IsSkillUnlocked(SkillTag.Multiplication), Is.False);
            Assert.That(profile.IsSkillUnlocked(SkillTag.Multiplication), Is.True);
            Assert.That(profile.WithUnlockedSkillsFrom(skills, Array.Empty<SkillTag>()).UnlockedSkills.Count, Is.EqualTo(0));
            Assert.That(() => profile.WithUnlockedSkillsFrom(skills, null), Throws.TypeOf<ArgumentNullException>());
        }
    }
}
