using System.Collections.Generic;
using MathHorde.Core.Common;
using MathHorde.Core.Levels;
using MathHorde.Core.Progress;
using MathHorde.Core.Resources;
using MathHorde.GameLogic;
using MathHorde.GameLogic.Events;
using MathHorde.Tests.EditMode.TestSupport;
using NUnit.Framework;

namespace MathHorde.Tests.EditMode.GameLogic
{
    // Contract game-logic.md 8. (ARCHITECTURE 6.1) — the mandatory Tick
    // order is the F acceptance criterion's core, plus victory/defeat
    // evaluation (spec 1.2, 1.3: ammo running out is NOT a defeat).
    [TestFixture]
    public class GameSessionServiceTests
    {
        /// <summary>
        /// Records the order in which the Tick sub-steps actually run, by
        /// wrapping the real WaveDirector/CombatService and the
        /// NullMathEventDirector. Proves steps 1-4 of ARCHITECTURE 6.1 fire
        /// in the contracted order, once per Tick call.
        /// </summary>
        private sealed class RecordingWaveDirector : IWaveDirector
        {
            private readonly IWaveDirector _inner;
            public List<string> Log { get; }

            public RecordingWaveDirector(IWaveDirector inner, List<string> log)
            {
                _inner = inner;
                Log = log;
            }

            public int CurrentWaveIndex => _inner.CurrentWaveIndex;
            public int TotalWaves => _inner.TotalWaves;
            public bool AllWavesCompleted => _inner.AllWavesCompleted;
            public bool IsBossActive => _inner.IsBossActive;
            public IReadOnlyList<SpawnCommand> PendingSpawns => _inner.PendingSpawns;
            public float MaxWaveGapSeconds => _inner.MaxWaveGapSeconds;

            public void Initialize(LevelDefinition level, IRandomSource random) => _inner.Initialize(level, random);

            public void Tick(double gameTime)
            {
                Log.Add("wave");
                _inner.Tick(gameTime);
            }

            public void ClearPendingSpawns() => _inner.ClearPendingSpawns();

            public void ApplyCombatModifiers(CombatModifiers modifiers) => _inner.ApplyCombatModifiers(modifiers);
        }

        private sealed class RecordingCombatService : ICombatService
        {
            private readonly ICombatService _inner;
            public List<string> Log { get; }

            public RecordingCombatService(ICombatService inner, List<string> log)
            {
                _inner = inner;
                Log = log;
            }

            public LaneIndex PlayerLane => _inner.PlayerLane;
            public string CurrentWeaponId => _inner.CurrentWeaponId;
            public Result<bool> SelectWeapon(string weaponId, double gameTime) => _inner.SelectWeapon(weaponId, gameTime);
            public bool IsPlayerAlive => _inner.IsPlayerAlive;
            public IReadOnlyList<EnemySnapshot> ActiveEnemies => _inner.ActiveEnemies;

            public void Initialize(LevelDefinition level, IReadOnlyList<WeaponDefinition> weapons, IReadOnlyDictionary<string, EnemyDefinition> enemyCatalog)
                => _inner.Initialize(level, weapons, enemyCatalog);

            public void Tick(double gameTime, float deltaTime)
            {
                Log.Add("combat");
                _inner.Tick(gameTime, deltaTime);
            }

            public int SpawnEnemy(string enemyId, LaneIndex lane, float spawnDepth) => _inner.SpawnEnemy(enemyId, lane, spawnDepth);

            public void MovePlayerToLane(LaneIndex lane) => _inner.MovePlayerToLane(lane);

            public void ApplyCombatModifiers(CombatModifiers modifiers) => _inner.ApplyCombatModifiers(modifiers);
        }

        private sealed class RecordingMathEventDirector : NullMathEventDirector
        {
            public List<string> Log { get; }

            public RecordingMathEventDirector(List<string> log)
            {
                Log = log;
            }

            public override void Tick(double gameTime)
            {
                Log.Add("math");
            }
        }

