using System;
using System.Collections.Generic;
using MathHorde.Core.Common;
using MathHorde.Core.Levels;
using MathHorde.Core.Problems;
using MathHorde.Core.Resources;
using MathHorde.GameLogic;
using MathHorde.GameLogic.Events;
using MathHorde.MathEngine;
using MathHorde.Tests.EditMode.MathEngine;
using MathHorde.Tests.EditMode.TestSupport;
using NUnit.Framework;

namespace MathHorde.Tests.EditMode.GameLogic
{
    [TestFixture]
    public class MathEventDirectorTests
    {
        private sealed class EmptySink : IGameEventSink
        { public void Publish<T>(in T e) where T : struct, IGameEvent { } }

        private sealed class CountingGenerator : IMathProblemGenerator
        {
            private readonly MathProblemGenerator _inner = new MathProblemGenerator();
            public int Generates, Prepares, Binds;
            public Result<MathProblem> Generate(ProblemGenerationRequest r) { Generates++; return _inner.Generate(r); }
            public IReadOnlyList<Result<MathProblem>> GenerateBatch(ProblemGenerationRequest r, int count) => _inner.GenerateBatch(r, count);
            public Result<PreparedResourceGate> PrepareResourceGate(ResourceGatePreparationRequest r) { Prepares++; return _inner.PrepareResourceGate(r); }
            public Result<ResourceGateBinding> BindResourceGate(PreparedResourceGate p, int snapshot, int capacity) { Binds++; return _inner.BindResourceGate(p, snapshot, capacity); }
        }

        private static MathEventScheduleEntry Entry(ProblemType type = ProblemType.ResourceOperationComparison, double trigger = 0, float think = 0)
            => new MathEventScheduleEntry(trigger, type, 1f, think, ResourceType.Ammo, GateGoal.Maximize);

        private static MathEventDirector Create(out ResourceService resources, out RecordingGameEventSink sink,
            MathEventScheduleEntry[] entries = null, int concurrent = 1, IMathProblemGenerator generator = null,
            IGameEventSink eventSink = null, IReadOnlyDictionary<int, RewardSpec> rewards = null)
        {
            entries = entries ?? new[] { Entry() };
            sink = new RecordingGameEventSink();
            eventSink = eventSink ?? sink;
            resources = new ResourceService(eventSink, new GameClock());
            var level = CombatTestFixtures.BuildLevel(startingAmmo: 20, maxAmmo: 30, mathEvents: entries);
            resources.Initialize(level);
            if (rewards == null)
            {
                var map = new Dictionary<int, RewardSpec>();
                for (int i = 0; i < entries.Length; i++)
                {
                    bool gate = entries[i].ProblemType == ProblemType.ResourceOperationComparison;
                    map[i] = new RewardSpec(ResourceType.Ammo, gate ? 0 : 10, gate);
                }
                rewards = map;
            }
            var settings = new MathEventDirectorSettings(concurrent, 5f, 3f, 2f, 1f, rewards);
            var director = new MathEventDirector(resources, eventSink, settings);
            director.Initialize(level, ResourceGatePreparationTests.Profile(capacity: 100), generator ?? new MathProblemGenerator(), new DeterministicRandomSource(7));
            return director;
        }

        private static ActiveMathEvent OpenDecision(MathEventDirector director)
        {
            Assert.That(director.PrepareUpcomingEvents(0).IsSuccess, Is.True);
            director.Tick(0); director.Tick(1); director.Tick(2);
            Assert.That(director.ActiveEvents[0].State, Is.EqualTo(MathEventState.AwaitingDecision));
            return director.ActiveEvents[0];
        }

