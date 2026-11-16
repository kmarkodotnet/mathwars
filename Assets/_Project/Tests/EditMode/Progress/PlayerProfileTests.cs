using System;
using MathHorde.Core.Progress;
using NUnit.Framework;

namespace MathHorde.Tests.EditMode.Progress
{
    [TestFixture]
    public class PlayerProfileTests
    {
        [Test]
        public void ValidValues_ConstructsSuccessfully()
        {
            var profile = new PlayerProfile(
                PlayerProfileId.New(),
                displayName: null,
                createdAt: DateTimeOffset.UtcNow,
                currentChallengeLevel: 3,
                highestChallengeLevel: 5,
                unlockedWorlds: new[] { new WorldId("w1") },
                completedLevels: new[] { new LevelId("w1-l01") });

            Assert.That(profile.DisplayName, Is.Null);
            Assert.That(profile.CurrentChallengeLevel, Is.EqualTo(3));
            Assert.That(profile.HighestChallengeLevel, Is.EqualTo(5));
            Assert.That(profile.UnlockedWorlds.Count, Is.EqualTo(1));
            Assert.That(profile.CompletedLevels.Count, Is.EqualTo(1));
        }

        [Test]
        public void NullCollections_DefaultToEmpty()
        {
            var profile = new PlayerProfile(
                PlayerProfileId.New(), "Kincső", DateTimeOffset.UtcNow, 1, 1, null, null);

            Assert.That(profile.UnlockedWorlds.Count, Is.EqualTo(0));
            Assert.That(profile.CompletedLevels.Count, Is.EqualTo(0));
        }

        [TestCase(0)]
        [TestCase(11)]
        public void OutOfRangeCurrentChallengeLevel_Throws(int level)
        {
            Assert.That(
                () => new PlayerProfile(PlayerProfileId.New(), null, DateTimeOffset.UtcNow, level, 10, null, null),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void HighestBelowCurrent_Throws()
        {
            Assert.That(
                () => new PlayerProfile(PlayerProfileId.New(), null, DateTimeOffset.UtcNow, 5, 3, null, null),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void PlayerProfileId_New_ProducesDistinctValues()
        {
            Assert.That(PlayerProfileId.New(), Is.Not.EqualTo(PlayerProfileId.New()));
        }
    }
}
