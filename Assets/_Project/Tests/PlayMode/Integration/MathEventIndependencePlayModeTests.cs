// MathEventIndependencePlayModeTests.cs
// F1-18 / F3-01 — math-horde.md 17.4 / criterion F
// ("Matematikai feladat közben is fut a harci szimuláció").
//
// GameSessionServiceTests (EditMode) already proves the Tick ORDER
// invariant — wave -> combat -> math, once per call — with manual,
// synthetic deltaTime values. GameplaySessionIntegrationTests (PlayMode,
// F1-17) proves the real Gameplay.unity scene's composition root drives
// combat over real engine frames, but only against NullMathEventDirector
// (the Phase-1 no-op placeholder — see IMathEventDirector.cs), which never
// does any work, so it cannot demonstrate that a math step with actual
// internal state fails to stall combat.
//
// F3-01 replaces the original busy counter with the production per-event
// lifecycle. The test driver supplies semantic inputs while the real
// session/wave/combat/resource services run across Unity frames. It holds
// AwaitingDecision open long enough to observe continuing ammo consumption.
// Scheduling, carriers, actual gate resolution and rewards belong to later
// F3 tickets; the driver deliberately does not simulate those services.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TestTools;
using MathHorde.Core.Common;
using MathHorde.Core.Levels;
using MathHorde.Core.Progress;
using MathHorde.Core.Resources;
using MathHorde.GameLogic;
using MathHorde.Infrastructure.Common;
using MathHorde.Presentation.Events;
using NUnit.Framework;

namespace MathHorde.Tests.PlayMode.Integration
{
    [TestFixture]
    public class MathEventIndependencePlayModeTests
    {
        /// <summary>
        /// Supplies semantic lifecycle inputs to the production state machine,
        /// using only the shared game time, with no combat/clock reference.
        /// </summary>
        private sealed class LifecycleDriver : IMathEventDirector
        {
            public int TickCount { get; private set; }
            public MathEventStateMachine Machine { get; } = new MathEventStateMachine(MathEventId.Create(7, 0));
            private double _decisionOpenedAt;

            public void Tick(double gameTime)
            {
                TickCount++;
                switch (Machine.State)
                {
                    case MathEventState.Inactive:
                        Machine.Fire(MathEventTrigger.Schedule, gameTime);
                        break;
                    case MathEventState.Preview:
                        Machine.Fire(MathEventTrigger.PreviewElapsed, gameTime);
                        break;
                    case MathEventState.Approaching:
                        Machine.Fire(MathEventTrigger.DecisionZoneEntered, gameTime);
                        _decisionOpenedAt = gameTime;
                        break;
                    case MathEventState.AwaitingDecision:
                        if (gameTime - _decisionOpenedAt >= 0.25)
                            Machine.Fire(MathEventTrigger.LaneSelected, gameTime);
                        break;
                    case MathEventState.Resolving:
                        Machine.Fire(MathEventTrigger.RewardApplied, gameTime);
                        break;
                }
            }
        }

        private static LevelDefinition BuildLevel(IReadOnlyList<WaveDefinition> waves)
        {
            return new LevelDefinition(
                id: new LevelId("w1-l01"),
                worldId: new WorldId("w1"),
                displayNameKey: "level.w1l01.name",
                laneCount: 3,
                targetDurationSeconds: 200,
                waves: waves,
                mathEvents: null,
                bossEnemyId: null,
                startingAmmo: 500,
                maxAmmo: 500,
                startingHealth: 1000,
                maxHealth: 1000,
                startingShield: 0,
                maxShield: 0,
                requiredMathAccuracy: 0f,
                weaponId: "basic_weapon");
        }