        [Test]
        public void ResourcePreviewHidesProblemAndOperations_ThenBindsFreshSnapshotOnce()
        {
            var generator = new CountingGenerator();
            var d = Create(out var resources, out var sink, generator: generator);
            Assert.That(d.PrepareUpcomingEvents(0).IsSuccess, Is.True);
            Assert.That(d.PrepareUpcomingEvents(0).IsSuccess, Is.True);
            Assert.That(generator.Prepares, Is.EqualTo(1));
            Assert.That(generator.Generates, Is.EqualTo(0));
            d.Tick(0);
            var v = d.ActiveEvents[0];
            var preview = sink.OfType<MathEventPreviewStarted>()[0];
            Assert.That(v.State, Is.EqualTo(MathEventState.Preview));
            Assert.That(v.Problem, Is.Null); Assert.That(v.ProblemId, Is.Null); Assert.That(v.ResourceGate, Is.Null);
            Assert.That(v.Carriers.Count, Is.EqualTo(0));
            Assert.That(preview.DisplayExpression, Is.Empty); Assert.That(preview.ProblemId, Is.Null);
            Assert.That(preview.Resource, Is.EqualTo(ResourceType.Ammo));
            resources.ApplyDelta(ResourceType.Ammo, -3, "WEAPON_FIRE");
            d.Tick(1);
            Assert.That(v.State, Is.EqualTo(MathEventState.Approaching));
            Assert.That(v.SnapshotValue, Is.EqualTo(17));
            Assert.That(v.ResourceGate.Value.ResourceCapacity, Is.EqualTo(30));
            Assert.That(v.ProblemId, Is.EqualTo(v.ResourceGate.Value.ProblemId));
            Assert.That(v.Carriers.Count, Is.EqualTo(2));
            resources.ApplyDelta(ResourceType.Ammo, -2, "WEAPON_FIRE");
            resources.SetMax(ResourceType.Ammo, 40, "UPGRADE");
            d.PrepareUpcomingEvents(1); d.Tick(1.5);
            Assert.That(v.SnapshotValue, Is.EqualTo(17)); Assert.That(v.ResourceGate.Value.ResourceCapacity, Is.EqualTo(30));
            Assert.That(v.Carriers[0].Position.Y, Is.EqualTo(4f));
            Assert.That(generator.Binds, Is.EqualTo(1)); Assert.That(generator.Prepares, Is.EqualTo(1));
            Assert.That(sink.OfType<MathEventGatesSpawned>().Count, Is.EqualTo(1));
        }

        [Test]
        public void LanePreviewKeepsFullEarlyImmutableProblem()
        {
            var generator = new CountingGenerator();
            var d = Create(out _, out var sink, entries: new[] { Entry(ProblemType.AnswerLaneSelection) }, generator: generator);
            Assert.That(d.PrepareUpcomingEvents(0).IsSuccess, Is.True);
            d.Tick(0); var v = d.ActiveEvents[0]; var original = v.Problem;
            Assert.That(original, Is.Not.Null); Assert.That(v.ResourceGate, Is.Null);
            Assert.That(v.ProblemId, Is.EqualTo(original.Id));
            Assert.That(sink.OfType<MathEventPreviewStarted>()[0].DisplayExpression, Is.EqualTo(original.DisplayExpression));
            Assert.That(v.Carriers.Count, Is.EqualTo(0));
            d.Tick(1); d.Tick(2);
            Assert.That(v.Problem, Is.SameAs(original)); Assert.That(v.Carriers.Count, Is.EqualTo(2));
            Assert.That(generator.Generates, Is.EqualTo(1)); Assert.That(generator.Prepares, Is.EqualTo(0));
        }

        [Test]
        public void DeadlineStartsAtGeometricDecisionArrival_AndExpiresOnlyOnce()
        {
            var d = Create(out _, out var sink);
            var v = OpenDecision(d);
            Assert.That(v.DecisionOpenedAt, Is.EqualTo(2)); Assert.That(v.DeadlineGameTime, Is.EqualTo(8));
            d.Tick(7.99); Assert.That(v.State, Is.EqualTo(MathEventState.AwaitingDecision));
            d.Tick(8); d.Tick(9); d.OnPlayerLaneSelected(new LaneIndex(0), 9);
            Assert.That(v.State, Is.EqualTo(MathEventState.Expired));
            Assert.That(d.PendingResolutions.Count, Is.EqualTo(1)); Assert.That(d.PendingResolutions[0].TimedOut, Is.True);
            Assert.That(sink.OfType<MathEventExpired>()[0].ReasonCode, Is.EqualTo("DEADLINE"));
            Assert.That(sink.OfType<MathEventExpired>().Count, Is.EqualTo(1));
        }

        [Test]
        public void LargeTimeStepUsesArrivalTime_AndLateInputCannotBeatDeadline()
        {
            var d = Create(out _, out _);
            d.PrepareUpcomingEvents(0); d.Tick(0); d.Tick(1); d.Tick(8);
            Assert.That(d.ActiveEvents[0].DecisionOpenedAt, Is.EqualTo(2));
            Assert.That(d.ActiveEvents[0].State, Is.EqualTo(MathEventState.Expired));
            Assert.That(d.PendingResolutions[0].TimedOut, Is.True);
            var d2 = Create(out _, out _); var v2 = OpenDecision(d2);
            d2.OnPlayerLaneSelected(new LaneIndex(0), 8);
            Assert.That(v2.State, Is.EqualTo(MathEventState.Expired));
            Assert.That(d2.PendingResolutions.Count, Is.EqualTo(1)); Assert.That(d2.PendingResolutions[0].TimedOut, Is.True);
        }