        private static GameSessionService CreateService(
            LevelDefinition level,
            out List<string> tickLog,
            out RecordingGameEventSink sink,
            out ICombatService combatService,
            out IResourceService resourceService)
        {
            tickLog = new List<string>();
            sink = new RecordingGameEventSink();
            var clock = new GameClock();
            resourceService = new ResourceService(sink, clock);

            var realWave = new WaveDirector();
            var realCombat = new CombatService(resourceService, sink);
            combatService = realCombat;

            var wave = new RecordingWaveDirector(realWave, tickLog);
            var combat = new RecordingCombatService(realCombat, tickLog);
            var math = new RecordingMathEventDirector(tickLog);

            return new GameSessionService(clock, wave, combat, math, resourceService, sink);
        }

        private static List<WaveDefinition> LongRunningWave()
        {
            return new List<WaveDefinition> { new WaveDefinition(0, startGameTime: 0.0, durationSeconds: 1000.0) };
        }

        [Test]
        public void Tick_RunsSubStepsInTheContractedOrder()
        {
            // "a tick-sorrend betartása" — the F criterion's core.
            var level = CombatTestFixtures.BuildLevel(waves: LongRunningWave());
            var service = CreateService(level, out var log, out _, out _, out _);
            var weapon = CombatTestFixtures.BuildWeapon();
            service.StartSession(level, new[] { weapon }, CombatTestFixtures.BuildCatalog(CombatTestFixtures.BuildEnemy()), seed: 1);

            service.Tick(0.1f);
            service.Tick(0.1f);

            Assert.That(log, Is.EqualTo(new[] { "wave", "combat", "math", "wave", "combat", "math" }));
        }

        [Test]
        public void Tick_WhilePaused_SkipsWaveCombatAndMathSteps()
        {
            var level = CombatTestFixtures.BuildLevel(waves: LongRunningWave());
            var service = CreateService(level, out var log, out _, out _, out _);
            var weapon = CombatTestFixtures.BuildWeapon();
            service.StartSession(level, new[] { weapon }, CombatTestFixtures.BuildCatalog(CombatTestFixtures.BuildEnemy()), seed: 1);

            service.Pause();
            service.Tick(0.1f);

            Assert.That(log, Is.Empty, "Szünetben sem a hullám-, sem a harci-, sem a matek-lépés nem futhat.");
        }

        [Test]
        public void CombatKeepsRunning_RegardlessOfWhatTheMathEventDirectorDoes()
        {
            // "matek-feladat közben is fut a harci szimuláció": even a
            // math-event director that reports heavy internal state every
            // tick cannot stall wave/combat, because they run BEFORE it in
            // the fixed order, and it has no reference back into either.
            var level = CombatTestFixtures.BuildLevel(waves: LongRunningWave());
            var service = CreateService(level, out var log, out _, out var combatService, out _);
            var weapon = CombatTestFixtures.BuildWeapon();
            service.StartSession(level, new[] { weapon }, CombatTestFixtures.BuildCatalog(CombatTestFixtures.BuildEnemy()), seed: 1);

            for (int i = 0; i < 50; i++)
            {
                service.Tick(0.1f);
            }

            int combatTicks = log.FindAll(entry => entry == "combat").Count;
            Assert.That(combatTicks, Is.EqualTo(50), "A harci szimuláció minden frame-ben lépett, a matek-léptetéstől függetlenül.");
        }

        [Test]
        public void Tick_PlayerHealthDepleted_EndsSessionAsDefeat()
        {
            // "nulla játékos-életerőnél GameOver"
            var waves = new List<WaveDefinition>
            {
                new WaveDefinition(0, startGameTime: 0.0, durationSeconds: 100.0, spawns: new[]
                {
                    new WaveSpawnEntry("brute", count: 1, spawnIntervalSeconds: 1f, allowedLanes: new[] { new LaneIndex(0) })
                })
            };
            var level = CombatTestFixtures.BuildLevel(waves: waves, startingHealth: 10, maxHealth: 10);
            var service = CreateService(level, out _, out var sink, out _, out _);
            var weapon = CombatTestFixtures.BuildWeapon(projectileSpeed: 0f);
            var brute = CombatTestFixtures.BuildEnemy(enemyId: "brute", maxHealth: 1000, contactDamage: 50, movementSpeed: 100f);
            service.StartSession(level, new[] { weapon }, CombatTestFixtures.BuildCatalog(brute), seed: 1);

            // Spawn happens at t=0.1; the enemy (depth 12, speed 100) needs
            // a couple more ticks to close to depth<=0 and apply contact damage.
            for (int i = 0; i < 5 && service.IsRunning; i++)
            {
                service.Tick(0.1f);
            }

            Assert.That(service.IsRunning, Is.False);
            Assert.That(service.Result, Is.EqualTo(SessionResult.Defeat));
            Assert.That(service.FailureReason, Is.EqualTo(FailureReason.HealthDepleted));
            Assert.That(sink.OfType<SessionEnded>().Count, Is.EqualTo(1));
        }

