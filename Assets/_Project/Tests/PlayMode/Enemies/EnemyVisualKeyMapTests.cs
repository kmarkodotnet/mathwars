// EnemyVisualKeyMapTests.cs
// F1-14 — proves the EnemyId -> visual-archetype-key resolution used to pick
// which pooled EnemyView represents an enemy (math-horde.md 3.1: ellenségek
// vizuálisan jól elkülöníthetők típusonként). Lives under Tests/PlayMode
// because the tested type is in the Presentation assembly (see
// ObjectPoolTests.cs header for why EditMode/dotnet test cannot reference it).
using System.Collections.Generic;
using NUnit.Framework;
using MathHorde.Presentation.Enemies;

namespace MathHorde.Tests.PlayMode.Enemies
{
    [TestFixture]
    public class EnemyVisualKeyMapTests
    {
        private static EnemyVisualKeyMap BuildFiveArchetypeMap()
        {
            var entries = new Dictionary<string, string>
            {
                ["basic_bot"] = "basic",
                ["fast_bot"] = "fast",
                ["armored_bot"] = "armored",
                ["boss_bot"] = "boss",
                ["swarmlet_bot"] = "swarmlet",
            };
            return new EnemyVisualKeyMap(entries, fallbackVisualKey: "basic");
        }

        [TestCase("basic_bot", "basic")]
        [TestCase("fast_bot", "fast")]
        [TestCase("armored_bot", "armored")]
        [TestCase("boss_bot", "boss")]
        [TestCase("swarmlet_bot", "swarmlet")]
        public void Resolve_KnownEnemyId_ReturnsItsOwnVisualKey(string enemyId, string expectedKey)
        {
            EnemyVisualKeyMap map = BuildFiveArchetypeMap();

            Assert.That(map.Resolve(enemyId), Is.EqualTo(expectedKey));
        }

        [Test]
        public void Resolve_UnknownEnemyId_FallsBackInsteadOfRenderingNothing()
        {
            EnemyVisualKeyMap map = BuildFiveArchetypeMap();

            Assert.That(map.Resolve("never_configured_enemy"), Is.EqualTo("basic"));
        }

        [TestCase(null)]
        [TestCase("")]
        public void Resolve_NullOrEmptyEnemyId_FallsBack(string enemyId)
        {
            EnemyVisualKeyMap map = BuildFiveArchetypeMap();

            Assert.That(map.Resolve(enemyId), Is.EqualTo("basic"));
        }

        [Test]
        public void AllVisualKeys_ContainsEveryDistinctKeyPlusFallback()
        {
            EnemyVisualKeyMap map = BuildFiveArchetypeMap();

            var keys = map.AllVisualKeys;

            Assert.That(keys.Count, Is.EqualTo(5));
            Assert.That(keys, Contains.Item("basic"));
            Assert.That(keys, Contains.Item("fast"));
            Assert.That(keys, Contains.Item("armored"));
            Assert.That(keys, Contains.Item("boss"));
            Assert.That(keys, Contains.Item("swarmlet"));
        }

        [Test]
        public void Constructor_RejectsEmptyFallbackKey()
        {
            Assert.That(
                () => new EnemyVisualKeyMap(new Dictionary<string, string>(), fallbackVisualKey: ""),
                Throws.ArgumentException);
        }
    }
}
