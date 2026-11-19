using System;
using System.Collections.Generic;
using MathHorde.Core.Common;
using MathHorde.Core.Expressions;
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
    public class GateResolutionServiceTests
    {
        private sealed class EmptySink : IGameEventSink
        {
            public void Publish<T>(in T gameEvent) where T : struct, IGameEvent { }
        }

        // Only generation is fixed. Real director timing, binding, lane commands and resolver run below.
        private sealed class FixedGenerator : IMathProblemGenerator
        {
            private readonly MathProblemGenerator _binder = new MathProblemGenerator();
            internal PreparedResourceGate Prepared;
            internal MathProblem Problem;
            internal int Binds;
            public Result<MathProblem> Generate(ProblemGenerationRequest request) => Result<MathProblem>.Ok(Problem);
            public IReadOnlyList<Result<MathProblem>> GenerateBatch(ProblemGenerationRequest request, int count)
                => throw new NotSupportedException();
            public Result<PreparedResourceGate> PrepareResourceGate(ResourceGatePreparationRequest request)
                => Result<PreparedResourceGate>.Ok(Prepared);
            public Result<ResourceGateBinding> BindResourceGate(PreparedResourceGate prepared, int snapshot, int capacity)
            {
                Binds++;
                return _binder.BindResourceGate(prepared, snapshot, capacity);
            }
        }

        private sealed class Fixture
        {
            internal readonly MathEventDirector Director;
            internal readonly ResourceService Resources;
            internal readonly FixedGenerator Generator;
            internal ActiveMathEvent Event => Director.ActiveEvents[0];

            internal Fixture(ProblemType type = ProblemType.ResourceOperationComparison, int count = 1,
                IGameEventSink sink = null, GateGoal goal = GateGoal.Maximize,
                ResourceType resource = ResourceType.Ammo, bool decreasing = false,
                ErrorCategory? distractor = ErrorCategory.OnesDigitError, int incorrectAmount = 0,
                int seed = 7)
            {
                sink = sink ?? new EmptySink();
                bool gate = type == ProblemType.ResourceOperationComparison;
                var reward = new RewardSpec(resource, gate ? 0 : 30, gate, incorrectAmount);
                var tags = new[] { SkillTag.Addition, SkillTag.Multiplication };
                var options = new[]
                {
                    new ResourceGateOptionTemplate(0, new LaneIndex(1), new BinaryOperationNode(
                        decreasing ? BinaryOperator.Subtraction : BinaryOperator.Multiplication,
                        new UnknownNode(), new ConstantNode(decreasing ? 10 : 2)), decreasing ? "−10" : "×2"),
                    new ResourceGateOptionTemplate(1, new LaneIndex(0), new BinaryOperationNode(
                        decreasing ? BinaryOperator.Subtraction : BinaryOperator.Addition,
                        new UnknownNode(), new ConstantNode(decreasing ? 5 : 25)), decreasing ? "−5" : "+25")
                };
                var metadata = new IReadOnlyList<SkillTag>[44];
                for (int i = 0; i < metadata.Length; i++) metadata[i] = tags;
                Generator = new FixedGenerator
                {
                    Prepared = new PreparedResourceGate(new MathProblemId(new Guid("11111111-2222-3333-4444-555555555555")),
                        seed, ResourceGatePreparationTests.Profile(capacity: 100, operators: new[]
                            { BinaryOperator.Addition, BinaryOperator.Multiplication, BinaryOperator.Subtraction }), 6f, reward, goal,
                        goal == GateGoal.ReachTarget ? 44 : (int?)null, 43,
                        new[] { new ResourceGateTemplate(options, tags) }, new[] { metadata }),
                    Problem = new MathProblem(new MathProblemId(new Guid("66666666-7777-8888-9999-000000000000")),
                        type, new ConstantNode(42), "42 = ?", new EvaluationResult(42),
                        new[]
                        {
                            new AnswerOption(0, new LaneIndex(1), new EvaluationResult(41), null, false, distractor, "41"),
                            new AnswerOption(1, new LaneIndex(0), new EvaluationResult(42), null, true, null, "42")
                        }, 1, tags, 6f, reward, seed, goal, type == ProblemType.TargetValueSelection ? 42 : (int?)null, null)
                };
                var entries = new MathEventScheduleEntry[count];
                var rewards = new Dictionary<int, RewardSpec>();
                var targets = new Dictionary<int, int>();
                for (int i = 0; i < count; i++)
                {
                    entries[i] = new MathEventScheduleEntry(0, type, 1, 6, resource, goal);
                    // MVP director validates IncorrectAmount == 0; the fixed generated task below
                    // can exercise the resolver's wider RewardSpec contract independently.
                    rewards[i] = new RewardSpec(resource, gate ? 0 : 30, gate);
                    if (goal == GateGoal.ReachTarget) targets[i] = 44;
                    else if (type == ProblemType.TargetValueSelection) targets[i] = 42;
                }
                Resources = new ResourceService(sink, new GameClock());
                var level = CombatTestFixtures.BuildLevel(startingAmmo: 20, maxAmmo: 43,
                    startingHealth: 20, maxHealth: 43, mathEvents: entries);
                Resources.Initialize(level);
                Director = new MathEventDirector(Resources, sink,
                    new MathEventDirectorSettings(count, 5, 3, 2, 1, rewards, targets));
                Director.Initialize(level, ResourceGatePreparationTests.Profile(capacity: 100), Generator,
                    new DeterministicRandomSource(seed));
                Assert.That(Director.PrepareUpcomingEvents(0).IsSuccess, Is.True);
            }

            internal void Open()
            {
                Director.Tick(0);
                Director.Tick(1);
                Director.Tick(2);
                Assert.That(Event.State, Is.EqualTo(MathEventState.AwaitingDecision));
            }

            internal ResolveCommand Choose(int lane = 0, double time = 3.25)
            {
                Director.OnPlayerLaneSelected(new LaneIndex(lane), time);
                return Director.PendingResolutions[0];
            }
        }

        [Test]
        public void ResourceCorrect_UsesFrozenRawBindingDelta_AndLeavesLiveResourcesUntouched()
        {
            var f = new Fixture();
            f.Open();
            Assert.That(f.Event.Problem, Is.Null);
            f.Resources.ApplyDelta(ResourceType.Ammo, -12, "WEAPON_FIRE");
            f.Resources.SetMax(ResourceType.Ammo, 60, "UPGRADE");
            var command = f.Choose();
            var result = new GateResolutionService(new EmptySink(), 1).Resolve(command, f.Event);
            Assert.That(result.Outcome, Is.EqualTo(GateResolutionOutcome.CorrectRewarded));
            Assert.That(result.WasCorrect, Is.True);
            Assert.That(result.SelectedOptionIndex, Is.EqualTo(1)); // lane 0 contains option 1
            Assert.That(result.OperationResult, Is.EqualTo(45)); // raw, not the frozen Max=43
            Assert.That(result.SnapshotValue, Is.EqualTo(20));
            Assert.That(result.RewardDelta, Is.EqualTo(25));
            Assert.That(result.Resource, Is.EqualTo(ResourceType.Ammo));
            Assert.That(result.Answer.ProblemId, Is.EqualTo(f.Event.ProblemId.Value));
            Assert.That(result.Answer.SelectedOptionIndex, Is.EqualTo(1));
            Assert.That(result.Answer.SelectedLane, Is.EqualTo(new LaneIndex(0)));
            Assert.That(result.Answer.WasCorrect, Is.True);
            Assert.That(result.Answer.ErrorCategory, Is.EqualTo(ErrorCategory.None));
            Assert.That(result.Answer.ResponseTimeSeconds, Is.EqualTo(1.25f));
            Assert.That(result.Answer.AnsweredAtGameTime, Is.EqualTo(3.25));
            Assert.That(result.Answer.TimedOut, Is.False);
            Assert.That(f.Resources.GetCurrent(ResourceType.Ammo), Is.EqualTo(8));
            Assert.That(f.Event.State, Is.EqualTo(MathEventState.Resolving));
            Assert.That(f.Generator.Binds, Is.EqualTo(1));
            f.Resources.ApplyDelta(result.Resource, result.RewardDelta, "MATH_GATE");
            Assert.That(f.Resources.GetCurrent(ResourceType.Ammo), Is.EqualTo(33));
        }

        [Test]
        public void ResourceIncorrect_ReturnsChosenResultAndZeroReward()
        {
            var f = new Fixture(); f.Open();
            var result = new GateResolutionService(new EmptySink(), 1).Resolve(f.Choose(1), f.Event);
            Assert.That(result.Outcome, Is.EqualTo(GateResolutionOutcome.IncorrectNoReward));
            Assert.That(result.SelectedOptionIndex, Is.EqualTo(0));
            Assert.That(result.OperationResult, Is.EqualTo(40));
            Assert.That(result.RewardDelta, Is.EqualTo(0));
            Assert.That(result.Answer.WasCorrect, Is.False);
            Assert.That(result.Answer.ErrorCategory, Is.EqualTo(ErrorCategory.Unclassified));
            Assert.That(f.Resources.GetCurrent(ResourceType.Ammo), Is.EqualTo(20));
        }

        [TestCase(GateGoal.Maximize, 0, 45, 25)]
        [TestCase(GateGoal.Minimize, 1, 10, -10)]
        [TestCase(GateGoal.ReachTarget, 0, 45, 25)]
        public void ResourceGoal_UsesBindingWinner_IncludingNegativeDelta(GateGoal goal, int lane, int value, int delta)
        {
            var f = new Fixture(goal: goal, decreasing: goal == GateGoal.Minimize); f.Open();
            var result = new GateResolutionService(new EmptySink(), 1).Resolve(f.Choose(lane), f.Event);
            Assert.That(result.WasCorrect, Is.True);
            Assert.That(result.OperationResult, Is.EqualTo(value));
            Assert.That(result.RewardDelta, Is.EqualTo(delta));
        }

        [TestCase(ProblemType.AnswerLaneSelection)]
        [TestCase(ProblemType.MissingOperand)]
        [TestCase(ProblemType.EquationSolving)]
        [TestCase(ProblemType.TargetValueSelection)]
        public void NonResourceCorrect_UsesOptionAndFlatReward(ProblemType type)
        {
            var f = new Fixture(type, resource: ResourceType.Health); f.Open();
            var result = new GateResolutionService(new EmptySink(), 1).Resolve(f.Choose(), f.Event);
            Assert.That(result.Outcome, Is.EqualTo(GateResolutionOutcome.CorrectRewarded));
            Assert.That(result.OperationResult, Is.EqualTo(42));
            Assert.That(result.RewardDelta, Is.EqualTo(30));
            Assert.That(result.Resource, Is.EqualTo(ResourceType.Health));
            Assert.That(result.Answer.ProblemId, Is.EqualTo(f.Event.Problem.Id));
            Assert.That(result.Answer.ErrorCategory, Is.EqualTo(ErrorCategory.None));
            Assert.That(f.Resources.GetCurrent(ResourceType.Health), Is.EqualTo(20));
        }

        [TestCase(ErrorCategory.OnesDigitError)]
        [TestCase(ErrorCategory.OperatorPrecedenceError)]
        [TestCase(null)]
        public void NonResourceIncorrect_CarriesDistractorErrorOrUnclassified(ErrorCategory? category)
        {
            var f = new Fixture(ProblemType.AnswerLaneSelection, distractor: category, incorrectAmount: -2); f.Open();
            var result = new GateResolutionService(new EmptySink(), 1).Resolve(f.Choose(1), f.Event);
            Assert.That(result.Outcome, Is.EqualTo(GateResolutionOutcome.IncorrectNoReward));
            Assert.That(result.RewardDelta, Is.EqualTo(-2));
            Assert.That(result.Answer.ErrorCategory, Is.EqualTo(category ?? ErrorCategory.Unclassified));
            Assert.That(result.Answer.SelectedOptionIndex, Is.EqualTo(0));
            Assert.That(result.Answer.SelectedLane, Is.EqualTo(new LaneIndex(1)));
        }

        [TestCase(ProblemType.ResourceOperationComparison)]
        [TestCase(ProblemType.AnswerLaneSelection)]
        public void Deadline_ProducesOneTimeoutWithFinalizedProblemMetadata(ProblemType type)
        {
            var f = new Fixture(type); f.Open();
            f.Director.Tick(8);
            var service = new GateResolutionService(new EmptySink(), 1);
            var result = service.Resolve(f.Director.PendingResolutions[0], f.Event);
            Assert.That(result.Outcome, Is.EqualTo(GateResolutionOutcome.Expired));
            Assert.That(result.SelectedOptionIndex, Is.EqualTo(-1));
            Assert.That(result.OperationResult, Is.EqualTo(0));
            Assert.That(result.WasCorrect, Is.False);
            Assert.That(result.RewardDelta, Is.EqualTo(0));
            Assert.That(result.Answer.ProblemId, Is.EqualTo(f.Event.ProblemId.Value));
            Assert.That(result.Answer.TimedOut, Is.True);
            Assert.That(result.Answer.SelectedOptionIndex, Is.Null);
            Assert.That(result.Answer.SelectedLane, Is.Null);
            Assert.That(result.Answer.ErrorCategory, Is.EqualTo(ErrorCategory.Timeout));
            Assert.That(result.Answer.ResponseTimeSeconds, Is.EqualTo(6));
            Assert.That(service.IsResolved(f.Event.EventId), Is.True);
            Assert.That(service.Resolve(f.Director.PendingResolutions[0], f.Event).Outcome,
                Is.EqualTo(GateResolutionOutcome.Duplicate));
        }

        [Test]
        public void ChoiceAtDeadline_CannotRewardEvenWithCorrectLane()
        {
            var f = new Fixture(); f.Open();
            var command = new ResolveCommand(f.Event.EventId, new LaneIndex(0), false, 8);
            var result = new GateResolutionService(new EmptySink(), 1).Resolve(command, f.Event);
            Assert.That(result.Outcome, Is.EqualTo(GateResolutionOutcome.Expired));
            Assert.That(result.RewardDelta, Is.EqualTo(0));
            Assert.That(result.Answer.TimedOut, Is.True);
        }

        [TestCase(0)]
        [TestCase(1)]
        public void Replay_IgnoresChangedLaneAndEmitsExactlyOneResolution(int firstLane)
        {
            var sink = new RecordingGameEventSink();
            var f = new Fixture(sink: sink); f.Open();
            var service = new GateResolutionService(sink, 1);
            var command = f.Choose(firstLane);
            service.Resolve(command, f.Event);
            var duplicate = service.Resolve(new ResolveCommand(command.EventId, new LaneIndex(1 - firstLane), false, 4), null);
            Assert.That(duplicate.Outcome, Is.EqualTo(GateResolutionOutcome.Duplicate));
            Assert.That(duplicate.RewardDelta, Is.EqualTo(0));
            Assert.That(duplicate.WasCorrect, Is.False);
            var events = sink.OfType<MathEventResolved>();
            Assert.That(events.Count, Is.EqualTo(1));
            Assert.That(events[0].EventId, Is.EqualTo(command.EventId));
            Assert.That(events[0].SelectedLane, Is.EqualTo(command.SelectedLane));
            Assert.That(events[0].WasCorrect, Is.EqualTo(firstLane == 0));
            Assert.That(events[0].GameTime, Is.EqualTo(command.GameTime));
        }

        [Test]
        public void Restore_ReplacesJournal_CopiesInput_AndAcceptsOtherNewEvents()
        {
            var f = new Fixture(count: 2); f.Open();
            var service = new GateResolutionService(new EmptySink(), 2);
            var first = f.Director.ActiveEvents[0];
            var second = f.Director.ActiveEvents[1];
            var restored = new List<Guid> { first.EventId.Value, first.EventId.Value };
            service.RestoreResolvedEvents(restored);
            restored.Clear();
            Assert.That(service.IsResolved(first.EventId), Is.True);
            Assert.That(service.Resolve(new ResolveCommand(first.EventId, default, false, 3), null).Outcome,
                Is.EqualTo(GateResolutionOutcome.Duplicate));
            Assert.That(service.Resolve(new ResolveCommand(second.EventId, default, false, 3), second).Outcome,
                Is.EqualTo(GateResolutionOutcome.CorrectRewarded));
            service.RestoreResolvedEvents(Array.Empty<Guid>());
            Assert.That(service.IsResolved(first.EventId), Is.False);
            Assert.That(service.IsResolved(second.EventId), Is.False);
            Assert.That(service.Resolve(new ResolveCommand(first.EventId, default, false, 3), first).Outcome,
                Is.EqualTo(GateResolutionOutcome.CorrectRewarded));
        }

        [Test]
        public void SessionIdentities_DoNotCollideAndRestoredReplaysNeedNoTask()
        {
            var first = new Fixture(seed: 7); first.Open();
            var second = new Fixture(seed: 8); second.Open();
            var service = new GateResolutionService(new EmptySink(), 2);
            service.RestoreResolvedEvents(new[] { first.Event.EventId.Value });
            Assert.That(service.IsResolved(second.Event.EventId), Is.False);
            Assert.That(service.Resolve(second.Choose(), second.Event).Outcome, Is.EqualTo(GateResolutionOutcome.CorrectRewarded));
        }

        [Test]
        public void InvalidCommands_DoNotConsumeIdempotencySlot()
        {
            var f = new Fixture(); f.Open();
            var service = new GateResolutionService(new EmptySink(), 1);
            var valid = new ResolveCommand(f.Event.EventId, default, false, 3);
            Assert.That(() => service.Resolve(valid, null), Throws.TypeOf<ArgumentNullException>());
            Assert.That(() => service.Resolve(new ResolveCommand(MathEventId.Create(9, 1), default, false, 3), f.Event),
                Throws.TypeOf<ArgumentException>());
            Assert.That(() => service.Resolve(new ResolveCommand(f.Event.EventId, new LaneIndex(2), false, 3), f.Event),
                Throws.TypeOf<ArgumentException>());
            Assert.That(() => service.Resolve(new ResolveCommand(f.Event.EventId, default, false, double.NaN), f.Event),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => service.Resolve(new ResolveCommand(f.Event.EventId, default, false, 1), f.Event),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(service.IsResolved(f.Event.EventId), Is.False);
            Assert.That(service.Resolve(valid, f.Event).Outcome, Is.EqualTo(GateResolutionOutcome.CorrectRewarded));
        }

        [Test]
        public void PreviewAndSessionEndedEvents_CannotBeResolvedAsChoices()
        {
            var f = new Fixture(); f.Director.Tick(0);
            var service = new GateResolutionService(new EmptySink(), 1);
            Assert.That(() => service.Resolve(new ResolveCommand(f.Event.EventId, default, false, 0), f.Event),
                Throws.TypeOf<InvalidOperationException>());
            f.Director.Tick(1); f.Director.Tick(2); f.Director.OnSessionEnded(3);
            Assert.That(() => service.Resolve(new ResolveCommand(f.Event.EventId, default, false, 3), f.Event),
                Throws.TypeOf<InvalidOperationException>());
            Assert.That(service.IsResolved(f.Event.EventId), Is.False);
        }

        [Test]
        public void ConstructorRestoreAndCapacity_GuardMisuseWithoutGrowingOnResolve()
        {
            Assert.That(() => new GateResolutionService(null, 1), Throws.TypeOf<ArgumentNullException>());
            Assert.That(() => new GateResolutionService(new EmptySink(), -1), Throws.TypeOf<ArgumentOutOfRangeException>());
            var service = new GateResolutionService(new EmptySink(), 0);
            Assert.That(() => service.RestoreResolvedEvents(null), Throws.TypeOf<ArgumentNullException>());
            var f = new Fixture(); f.Open(); var command = f.Choose();
            Assert.That(() => service.Resolve(command, f.Event), Throws.TypeOf<InvalidOperationException>());
            Assert.That(service.IsResolved(f.Event.EventId), Is.False);
        }

        [TestCase(ProblemType.ResourceOperationComparison)]
        [TestCase(ProblemType.AnswerLaneSelection)]
        public void FirstResolutionsAndReplays_AllocateZeroBytesAcrossTenThousandEvents(ProblemType type)
        {
            const int count = 10000;
            var activeEvents = new ActiveMathEvent[count];
            for (int i = 0; i < count; i++)
            {
                var f = new Fixture(type, seed: i); f.Open();
                activeEvents[i] = f.Event;
            }
            var service = new GateResolutionService(new EmptySink(), count);
            int correctCount = 0, duplicateCount = 0;
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < count; i++)
            {
                var active = activeEvents[i];
                var command = new ResolveCommand(active.EventId, new LaneIndex(i % 2), false, 3);
                if (service.Resolve(command, active).WasCorrect) correctCount++;
                if (service.Resolve(command, active).Outcome == GateResolutionOutcome.Duplicate) duplicateCount++;
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.EqualTo(0));
            Assert.That(correctCount, Is.EqualTo(count / 2));
            Assert.That(duplicateCount, Is.EqualTo(count));
        }
    }
}
