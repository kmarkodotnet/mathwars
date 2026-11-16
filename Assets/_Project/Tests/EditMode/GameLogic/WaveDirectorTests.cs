using System.Collections.Generic;
using MathHorde.Core.Common;
using MathHorde.Core.Levels;
using MathHorde.GameLogic;
using MathHorde.Tests.EditMode.TestSupport;
using NUnit.Framework;

namespace MathHorde.Tests.EditMode.GameLogic
{
    // Contract game-logic.md 4. (spec 4.1): wave scheduling, SpawnCommand,
    // and the MaxWaveGapSeconds invariant ("két matematikai feladat között
    // sem állhat le tartósan a spawnolás").
    [TestFixture]
    public class WaveDirectorTests
    {
        [Test]
        public void Tick_PastSpawnTime_MovesEntryIntoPendingSpawns()
        {
            var waves = new List<WaveDefinition>
            {
                new WaveDefinition(0, startGameTime: 2.0, durationSeconds: 10.0, spawns: new[]
                {
                    new WaveSpawnEntry("basic_bot", count: 1, spawnIntervalSeconds: 1f, allowedLanes: new[] { new LaneIndex(0) })
                })
            };
            var level = CombatTestFixtures.BuildLevel(waves: waves);
            var director = new WaveDirector();
            director.Initialize(level, new DeterministicRandomSource(1));

            director.Tick(1.0);
            Assert.That(director.PendingSpawns, Is.Empty, "A spawn ideje (2.0) még nem jött el.");

            director.Tick(2.0);
            Assert.That(director.PendingSpawns.Count, Is.EqualTo(1));
            Assert.That(director.PendingSpawns[0].EnemyId, Is.EqualTo("basic_bot"));
            Assert.That(director.PendingSpawns[0].Lane.Value, Is.EqualTo(0));
        }

        [Test]
        public void ClearPendingSpawns_EmptiesTheList()
        {
            var waves = new List<WaveDefinition>
            {
                new WaveDefinition(0, startGameTime: 0.0, durationSeconds: 10.0, spawns: new[]
                {
                    new WaveSpawnEntry("basic_bot", count: 1, spawnIntervalSeconds: 1f)
                })
            };
            var level = CombatTestFixtures.BuildLevel(waves: waves);
            var director = new WaveDirector();
            director.Initialize(level, new DeterministicRandomSource(1));

            director.Tick(0.0);
            Assert.That(director.PendingSpawns.Count, Is.EqualTo(1));

            director.ClearPendingSpawns();
            Assert.That(director.PendingSpawns, Is.Empty);
        }

        [Test]
        public void Tick_DoesNotDispatchTheSameSpawnTwice()
        {
            var waves = new List<WaveDefinition>
            {
                new WaveDefinition(0, startGameTime: 0.0, durationSeconds: 10.0, spawns: new[]
                {
                    new WaveSpawnEntry("basic_bot", count: 1, spawnIntervalSeconds: 1f)
                })
            };
            var level = CombatTestFixtures.BuildLevel(waves: waves);
            var director = new WaveDirector();
            director.Initialize(level, new DeterministicRandomSource(1));

            director.Tick(0.0);
            director.ClearPendingSpawns();
            director.Tick(5.0);

            Assert.That(director.PendingSpawns, Is.Empty, "Már kiadott spawn nem adódik ki másodszor.");
        }

        [Test]
        public void MaxWaveGapSeconds_IsNeverExceededAcrossTheWholeLevel()
        {
            // Two waves with a deliberately huge silent gap between them —
            // the contract requires WaveDirector to never let this stand.
            var waves = new List<WaveDefinition>
            {
                new WaveDefinition(0, startGameTime: 0.0, durationSeconds: 5.0, spawns: new[]
                {
                    new WaveSpawnEntry("basic_bot", count: 1, spawnIntervalSeconds: 1f, allowedLanes: new[] { new LaneIndex(0) })
                }),
                new WaveDefinition(1, startGameTime: 30.0, durationSeconds: 5.0, spawns: new[]
                {
                    new WaveSpawnEntry("fast_bot", count: 1, spawnIntervalSeconds: 1f, allowedLanes: new[] { new LaneIndex(1) })
                })
            };
            var level = CombatTestFixtures.BuildLevel(waves: waves);
            var director = new WaveDirector(maxWaveGapSeconds: 2f);
            director.Initialize(level, new DeterministicRandomSource(1));

            var allDispatched = new List<SpawnCommand>();
            for (double t = 0.0; t <= 31.0; t += 0.5)
            {
                director.Tick(t);
                allDispatched.AddRange(director.PendingSpawns);
                director.ClearPendingSpawns();
            }

            Assert.That(allDispatched.Count, Is.GreaterThan(2), "A hézagot ki kell tölteni pót-spawnokkal.");

            for (int i = 1; i < allDispatched.Count; i++)
            {
                double gap = allDispatched[i].ScheduledGameTime - allDispatched[i - 1].ScheduledGameTime;
                Assert.That(gap, Is.LessThanOrEqualTo(director.MaxWaveGapSeconds + 1e-6),
                    $"A {i - 1}. és {i}. spawn között {gap:0.###}s hézag van, ez > MaxWaveGapSeconds ({director.MaxWaveGapSeconds}).");
            }
        }

        [Test]
        public void AllWavesCompleted_IsFalseBeforeLastWaveEndsAndTrueAfter()
        {
            var waves = new List<WaveDefinition>
            {
                new WaveDefinition(0, startGameTime: 0.0, durationSeconds: 5.0, spawns: new[]
                {
                    new WaveSpawnEntry("basic_bot", count: 1, spawnIntervalSeconds: 1f)
                })
            };
            var level = CombatTestFixtures.BuildLevel(waves: waves);
            var director = new WaveDirector();
            director.Initialize(level, new DeterministicRandomSource(1));

            director.Tick(3.0);
            Assert.That(director.AllWavesCompleted, Is.False);

            director.Tick(5.1);
            Assert.That(director.AllWavesCompleted, Is.True);
        }

        [Test]
        public void IsBossActive_TrueOnlyDuringFinalWaveWhenBossEnemyIdIsSet()
        {
            var waves = new List<WaveDefinition>
            {
                new WaveDefinition(0, startGameTime: 0.0, durationSeconds: 5.0, spawns: new[]
                {
                    new WaveSpawnEntry("basic_bot", count: 1, spawnIntervalSeconds: 1f)
                }),
                new WaveDefinition(1, startGameTime: 10.0, durationSeconds: 5.0, spawns: new[]
                {
                    new WaveSpawnEntry("boss_bot", count: 1, spawnIntervalSeconds: 1f)
                })
            };
            var level = CombatTestFixtures.BuildLevel(waves: waves, bossEnemyId: "boss_bot");
            var director = new WaveDirector();
            director.Initialize(level, new DeterministicRandomSource(1));

            director.Tick(2.0);
            Assert.That(director.IsBossActive, Is.False, "Az első hullám nem a boss-hullám.");

            director.Tick(11.0);
            Assert.That(director.IsBossActive, Is.True);
        }

        [Test]
        public void TotalWaves_MatchesLevelWaveCount()
        {
            var waves = new List<WaveDefinition>
            {
                new WaveDefinition(0, 0.0, 5.0),
                new WaveDefinition(1, 10.0, 5.0)
            };
            var level = CombatTestFixtures.BuildLevel(waves: waves);
            var director = new WaveDirector();
            director.Initialize(level, new DeterministicRandomSource(1));

            Assert.That(director.TotalWaves, Is.EqualTo(2));
        }
    }
}
