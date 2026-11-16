using System;
using System.Collections.Generic;
using MathHorde.Core.Progress;
using NUnit.Framework;

namespace MathHorde.Tests.EditMode.Progress
{
    [TestFixture]
    public class DifficultyAdjustmentTests
    {
        [Test]
        public void ValidValues_ConstructsSuccessfully()
        {
            var metrics = new Dictionary<string, double> { ["consecutiveFailures"] = 2 };

            var adjustment = new DifficultyAdjustment(
                previousLevel: 4,
                newLevel: 3,
                reason: "TWO_CONSECUTIVE_FAILURES",
                timestamp: DateTimeOffset.UtcNow,
                triggerMetrics: metrics);

            Assert.That(adjustment.PreviousLevel, Is.EqualTo(4));
            Assert.That(adjustment.NewLevel, Is.EqualTo(3));
            Assert.That(adjustment.TriggerMetrics["consecutiveFailures"], Is.EqualTo(2));
        }

        [Test]
        public void NullTriggerMetrics_DefaultsToEmpty()
        {
            var adjustment = new DifficultyAdjustment(1, 2, "LEVEL_PASSED_FIRST_TRY", DateTimeOffset.UtcNow, null);

            Assert.That(adjustment.TriggerMetrics.Count, Is.EqualTo(0));
        }

        [TestCase(0)]
        [TestCase(11)]
        public void OutOfRangePreviousLevel_Throws(int level)
        {
            Assert.That(
                () => new DifficultyAdjustment(level, 5, "x", DateTimeOffset.UtcNow, null),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void BlankReason_Throws(string reason)
        {
            Assert.That(
                () => new DifficultyAdjustment(1, 2, reason, DateTimeOffset.UtcNow, null),
                Throws.ArgumentException);
        }
    }
}