        [TestCase(RewardApplicationStatus.Applied)]
        [TestCase(RewardApplicationStatus.SkippedIncorrect)]
        public void ChoiceAndRewardFeedbackAreIdempotent_AndNeverMutateResources(RewardApplicationStatus status)
        {
            var d = Create(out var resources, out var sink); var v = OpenDecision(d);
            d.OnPlayerLaneSelected(new LaneIndex(0), 2.1); d.OnPlayerLaneSelected(new LaneIndex(1), 2.1);
            Assert.That(v.State, Is.EqualTo(MathEventState.Resolving)); Assert.That(d.PendingResolutions.Count, Is.EqualTo(1));
            d.ClearPendingResolutions(); Assert.That(d.PendingResolutions.Count, Is.EqualTo(0));
            var ignored = new RewardApplication(v.EventId, RewardApplicationStatus.AlreadyApplied, 25, 25, ResourceType.Ammo);
            d.OnRewardProcessed(ignored, 2.2); Assert.That(v.State, Is.EqualTo(MathEventState.Resolving));
            var application = new RewardApplication(v.EventId, status, 25, status == RewardApplicationStatus.Applied ? 10 : 0, ResourceType.Ammo);
            d.OnRewardProcessed(application, 2.3); d.OnRewardProcessed(application, 2.3);
            Assert.That(v.State, Is.EqualTo(MathEventState.Completed)); Assert.That(resources.GetCurrent(ResourceType.Ammo), Is.EqualTo(20));
            Assert.That(sink.OfType<MathEventCompleted>().Count, Is.EqualTo(1));
            Assert.That(sink.OfType<MathEventCompleted>()[0].AppliedDelta, Is.EqualTo(application.AppliedDelta));
            d.Tick(3); Assert.That(d.ActiveEvents.Count, Is.EqualTo(1)); d.Tick(3.3); Assert.That(d.ActiveEvents.Count, Is.EqualTo(0));
        }

        [Test]
        public void UnrelatedAndSkippedExpiredFeedbackDoNotCompleteResolvingEvent()
        {
            var d = Create(out _, out var sink); var v = OpenDecision(d); d.OnPlayerLaneSelected(new LaneIndex(0), 2);
            d.OnRewardProcessed(new RewardApplication(MathEventId.Create(3, 99), RewardApplicationStatus.Applied, 1, 1, ResourceType.Ammo), 2);
            d.OnRewardProcessed(new RewardApplication(v.EventId, RewardApplicationStatus.SkippedExpired, 0, 0, ResourceType.Ammo), 2);
            Assert.That(v.State, Is.EqualTo(MathEventState.Resolving)); Assert.That(sink.OfType<MathEventCompleted>().Count, Is.EqualTo(0));
        }

        [Test]
        public void CompletedEventsSurviveSessionEnd_AndConfigurationMapsAreCopied()
        {
            var rewards = new Dictionary<int, RewardSpec> { [0] = new RewardSpec(ResourceType.Ammo, 0, true) };
            var targets = new Dictionary<int, int> { [2] = 25 };
            var settings = new MathEventDirectorSettings(1, 5f, 3f, 2f, 1f, rewards, targets);
            rewards.Clear(); targets.Clear();
            Assert.That(settings.RewardsByScheduleIndex.Count, Is.EqualTo(1));
            Assert.That(settings.TargetsByScheduleIndex[2], Is.EqualTo(25));
            Assert.That(() => ((IDictionary<int, RewardSpec>)settings.RewardsByScheduleIndex).Clear(), Throws.TypeOf<NotSupportedException>());
            var d = Create(out _, out var sink); var v = OpenDecision(d);
            d.OnPlayerLaneSelected(new LaneIndex(0), 2);
            d.OnRewardProcessed(new RewardApplication(v.EventId, RewardApplicationStatus.Applied, 1, 1, ResourceType.Ammo), 2);
            d.OnSessionEnded(2);
            Assert.That(v.State, Is.EqualTo(MathEventState.Completed)); Assert.That(sink.OfType<MathEventExpired>().Count, Is.EqualTo(0));
        }

