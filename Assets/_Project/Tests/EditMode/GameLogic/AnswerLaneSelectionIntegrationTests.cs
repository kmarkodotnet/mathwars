using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
    public class AnswerLaneSelectionIntegrationTests
    {
        [TestCase(2, 2)]
        [TestCase(2, 3)]
        [TestCase(2, 4)]
        [TestCase(3, 3)]
        [TestCase(3, 4)]
        [TestCase(4, 4)]
        public void GeneratedOptions_KeepTheirEarlyPreviewAndLaneThroughResolution(int count, int laneCount)
        {
            // Exercise every offered choice, including every wrong answer, with real services.
            for (int selected = 0; selected < count; selected++)
            {
                var sink = new RecordingGameEventSink();
                var f = new Fixture(count, laneCount, sink, 306000 + 10 * count + laneCount);
                var original = f.Generator.Problem;
                Assert.That(new ProblemValidator().IsValid(original, f.Generator.Request.Difficulty), Is.True);
                var replay = new MathProblemGenerator().Generate(f.Generator.Request);
                Assert.That(MathProblemGeneratorTests.Signature(replay.Value), Is.EqualTo(MathProblemGeneratorTests.Signature(original)));

                f.Director.Tick(0);
                var active = f.Director.ActiveEvents[0];
                Assert.That(active.State, Is.EqualTo(MathEventState.Preview));
                Assert.That(active.Problem, Is.SameAs(original));
                Assert.That(active.Carriers.Count, Is.EqualTo(0));
                Assert.That(active.ResourceGate, Is.Null);
                var preview = sink.OfType<MathEventPreviewStarted>().Single();
                Assert.That(preview.ProblemId, Is.EqualTo(original.Id));
                Assert.That(preview.DisplayExpression, Is.EqualTo(original.Expression.ToDisplayString() + " = ?"));

                f.Resources.ApplyDelta(ResourceType.Ammo, -7, "WEAPON_FIRE");
                Assert.That(f.Director.PrepareUpcomingEvents(0).IsSuccess, Is.True);
                f.Director.Tick(.999);
                Assert.That(active.State, Is.EqualTo(MathEventState.Preview));
                Assert.That(active.Carriers.Count, Is.EqualTo(0));
                f.Director.Tick(1);
                Assert.That(active.State, Is.EqualTo(MathEventState.Approaching));
                Assert.That(active.Carriers.Count, Is.EqualTo(count));
                Assert.That(active.Problem, Is.SameAs(original));
                Assert.That(original.Reward.FlatAmount, Is.EqualTo(30));
                Assert.That(original.Reward.UseSnapshotDelta, Is.False);
                Assert.That(original.ResourceCapacity, Is.Null);

                foreach (var carrier in active.Carriers)
                {
                    var option = original.Options.Single(o => o.OptionIndex == carrier.OptionIndex);
                    Assert.That(carrier.Lane, Is.EqualTo(option.Lane));
                    Assert.That(carrier.Position.X, Is.EqualTo(option.Lane.Value));
                    Assert.That(carrier.DisplayLabel, Is.EqualTo(option.Value.ToDisplayString()));
                    Assert.That(carrier.IsCorrect, Is.EqualTo(option.IsCorrect));
                    Assert.That(option.GateOperation, Is.Null);
                }
                Assert.That(active.Carriers.Select(c => c.Lane).Distinct().Count(), Is.EqualTo(count));
                Assert.That(active.Carriers.All(c => c.Lane.Value < count), Is.True);

                f.Resources.ApplyDelta(ResourceType.Ammo, -6, "WEAPON_FIRE");
                f.Director.Tick(2);
                Assert.That(active.State, Is.EqualTo(MathEventState.AwaitingDecision));
                if (laneCount > count)
                {
                    f.Director.OnPlayerLaneSelected(new LaneIndex(count), 2);
                    Assert.That(active.State, Is.EqualTo(MathEventState.AwaitingDecision));
                    Assert.That(f.Director.PendingResolutions.Count, Is.EqualTo(0));
                }
                var choice = original.Options[selected];
                f.Director.OnPlayerLaneSelected(choice.Lane, 2);
                Assert.That(f.Director.PendingResolutions.Count, Is.EqualTo(1));
                var command = f.Director.PendingResolutions[0];
                Assert.That(command.SelectedLane, Is.EqualTo(choice.Lane));
                var resolution = f.Resolver.Resolve(command, active);
                Assert.That(resolution.SelectedOptionIndex, Is.EqualTo(choice.OptionIndex));
                Assert.That(resolution.OperationResult, Is.EqualTo(choice.Value.Value));
                Assert.That(resolution.WasCorrect, Is.EqualTo(choice.IsCorrect));
                Assert.That(resolution.RewardDelta, Is.EqualTo(choice.IsCorrect ? 30 : 0));
                Assert.That(resolution.Outcome, Is.EqualTo(choice.IsCorrect
                    ? GateResolutionOutcome.CorrectRewarded : GateResolutionOutcome.IncorrectNoReward));
                var application = f.Rewards.ApplyReward(resolution);
                f.Director.OnRewardProcessed(application, 2);
                Assert.That(active.State, Is.EqualTo(MathEventState.Completed));
                Assert.That(f.Resources.GetCurrent(ResourceType.Ammo), Is.EqualTo(choice.IsCorrect ? 37 : 7));
                Assert.That(f.Generator.Generates, Is.EqualTo(1));
                Assert.That(f.Generator.ResourceCalls, Is.EqualTo(0));
                Assert.That(active.Problem, Is.SameAs(original));
                Assert.That(sink.OfType<MathEventPreviewStarted>().Count, Is.EqualTo(1));
                Assert.That(sink.OfType<MathEventGatesSpawned>().Count, Is.EqualTo(1));
            }
        }

        [TestCase(2, true)]
        [TestCase(3, true)]
        [TestCase(4, true)]
        [TestCase(2, false)]
        [TestCase(3, false)]
        [TestCase(4, false)]
        public void PreparedLaneLifecycle_AllocatesZeroIncludingCarriersResolutionAndReward(int count, bool correct)
        {
            var warm = new Fixture(count, 4, new EmptySink(), 306100);
            ExerciseLifecycle(warm, correct);
            var f = new Fixture(count, 4, new EmptySink(), 306101);
            long before = GC.GetAllocatedBytesForCurrentThread();
            ExerciseLifecycle(f, correct);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.EqualTo(0));
            Assert.That(f.Generator.Generates, Is.EqualTo(1));
            Assert.That(f.Generator.ResourceCalls, Is.EqualTo(0));
            Assert.That(f.Director.ActiveEvents.Count, Is.EqualTo(0));
            Assert.That(f.Resources.GetCurrent(ResourceType.Ammo), Is.EqualTo(correct ? 50 : 20));
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void Generate_MediumDifficultyAverageStaysBelowTwoMilliseconds(int count)
        {
            const int samples = 10000;
            var generator = new MathProblemGenerator();
            for (int i = 0; i < 64; i++) generator.Generate(MathProblemGeneratorTests.Request(seed: 306200 + i, count: count));
            long elapsedTicks = 0;
            for (int i = 0; i < samples; i++)
            {
                var request = MathProblemGeneratorTests.Request(seed: 316000 + i, count: count);
                long before = Stopwatch.GetTimestamp();
                var result = generator.Generate(request);
                elapsedTicks += Stopwatch.GetTimestamp() - before;
                Assert.That(result.IsSuccess, Is.True, "seed=" + request.Seed);
            }
            double averageMs = elapsedTicks * 1000d / Stopwatch.Frequency / samples;
            TestContext.WriteLine("AnswerLaneSelection CL4 count=" + count + " N=" + samples
                + " seeds=316000..325999 averageMs=" + averageMs);
            Assert.That(averageMs, Is.LessThan(2d));
        }

        private static void ExerciseLifecycle(Fixture f, bool correct)
        {
            f.Director.Tick(0); f.Director.Tick(1); f.Director.Tick(1.5); f.Director.Tick(2);
            var active = f.Director.ActiveEvents[0];
            for (int i = 0; i < active.Problem.Options.Count; i++)
            {
                var option = active.Problem.Options[i];
                if (option.IsCorrect != correct) continue;
                f.Director.OnPlayerLaneSelected(option.Lane, 2);
                break;
            }
            var resolution = f.Resolver.Resolve(f.Director.PendingResolutions[0], active);
            var application = f.Rewards.ApplyReward(resolution);
            f.Director.OnRewardProcessed(application, 2);
            f.Director.Tick(3);
        }

        private sealed class Fixture
        {
            internal readonly CountingGenerator Generator = new CountingGenerator();
            internal readonly MathEventDirector Director;
            internal readonly ResourceService Resources;
            internal readonly GateResolutionService Resolver;
            internal readonly RewardService Rewards;

            internal Fixture(int count, int laneCount, IGameEventSink sink, int seed)
            {
                var clock = new GameClock();
                var level = CombatTestFixtures.BuildLevel(laneCount: laneCount, mathEvents: new[]
                {
                    new MathEventScheduleEntry(0, ProblemType.AnswerLaneSelection, 1f, 0f, ResourceType.Ammo, GateGoal.Maximize)
                });
                var settings = new MathEventDirectorSettings(1, 5f, 3f, 2f, 1f,
                    new Dictionary<int, RewardSpec> { [0] = new RewardSpec(ResourceType.Ammo, 30, false) });
                Resources = new ResourceService(sink, clock);
                Resources.Initialize(level);
                Resolver = new GateResolutionService(sink, 1);
                Rewards = new RewardService(Resources, sink, clock, 1);
                Director = new MathEventDirector(Resources, sink, settings);
                Director.Initialize(level, MathProblemGeneratorTests.Request(count: count).Difficulty,
                    Generator, new DeterministicRandomSource(seed));
                Assert.That(Director.PrepareUpcomingEvents(0).IsSuccess, Is.True);
            }
        }

        private sealed class CountingGenerator : IMathProblemGenerator
        {
            private readonly MathProblemGenerator _inner = new MathProblemGenerator();
            internal int Generates, ResourceCalls;
            internal ProblemGenerationRequest Request;
            internal MathProblem Problem;
            public Result<MathProblem> Generate(ProblemGenerationRequest request)
            {
                Generates++; Request = request;
                var result = _inner.Generate(request);
                if (result.IsSuccess) Problem = result.Value;
                return result;
            }
            public IReadOnlyList<Result<MathProblem>> GenerateBatch(ProblemGenerationRequest request, int count)
                => _inner.GenerateBatch(request, count);
            public Result<PreparedResourceGate> PrepareResourceGate(ResourceGatePreparationRequest request)
            { ResourceCalls++; return _inner.PrepareResourceGate(request); }
            public Result<ResourceGateBinding> BindResourceGate(PreparedResourceGate prepared, int snapshot, int capacity)
            { ResourceCalls++; return _inner.BindResourceGate(prepared, snapshot, capacity); }
        }

        private sealed class EmptySink : IGameEventSink
        { public void Publish<T>(in T gameEvent) where T : struct, IGameEvent { } }
    }
}