        [Test]
        public void Tick_NoAmmoPool_DoesNotEndTheSession()
        {
            // spec 1.3: "a lőszer kifogyása önmagában nem vereség".
            var waves = new List<WaveDefinition>
            {
                new WaveDefinition(0, startGameTime: 0.0, durationSeconds: 100.0)
            };
            var level = CombatTestFixtures.BuildLevel(waves: waves, startingHealth: 100, maxHealth: 100);
            var service = CreateService(level, out _, out _, out _, out var resourceService);
            var weapon = CombatTestFixtures.BuildWeapon();
            service.StartSession(level, new[] { weapon }, CombatTestFixtures.BuildCatalog(CombatTestFixtures.BuildEnemy()), seed: 1);

            for (int i = 0; i < 20; i++)
            {
                service.Tick(0.1f);
            }

            Assert.That(resourceService.State.Pools.ContainsKey(ResourceType.Ammo), Is.False);
            Assert.That(service.IsRunning, Is.True, "Lőszerhiány önmagában nem állítja meg a sessiont.");
            Assert.That(service.Result, Is.EqualTo(SessionResult.InProgress));
        }

        [Test]
        public void Tick_AllWavesCompletedAndPlayerAlive_EndsSessionAsVictory()
        {
            var waves = new List<WaveDefinition>
            {
                new WaveDefinition(0, startGameTime: 0.0, durationSeconds: 1.0)
            };
            var level = CombatTestFixtures.BuildLevel(waves: waves, requiredMathAccuracy: 0f);
            var service = CreateService(level, out _, out var sink, out _, out _);
            var weapon = CombatTestFixtures.BuildWeapon();
            service.StartSession(level, new[] { weapon }, CombatTestFixtures.BuildCatalog(CombatTestFixtures.BuildEnemy()), seed: 1);

            service.Tick(0.5f);
            Assert.That(service.IsRunning, Is.True);

            service.Tick(0.6f); // t = 1.1, past the wave's duration window
            Assert.That(service.IsRunning, Is.False);
            Assert.That(service.Result, Is.EqualTo(SessionResult.Victory));
            Assert.That(sink.OfType<SessionEnded>().Count, Is.EqualTo(1));
        }

        [Test]
        public void Finalize_IsIdempotent_SecondCallReturnsSameRecord()
        {
            var level = CombatTestFixtures.BuildLevel();
            var service = CreateService(level, out _, out _, out _, out _);
            var weapon = CombatTestFixtures.BuildWeapon();
            service.StartSession(level, new[] { weapon }, CombatTestFixtures.BuildCatalog(CombatTestFixtures.BuildEnemy()), seed: 1);

            var first = service.Finalize(SessionResult.Victory, FailureReason.None);
            var second = service.Finalize(SessionResult.Defeat, FailureReason.HealthDepleted);

            Assert.That(second, Is.SameAs(first));
            Assert.That(second.Result, Is.EqualTo(SessionResult.Victory), "A második hívás nem írhatja felül az elsőt.");
        }

        [Test]
        public void StartSession_PublishesSessionStarted()
        {
            var level = CombatTestFixtures.BuildLevel();
            var service = CreateService(level, out _, out var sink, out _, out _);
            var weapon = CombatTestFixtures.BuildWeapon();

            service.StartSession(level, new[] { weapon }, CombatTestFixtures.BuildCatalog(CombatTestFixtures.BuildEnemy()), seed: 42);

            var started = sink.OfType<SessionStarted>();
            Assert.That(started.Count, Is.EqualTo(1));
            Assert.That(started[0].Seed, Is.EqualTo(42));
            Assert.That(started[0].SessionId, Is.EqualTo(service.SessionId));
        }
    }
}
