using System;
using MathHorde.Core.Problems;
using MathHorde.Core.Resources;
using NUnit.Framework;

namespace MathHorde.Tests.EditMode.Problems
{
    // RewardSpec (MathHorde.Core.Problems, contract core-domain.md 3.).

    [TestFixture]
    public class RewardSpecTests
    {
        [Test]
        public void Constructor_AnswerLaneSelection_FlatAmount_SetsProperties()
        {
            var reward = new RewardSpec(ResourceType.Ammo, 30, useSnapshotDelta: false);

            Assert.That(reward.Resource, Is.EqualTo(ResourceType.Ammo));
            Assert.That(reward.FlatAmount, Is.EqualTo(30));
            Assert.That(reward.UseSnapshotDelta, Is.False);
            Assert.That(reward.IncorrectAmount, Is.EqualTo(0));
        }

        [Test]
        public void Constructor_ResourceOperationComparison_UsesSnapshotDeltaWithZeroFlatAmount()
        {
            var reward = new RewardSpec(ResourceType.Shield, 0, useSnapshotDelta: true);

            Assert.That(reward.UseSnapshotDelta, Is.True);
            Assert.That(reward.FlatAmount, Is.EqualTo(0));
        }

        [Test]
        public void Constructor_UseSnapshotDeltaWithNonZeroFlatAmount_Throws()
        {
            Assert.That(
                () => new RewardSpec(ResourceType.Ammo, 25, useSnapshotDelta: true),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void Constructor_ExplicitIncorrectAmount_IsStored()
        {
            var reward = new RewardSpec(ResourceType.Health, 10, useSnapshotDelta: false, incorrectAmount: -5);

            Assert.That(reward.IncorrectAmount, Is.EqualTo(-5));
        }
    }
}