        [UnityTest]
        public IEnumerator CombatKeepsRunning_AcrossRealEngineFrames_WhileActualMathLifecycleAdvances()
        {
            var waves = new List<WaveDefinition>
            {
                new WaveDefinition(0, startGameTime: 0.0, durationSeconds: 1000.0, spawns: new[]
                {
                    new WaveSpawnEntry("basic_bot", count: 20, spawnIntervalSeconds: 0.1f, allowedLanes: new[] { new LaneIndex(0) })
                })
            };
            var level = BuildLevel(waves);
            var weapon = new WeaponDefinition("basic_weapon", damage: 1, fireRate: 20f, range: 20f, ammoPerShot: 1, projectileSpeed: 0f, TargetingMode.OwnLaneThenAdjacent, "fx_basic");
            var enemy = new EnemyDefinition("basic_bot", EnemyType.Basic, maxHealth: 1000, movementSpeed: 1f, contactDamage: 5, attackCooldownSeconds: 1f, rewardValue: 1, "prefab_basic_bot");
            var catalog = new Dictionary<string, EnemyDefinition> { ["basic_bot"] = enemy };

            var sink = new GameEventBus();
            var clock = new UnityGameClock();
            var resourceService = new ResourceService(sink, clock);
            var waveDirector = new WaveDirector();
            var combatService = new CombatService(resourceService, sink);
            var mathEventDirector = new LifecycleDriver();
            var session = new GameSessionService(clock, waveDirector, combatService, mathEventDirector, resourceService, sink);

            session.StartSession(level, weapon, catalog, seed: 7);
            int startingAmmo = resourceService.GetCurrent(ResourceType.Ammo);

            int framesRun = 0;
            int ammoAtDecisionOpen = -1;
            int ammoAtResolution = -1;
            float deadline = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < deadline
                   && (mathEventDirector.Machine.State != MathEventState.Completed
                       || resourceService.GetCurrent(ResourceType.Ammo) >= startingAmmo || combatService.ActiveEnemies.Count == 0))
            {
                session.Tick(Time.deltaTime);
                if (mathEventDirector.Machine.State == MathEventState.AwaitingDecision && ammoAtDecisionOpen < 0)
                    ammoAtDecisionOpen = resourceService.GetCurrent(ResourceType.Ammo);
                if (mathEventDirector.Machine.State == MathEventState.Resolving)
                    ammoAtResolution = resourceService.GetCurrent(ResourceType.Ammo);
                framesRun++;
                yield return null;
            }

            int ammoAfter = resourceService.GetCurrent(ResourceType.Ammo);

            Assert.That(framesRun, Is.GreaterThan(0), "A tesztnek legalább egy valós motor-frame-et le kellett futtatnia.");
            Assert.That(mathEventDirector.TickCount, Is.EqualTo(framesRun),
                "A matek-léptetésnek minden valós frame-ben pontosan egyszer kell futnia — ha ez elmarad, a 14. szakasz szinkronizációs követelménye sérül.");
            Assert.That(ammoAfter, Is.LessThan(startingAmmo),
                "A lőszernek fogynia kellett valós motor-frame-eken át, FÜGGETLENÜL attól, hogy a matek-direktor minden tick-ben dolgozott (kritérium F).");
            Assert.That(combatService.ActiveEnemies.Count, Is.GreaterThan(0),
                "Legalább egy ellenségnek életben kellett maradnia / spawn-olnia a mérési ablakban.");
            Assert.That(session.IsRunning, Is.True, "A session nem állhat meg csak attól, hogy a matek-direktor minden frame-ben dolgozik.");
            Assert.That(mathEventDirector.Machine.State, Is.EqualTo(MathEventState.Completed));
            Assert.That(ammoAtDecisionOpen, Is.GreaterThan(0));
            Assert.That(ammoAtResolution, Is.GreaterThanOrEqualTo(0));
            Assert.That(ammoAtResolution, Is.LessThan(ammoAtDecisionOpen),
                "Combat must keep firing while the actual lifecycle is AwaitingDecision.");
            Assert.That(clock.IsPaused, Is.False);
        }
    }
}