        [Test]
        public void ScheduleSortingRetainsOriginalIndices_AndWaitersReceiveTheirFullPreview()
        {
            var entries = new[] { Entry(trigger: 5), Entry(), Entry() };
            var d = Create(out _, out var sink, entries: entries);
            var first = OpenDecision(d);
            Assert.That(first.EventId, Is.EqualTo(MathEventId.Create(7, 1)));
            d.OnPlayerLaneSelected(new LaneIndex(0), 2);
            d.OnRewardProcessed(new RewardApplication(first.EventId, RewardApplicationStatus.SkippedIncorrect, 0, 0, ResourceType.Ammo), 2);
            d.Tick(2.1);
            Assert.That(d.ActiveEvents.Count, Is.EqualTo(2));
            var next = d.ActiveEvents[1]; Assert.That(next.EventId, Is.EqualTo(MathEventId.Create(7, 2)));
            Assert.That(next.PreviewStartedAt, Is.EqualTo(2.1));
            d.Tick(3); Assert.That(next.State, Is.EqualTo(MathEventState.Preview));
            d.Tick(3.1); Assert.That(next.State, Is.EqualTo(MathEventState.Approaching));
            Assert.That(sink.OfType<MathEventPreviewStarted>().Count, Is.EqualTo(2));
        }

        [Test]
        public void TimePressureFreezesOnPreviewEntry_OverridesRemainBaseValues()
        {
            var d = Create(out _, out _, entries: new[] { Entry(think: 4), Entry(trigger: 20, think: 4) }, concurrent: 2);
            d.PrepareUpcomingEvents(0); d.Tick(0); var first = d.ActiveEvents[0];
            d.ApplyTimePressure(2f, 0.5f); d.Tick(1); d.Tick(2);
            Assert.That(first.DeadlineGameTime, Is.EqualTo(6));
            Assert.That(first.ResourceGate.Value.Prepared.ThinkTimeSeconds, Is.EqualTo(4));
            d.Tick(20); var second = d.ActiveEvents[d.ActiveEvents.Count - 1];
            Assert.That(second.PreviewStartedAt, Is.EqualTo(20));
            d.Tick(21); d.Tick(22); Assert.That(second.State, Is.EqualTo(MathEventState.Approaching));
            d.Tick(23); Assert.That(second.DeadlineGameTime, Is.EqualTo(31));
            Assert.That(second.ResourceGate.Value.Prepared.ThinkTimeSeconds, Is.EqualTo(4));
        }

        [Test]
        public void CapacityChangeBeforeBindingReprepares_ChangeInsideTickFailsHonestly()
        {
            var generator = new CountingGenerator(); var d = Create(out var resources, out _, generator: generator);
            d.PrepareUpcomingEvents(0); d.Tick(0); resources.SetMax(ResourceType.Ammo, 40, "UPGRADE");
            Assert.That(d.PrepareUpcomingEvents(0).IsSuccess, Is.True); d.Tick(1);
            Assert.That(generator.Prepares, Is.EqualTo(2)); Assert.That(d.ActiveEvents[0].ResourceGate.Value.ResourceCapacity, Is.EqualTo(40));
            var d2 = Create(out var r2, out var sink); d2.PrepareUpcomingEvents(0); d2.Tick(0);
            r2.SetMax(ResourceType.Ammo, 40, "INVALID_ORDER"); d2.Tick(1);
            Assert.That(d2.ActiveEvents[0].State, Is.EqualTo(MathEventState.Expired));
            Assert.That(d2.PendingResolutions.Count, Is.EqualTo(0));
            Assert.That(sink.OfType<MathEventPreparationFailed>()[0].ErrorCode, Is.EqualTo("GATE_CONTEXT_CHANGED"));
        }

