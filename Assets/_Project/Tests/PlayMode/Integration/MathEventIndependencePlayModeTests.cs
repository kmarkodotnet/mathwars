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
using MathHorde.Core.Expressions;
using MathHorde.Core.Problems;
using MathHorde.Core.Skills;
using MathHorde.GameLogic;
using MathHorde.GameLogic.Events;
using MathHorde.MathEngine;
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
        private sealed class LifecycleDriver : NullMathEventDirector
        {
            public int TickCount { get; private set; }
            public MathEventStateMachine Machine { get; } = new MathEventStateMachine(MathEventId.Create(7, 0));
            private double _decisionOpenedAt;

            public override void Tick(double gameTime)
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

        private static LevelDefinition BuildLevel(IReadOnlyList<WaveDefinition> waves, IReadOnlyList<MathEventScheduleEntry> mathEvents = null)
        {
            return new LevelDefinition(
                id: new LevelId("w1-l01"),
                worldId: new WorldId("w1"),
                displayNameKey: "level.w1l01.name",
                laneCount: 3,
                targetDurationSeconds: 200,
                waves: waves,
                mathEvents: mathEvents,
                bossEnemyId: null,
                startingHealth: 1000,
                maxHealth: 1000,
                startingShield: 500,
                maxShield: 500,
                requiredMathAccuracy: 0f,
                weaponId: "basic_weapon");
        }

        [UnityTest]
        public IEnumerator RealResourceDirector_KeepsCombatRunningAcrossPreviewBindingAndDecision()
            => RunRealDirector(ProblemType.ResourceOperationComparison);

        [UnityTest]
        public IEnumerator RealLaneDirector_KeepsCombatRunningWithFullEarlyPreview()
            => RunRealDirector(ProblemType.AnswerLaneSelection);

        private static IEnumerator RunRealDirector(ProblemType type)
        {
            var waves = new[] { new WaveDefinition(0, 0, 1000, spawns: new[]
            { new WaveSpawnEntry("basic_bot", 20, 0.05f, new[] { new LaneIndex(0) }) }) };
            var level = BuildLevel(waves, new[] { new MathEventScheduleEntry(0, type, 0.05f, 2f, ResourceType.Shield, GateGoal.Maximize) });
            var profile = new DifficultyProfile(4, 4, 4, 4,
                new[] { BinaryOperator.Addition, BinaryOperator.Modulo }, (SkillTag[])System.Enum.GetValues(typeof(SkillTag)),
                0, 500, 2000, 2, 2, false, 0, false, 0, false, 2, 2f, 1, true);
            var bus = new GameEventBus(); var clock = new UnityGameClock();
            var resources = new ResourceService(bus, clock);
            var combat = new CombatService(resources, bus);
            bool gate = type == ProblemType.ResourceOperationComparison;
            var settings = new MathEventDirectorSettings(1, 5f, 3f, 20f, 1f,
                new Dictionary<int, RewardSpec> { [0] = new RewardSpec(ResourceType.Shield, gate ? 0 : 10, gate) });
            var director = new MathEventDirector(resources, bus, settings);
            var session = new GameSessionService(clock, new WaveDirector(), combat, director, resources, bus);
            var enemy = new EnemyDefinition("basic_bot", EnemyType.Basic, 1000, 1f, 5, 1f, "prefab_basic_bot");
            var weapon = new WeaponDefinition("basic_weapon", 1, 20f, 20f, 0f, TargetingMode.OwnLaneThenAdjacent, "fx_basic");
            session.StartSession(level, weapon, new Dictionary<string, EnemyDefinition> { ["basic_bot"] = enemy }, 7);
            director.Initialize(level, profile, new MathProblemGenerator(), new DeterministicRandomSource(7));
            var startup = director.PrepareUpcomingEvents(clock.GameTime);
            Assert.That(startup.IsSuccess, Is.True, startup.ErrorCode);
            int diagnostics = 0;
            bus.Subscribe<MathEventPreparationFailed>(_ => diagnostics++);
            bool sawPreview = false, sawSpawn = false;
            int shots = 0; bus.Subscribe<WeaponFired>(_ => shots++);
            int shotsAtDecision = -1, snapshot = -1;
            double openedAt = 0;
            ActiveMathEvent active = null;
            float wallDeadline = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < wallDeadline)
            {
                var prepared = director.PrepareUpcomingEvents(clock.GameTime);
                session.Tick(Time.deltaTime);
                Assert.That(prepared.IsSuccess, Is.True, prepared.ErrorCode);
                if (director.ActiveEvents.Count > 0)
                {
                    active = director.ActiveEvents[0];
                    if (active.State == MathEventState.Preview)
                    {
                        sawPreview = true;
                        Assert.That(active.Problem == null, Is.EqualTo(gate));
                        Assert.That(active.Carriers.Count, Is.EqualTo(0));
                    }
                    if (!sawSpawn && active.State == MathEventState.Approaching)
                    {
                        sawSpawn = true;
                        snapshot = active.SnapshotValue;
                        if (gate) Assert.That(snapshot, Is.EqualTo(resources.GetCurrent(ResourceType.Shield)), "Snapshot must follow this frame's combat.");
                    }
                    if (active.State == MathEventState.AwaitingDecision)
                    {
                        if (shotsAtDecision < 0) { shotsAtDecision = shots; openedAt = clock.GameTime; }
                        if (clock.GameTime - openedAt >= 0.25) break;
                    }
                }
                yield return null;
            }
            Assert.That(active, Is.Not.Null);
            Assert.That(sawPreview, Is.True); Assert.That(sawSpawn, Is.True);
            Assert.That(active.State, Is.EqualTo(MathEventState.AwaitingDecision));
            Assert.That(shots, Is.GreaterThan(shotsAtDecision));
            Assert.That(combat.ActiveEnemies.Count, Is.GreaterThan(0)); Assert.That(session.IsRunning, Is.True);
            Assert.That(clock.IsPaused, Is.False); Assert.That(diagnostics, Is.EqualTo(0));
            if (gate) Assert.That(active.ResourceGate.Value.SnapshotValue, Is.EqualTo(snapshot));
            double beforePause = clock.GameTime, deadline = active.DeadlineGameTime;
            session.Pause(); session.Tick(0.5f);
            Assert.That(clock.GameTime, Is.EqualTo(beforePause)); Assert.That(active.DeadlineGameTime, Is.EqualTo(deadline));
            session.Resume();
            LaneIndex selectedLane = default;
            if (gate)
                selectedLane = active.ResourceGate.Value.Template.Options[active.ResourceGate.Value.CorrectOptionIndex].Lane;
            else
                foreach (var option in active.Problem.Options)
                    if (option.IsCorrect) selectedLane = option.Lane;
            director.OnPlayerLaneSelected(selectedLane, clock.GameTime);
            Assert.That(active.State, Is.EqualTo(MathEventState.Resolving));
            Assert.That(director.PendingResolutions.Count, Is.EqualTo(1));
            Assert.That(director.PendingResolutions[0].TimedOut, Is.False);
            var resolver = new GateResolutionService(bus, 1);
            var rewards = new RewardService(resources, bus, clock, 1);
            var resolution = resolver.Resolve(director.PendingResolutions[0], active);
            int beforeReward = resources.GetCurrent(ResourceType.Shield);
            var application = rewards.ApplyReward(resolution);
            Assert.That(application.Status, Is.EqualTo(RewardApplicationStatus.Applied));
            Assert.That(application.RequestedDelta, Is.EqualTo(resolution.RewardDelta));
            Assert.That(resources.GetCurrent(ResourceType.Shield), Is.EqualTo(beforeReward + application.AppliedDelta));
            director.OnRewardProcessed(application, clock.GameTime);
            Assert.That(active.State, Is.EqualTo(MathEventState.Completed));
            var restoredRewards = new RewardService(resources, bus, clock, 1);
            restoredRewards.RestoreAppliedRewards(rewards.AppliedRewardEventIds);
            Assert.That(restoredRewards.ApplyReward(resolution).Status, Is.EqualTo(RewardApplicationStatus.AlreadyApplied));
            Assert.That(resources.GetCurrent(ResourceType.Shield), Is.EqualTo(beforeReward + application.AppliedDelta));
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
            var weapon = new WeaponDefinition("basic_weapon", damage: 1, fireRate: 20f, range: 20f, projectileSpeed: 0f, TargetingMode.OwnLaneThenAdjacent, "fx_basic");
            var enemy = new EnemyDefinition("basic_bot", EnemyType.Basic, maxHealth: 1000, movementSpeed: 1f, contactDamage: 5, attackCooldownSeconds: 1f, "prefab_basic_bot");
            var catalog = new Dictionary<string, EnemyDefinition> { ["basic_bot"] = enemy };

            var sink = new GameEventBus();
            var clock = new UnityGameClock();
            var resourceService = new ResourceService(sink, clock);
            var waveDirector = new WaveDirector();
            var combatService = new CombatService(resourceService, sink);
            var mathEventDirector = new LifecycleDriver();
            var session = new GameSessionService(clock, waveDirector, combatService, mathEventDirector, resourceService, sink);

            session.StartSession(level, weapon, catalog, seed: 7);
            int shots = 0; sink.Subscribe<WeaponFired>(_ => shots++);
            int startingShots = shots;

            int framesRun = 0;
            int shotsAtDecisionOpen = -1;
            int shotsAtResolution = -1;
            float deadline = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < deadline
                   && (mathEventDirector.Machine.State != MathEventState.Completed
                       || shots <= startingShots || combatService.ActiveEnemies.Count == 0))
            {
                session.Tick(Time.deltaTime);
                if (mathEventDirector.Machine.State == MathEventState.AwaitingDecision && shotsAtDecisionOpen < 0)
                    shotsAtDecisionOpen = shots;
                if (mathEventDirector.Machine.State == MathEventState.Resolving)
                    shotsAtResolution = shots;
                framesRun++;
                yield return null;
            }

            int shotsAfter = shots;

            Assert.That(framesRun, Is.GreaterThan(0), "A tesztnek legalább egy valós motor-frame-et le kellett futtatnia.");
            Assert.That(mathEventDirector.TickCount, Is.EqualTo(framesRun),
                "A matek-léptetésnek minden valós frame-ben pontosan egyszer kell futnia — ha ez elmarad, a 14. szakasz szinkronizációs követelménye sérül.");
            Assert.That(shotsAfter, Is.GreaterThan(startingShots),
                "A lőszernek fogynia kellett valós motor-frame-eken át, FÜGGETLENÜL attól, hogy a matek-direktor minden tick-ben dolgozott (kritérium F).");
            Assert.That(combatService.ActiveEnemies.Count, Is.GreaterThan(0),
                "Legalább egy ellenségnek életben kellett maradnia / spawn-olnia a mérési ablakban.");
            Assert.That(session.IsRunning, Is.True, "A session nem állhat meg csak attól, hogy a matek-direktor minden frame-ben dolgozik.");
            Assert.That(mathEventDirector.Machine.State, Is.EqualTo(MathEventState.Completed));
            Assert.That(shotsAtDecisionOpen, Is.GreaterThan(0));
            Assert.That(shotsAtResolution, Is.GreaterThanOrEqualTo(0));
            Assert.That(shotsAtResolution, Is.GreaterThan(shotsAtDecisionOpen),
                "Combat must keep firing while the actual lifecycle is AwaitingDecision.");
            Assert.That(clock.IsPaused, Is.False);
        }
    }
}
