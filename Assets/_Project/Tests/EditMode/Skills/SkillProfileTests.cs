using System;
using System.Collections.Generic;
using MathHorde.Core.Problems;
using MathHorde.Core.Skills;
using NUnit.Framework;

namespace MathHorde.Tests.EditMode.Skills
{
    // SkillProfile/SkillMasteryLevel/SkillMasteryThresholds (contract
    // core-domain.md 4., ADR-010, task F2-06) — see SkillProfile's own class
    // comment for why this type was created by F2-06 even though its full
    // design is ADR-010/F4-02's declared scope.
    [TestFixture]
    public class SkillProfileTests
    {
        private static SkillStatistics Stats(
            SkillTag tag, int attempts, int correct, float masteryEstimate, float confidence) =>
            new SkillStatistics(
                tag, difficultyLevel: 1, attempts: attempts, correctAnswers: correct,
                averageResponseTimeSeconds: 1f, medianResponseTimeSeconds: 1f,
                lastPracticedAt: null, masteryEstimate: masteryEstimate, confidence: confidence,
                typicalErrors: null);

        [Test]
        public void GetOrEmpty_UnknownTag_ReturnsZeroAttemptRecord()
        {
            var profile = new SkillProfile(new Dictionary<SkillTag, SkillStatistics>(), DateTimeOffset.UtcNow);

            var stats = profile.GetOrEmpty(SkillTag.Division);

            Assert.That(stats.SkillId, Is.EqualTo(SkillTag.Division));
            Assert.That(stats.Attempts, Is.EqualTo(0));
        }

        [Test]
        public void GetMasteryLevel_ZeroAttempts_IsIntroduced_NotLocked()
        {
            // ADR-010 F1-F3 fallback: "Attempts == 0 is ide tartozik [Introduced]".
            var profile = new SkillProfile(new Dictionary<SkillTag, SkillStatistics>(), DateTimeOffset.UtcNow);

            Assert.That(profile.GetMasteryLevel(SkillTag.Multiplication), Is.EqualTo(SkillMasteryLevel.Introduced));
        }

        [Test]
        public void GetMasteryLevel_HighEstimateLowAttempts_IsPracticedNotMastered()
        {
            // spec 8.: a single correct answer (or too few samples) must
            // never read as Mastered, even with a high raw estimate.
            var skills = new Dictionary<SkillTag, SkillStatistics>
            {
                [SkillTag.Addition] = Stats(SkillTag.Addition, attempts: 1, correct: 1, masteryEstimate: 1.0f, confidence: 1.0f)
            };
            var profile = new SkillProfile(skills, DateTimeOffset.UtcNow);

            Assert.That(profile.GetMasteryLevel(SkillTag.Addition), Is.Not.EqualTo(SkillMasteryLevel.Mastered));
        }

        [Test]
        public void GetMasteryLevel_MeetsMasteryAndConfidence_IsMastered()
        {
            var skills = new Dictionary<SkillTag, SkillStatistics>
            {
                [SkillTag.Addition] = Stats(SkillTag.Addition, attempts: 10, correct: 9, masteryEstimate: 0.85f, confidence: 0.7f)
            };
            var profile = new SkillProfile(skills, DateTimeOffset.UtcNow);

            Assert.That(profile.GetMasteryLevel(SkillTag.Addition), Is.EqualTo(SkillMasteryLevel.Mastered));
        }

        [Test]
        public void GetMasteryLevel_MeetsMasteryButNotConfidence_IsNotMastered()
        {
            var skills = new Dictionary<SkillTag, SkillStatistics>
            {
                [SkillTag.Addition] = Stats(SkillTag.Addition, attempts: 10, correct: 9, masteryEstimate: 0.9f, confidence: 0.3f)
            };
            var profile = new SkillProfile(skills, DateTimeOffset.UtcNow);

            Assert.That(profile.GetMasteryLevel(SkillTag.Addition), Is.Not.EqualTo(SkillMasteryLevel.Mastered));
        }

        [Test]
        public void GetMasteryLevel_MeetsPracticedBand_IsPracticed()
        {
            var skills = new Dictionary<SkillTag, SkillStatistics>
            {
                [SkillTag.Addition] = Stats(SkillTag.Addition, attempts: 4, correct: 2, masteryEstimate: 0.55f, confidence: 0.1f)
            };
            var profile = new SkillProfile(skills, DateTimeOffset.UtcNow);

            Assert.That(profile.GetMasteryLevel(SkillTag.Addition), Is.EqualTo(SkillMasteryLevel.Practiced));
        }

