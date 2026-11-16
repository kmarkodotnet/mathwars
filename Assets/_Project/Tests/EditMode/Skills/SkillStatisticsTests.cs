using System;
using System.Collections.Generic;
using MathHorde.Core.Problems;
using MathHorde.Core.Skills;
using NUnit.Framework;

namespace MathHorde.Tests.EditMode.Skills
{
    [TestFixture]
    public class SkillStatisticsTests
    {
        [Test]
        public void Accuracy_IsDerivedFromCorrectOverAttempts()
        {
            var stats = new SkillStatistics(
                SkillTag.Multiplication, difficultyLevel: 3, attempts: 10, correctAnswers: 7,
                averageResponseTimeSeconds: 2f, medianResponseTimeSeconds: 1.8f,
                lastPracticedAt: DateTimeOffset.UtcNow, masteryEstimate: 0.6f, confidence: 0.5f,
                typicalErrors: null);

            Assert.That(stats.Accuracy, Is.EqualTo(0.7f).Within(0.0001f));
        }

        [Test]
        public void ZeroAttempts_AccuracyIsZero()
        {
            var stats = new SkillStatistics(
                SkillTag.Addition, 1, 0, 0, 0f, 0f, null, 0f, 0f, null);

            Assert.That(stats.Accuracy, Is.EqualTo(0f));
        }

        [Test]
        public void NullTypicalErrors_DefaultsToEmpty()
        {
            var stats = new SkillStatistics(SkillTag.Addition, 1, 0, 0, 0f, 0f, null, 0f, 0f, null);

            Assert.That(stats.TypicalErrors.Count, Is.EqualTo(0));
        }

        [Test]
        public void TypicalErrors_RoundTripsGivenDictionary()
        {
            var errors = new Dictionary<ErrorCategory, int> { [ErrorCategory.CarryError] = 2 };

            var stats = new SkillStatistics(SkillTag.Addition, 1, 5, 3, 0f, 0f, null, 0.3f, 0.2f, errors);

            Assert.That(stats.TypicalErrors[ErrorCategory.CarryError], Is.EqualTo(2));
        }

        [Test]
        public void CorrectAnswersGreaterThanAttempts_Throws()
        {
            Assert.That(
                () => new SkillStatistics(SkillTag.Addition, 1, attempts: 2, correctAnswers: 3, 0f, 0f, null, 0f, 0f, null),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [TestCase(-0.1f)]
        [TestCase(1.1f)]
        public void OutOfRangeMasteryEstimate_Throws(float mastery)
        {
            Assert.That(
                () => new SkillStatistics(SkillTag.Addition, 1, 1, 1, 0f, 0f, null, mastery, 0f, null),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }
    }
}
