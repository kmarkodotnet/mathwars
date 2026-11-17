// MathEventIndependencePlayModeTests.cs
// F1-18 — audit-identified gap fill for math-horde.md 17.4 / criterion F
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
// This test closes that gap the honest way: F3's real IGateResolutionService
// / AwaitingDecision state machine does not exist yet (game-logic.md 5.,
// 6. are still contract-only — grep confirms no implementation), and
// GameplaySessionBootstrap hard-wires NullMathEventDirector with no
// injection seam (Phase-1 scope, not this task's to change — a DI seam
// there would be a composition-root/contract change, out of scope for
// F1-18). So instead of faking the not-yet-built state machine, this test
// wires the REAL GameLogic services (WaveDirector, CombatService,
// ResourceService, GameSessionService) directly — same as
// GameSessionServiceTests does — but drives them across REAL Unity engine
// frames (yield return null, real Time.deltaTime) with a math-event
// director stand-in that does non-trivial, observable work every tick.
// It proves two things no existing test proves together: (1) the math step
// actually runs every real engine frame (TickCount keeps pace with frame
// count), and (2) combat (ammo depletion, enemy damage) is completely
// unaffected by it, under real frame timing rather than synthetic Tick()
// calls. When F3-14 lands the real state machine, this test's director
// stand-in should be replaced with the genuine AwaitingDecision scenario.
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
        /// Stands in for an active math event ("AwaitingDecision"): does
        /// real, observable bookkeeping every Tick (not a no-op like
        /// NullMathEventDirector), yet structurally cannot touch combat —
        /// the contract (game-logic.md 5.) gives it no reference to any
        /// other service, only a gameTime double.
        /// </summary>
        private sealed class BusyMathEventDirector : IMathEventDirector
        {
            public int TickCount { get; private set; }
            private double _busyForSeconds;

            public void Tick(double gameTime)
            {
                TickCount++;
                // Non-trivial internal state, deliberately never read by
                // anything outside this class — this is the point: even
                // "the math event is still open after N seconds" cannot
                // leak into combat through this interface.
                _busyForSeconds = gameTime;
            }

            public double LastSeenGameTime => _busyForSeconds;
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
        public IEnumerator CombatKeepsRunning_AcrossRealEngineFrames_WhileMathEventDirectorIsBusyEveryTick()
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
            var mathEventDirector = new BusyMathEventDirector();
            var session = new GameSessionService(clock, waveDirector, combatService, mathEventDirector, resourceService, sink);

            session.StartSession(level, weapon, catalog, seed: 7);
            int startingAmmo = resourceService.GetCurrent(ResourceType.Ammo);

            int framesRun = 0;
            float deadline = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < deadline
                   && (resourceService.GetCurrent(ResourceType.Ammo) >= startingAmmo || combatService.ActiveEnemies.Count == 0))
            {
                session.Tick(Time.deltaTime);
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
        }
    }
}