        [Test]
        public void GetMasteryLevel_BelowPracticedBand_IsIntroduced()
        {
            var skills = new Dictionary<SkillTag, SkillStatistics>
            {
                [SkillTag.Addition] = Stats(SkillTag.Addition, attempts: 2, correct: 1, masteryEstimate: 0.3f, confidence: 0.1f)
            };
            var profile = new SkillProfile(skills, DateTimeOffset.UtcNow);

            Assert.That(profile.GetMasteryLevel(SkillTag.Addition), Is.EqualTo(SkillMasteryLevel.Introduced));
        }

        [Test]
        public void MasteredSkills_OnlyListsMasteredOnes()
        {
            var skills = new Dictionary<SkillTag, SkillStatistics>
            {
                [SkillTag.Addition] = Stats(SkillTag.Addition, attempts: 10, correct: 9, masteryEstimate: 0.9f, confidence: 0.8f),
                [SkillTag.Subtraction] = Stats(SkillTag.Subtraction, attempts: 10, correct: 3, masteryEstimate: 0.3f, confidence: 0.8f)
            };
            var profile = new SkillProfile(skills, DateTimeOffset.UtcNow);

            Assert.That(profile.MasteredSkills.Count, Is.EqualTo(1));
            Assert.That(ContainsTag(profile.MasteredSkills, SkillTag.Addition), Is.True);
        }

        [Test]
        public void SkillsNeedingPractice_OrdersLowestMasteryFirstAndExcludesUnattempted()
        {
            var skills = new Dictionary<SkillTag, SkillStatistics>
            {
                [SkillTag.Addition] = Stats(SkillTag.Addition, attempts: 5, correct: 4, masteryEstimate: 0.7f, confidence: 0.2f),
                [SkillTag.Subtraction] = Stats(SkillTag.Subtraction, attempts: 5, correct: 1, masteryEstimate: 0.2f, confidence: 0.2f),
                [SkillTag.Multiplication] = Stats(SkillTag.Multiplication, attempts: 0, correct: 0, masteryEstimate: 0f, confidence: 0f)
            };
            var profile = new SkillProfile(skills, DateTimeOffset.UtcNow);

            var needingPractice = profile.SkillsNeedingPractice;

            Assert.That(needingPractice.Count, Is.EqualTo(2));
            Assert.That(needingPractice[0], Is.EqualTo(SkillTag.Subtraction));
            Assert.That(needingPractice[1], Is.EqualTo(SkillTag.Addition));
        }

        [Test]
        public void Thresholds_AreInjectable_NotHardcoded()
        {
            var custom = new SkillMasteryThresholds(masteryThreshold: 0.6f, practicedThreshold: 0.3f, minAttemptsForMastery: 4, masteryConfidenceMin: 0.1f);
            var skills = new Dictionary<SkillTag, SkillStatistics>
            {
                [SkillTag.Addition] = Stats(SkillTag.Addition, attempts: 4, correct: 3, masteryEstimate: 0.65f, confidence: 0.2f)
            };
            var profile = new SkillProfile(skills, DateTimeOffset.UtcNow, custom);

            Assert.That(profile.GetMasteryLevel(SkillTag.Addition), Is.EqualTo(SkillMasteryLevel.Mastered));
        }

        [Test]
        public void Thresholds_PracticedNotBelowMastery_Throws()
        {
            Assert.That(
                () => new SkillMasteryThresholds(masteryThreshold: 0.5f, practicedThreshold: 0.5f),
                Throws.TypeOf<ArgumentException>());
        }

        [TestCase(0.49f)]
        [TestCase(1.01f)]
        public void Thresholds_MasteryThresholdOutOfRange_Throws(float value)
        {
            Assert.That(
                () => new SkillMasteryThresholds(masteryThreshold: value, practicedThreshold: 0.2f),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        private static bool ContainsTag(IReadOnlyCollection<SkillTag> tags, SkillTag tag)
        {
            foreach (var item in tags)
            {
                if (item == tag)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