        [Test]
        public void StartupFailureChecksEveryEntry_AndDoesNotEmitChildAttemptOrRunLazyGeneration()
        {
            var generator = new CountingGenerator();
            var rewards = new Dictionary<int, RewardSpec> { [1] = new RewardSpec(ResourceType.Ammo, 10, false) };
            var d = Create(out _, out var sink, entries: new[] { Entry(), Entry(ProblemType.AnswerLaneSelection) }, generator: generator, rewards: rewards);
            Assert.That(d.PrepareUpcomingEvents(0).ErrorCode, Is.EqualTo("MATH_EVENT_CONFIG_INVALID"));
            Assert.That(generator.Generates, Is.EqualTo(1)); Assert.That(d.ActiveEvents.Count, Is.EqualTo(0));
            Assert.That(sink.OfType<MathEventExpired>().Count, Is.EqualTo(0)); // host must not Tick on startup failure
            var unprepared = Create(out _, out var s2, generator: generator); unprepared.Tick(0);
            Assert.That(unprepared.ActiveEvents[0].State, Is.EqualTo(MathEventState.Expired));
            Assert.That(s2.OfType<MathEventPreparationFailed>()[0].ErrorCode, Is.EqualTo("MATH_EVENT_NOT_PREPARED"));
            Assert.That(unprepared.PendingResolutions.Count, Is.EqualTo(0)); Assert.That(generator.Prepares, Is.EqualTo(0));
        }

        [Test]
        public void RunningPreparationFailureExpiresOnlyAffectedUnboundEvent()
        {
            var d = Create(out var resources, out var sink, entries: new[] { Entry(), Entry(ProblemType.AnswerLaneSelection) }, concurrent: 2);
            d.PrepareUpcomingEvents(0); d.Tick(0); resources.SetMax(ResourceType.Ammo, 200, "UPGRADE");
            Assert.That(d.PrepareUpcomingEvents(0).ErrorCode, Is.EqualTo("GENERATION_EXHAUSTED")); d.Tick(1);
            Assert.That(d.ActiveEvents[0].State, Is.EqualTo(MathEventState.Expired));
            Assert.That(d.ActiveEvents[1].State, Is.EqualTo(MathEventState.Approaching));
            Assert.That(sink.OfType<MathEventExpired>()[0].ReasonCode, Is.EqualTo("PREPARATION_FAILED"));
            Assert.That(d.PendingResolutions.Count, Is.EqualTo(0));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void SessionEndExpiresNonterminalStates_AndLateFeedbackCannotReviveThem(int phase)
        {
            var d = Create(out _, out var sink, entries: new[] { Entry(), Entry(trigger: 50) });
            d.PrepareUpcomingEvents(0); d.Tick(0);
            if (phase >= 1) d.Tick(1);
            if (phase >= 2) d.Tick(2);
            if (phase >= 3) d.OnPlayerLaneSelected(new LaneIndex(0), 2);
            var v = d.ActiveEvents[0]; d.OnSessionEnded(2); d.OnSessionEnded(2);
            d.OnRewardProcessed(new RewardApplication(v.EventId, RewardApplicationStatus.Applied, 25, 25, ResourceType.Ammo), 2);
            Assert.That(v.State, Is.EqualTo(MathEventState.Expired)); Assert.That(d.PendingResolutions.Count, Is.EqualTo(0));
            Assert.That(sink.OfType<MathEventExpired>().Count, Is.EqualTo(2)); Assert.That(sink.OfType<MathEventCompleted>().Count, Is.EqualTo(0));
        }

        [Test]
        public void DirectorRunsTenThousandTicksWithoutCombat_AndLifecycleAndErrorPathsAllocateZero()
        {
            var warm = Create(out _, out _, eventSink: new EmptySink()); warm.PrepareUpcomingEvents(0); ExerciseLifecycle(warm);
            var d = Create(out _, out _, eventSink: new EmptySink()); d.PrepareUpcomingEvents(0);
            long before = GC.GetAllocatedBytesForCurrentThread(); ExerciseLifecycle(d);
            for (int i = 4; i < 10000; i++) d.Tick(i);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.EqualTo(0));
            Assert.That(d.PendingResolutions.Count, Is.EqualTo(1));
            var errorWarm = Create(out _, out _, eventSink: new EmptySink()); errorWarm.Tick(0);
            var error = Create(out _, out _, eventSink: new EmptySink());
            before = GC.GetAllocatedBytesForCurrentThread(); error.Tick(0); error.Tick(1);
            allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.EqualTo(0));
        }

        private static void ExerciseLifecycle(MathEventDirector d)
        {
            d.Tick(0); d.Tick(1); d.Tick(1.5); d.Tick(2); d.OnPlayerLaneSelected(new LaneIndex(0), 2);
            var v = d.ActiveEvents[0];
            d.OnRewardProcessed(new RewardApplication(v.EventId, RewardApplicationStatus.SkippedIncorrect, 0, 0, ResourceType.Ammo), 2);
            d.Tick(3);
        }
    }
}
