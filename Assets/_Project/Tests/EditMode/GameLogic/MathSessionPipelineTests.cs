using System;
using System.Collections.Generic;
using MathHorde.Analytics;
using MathHorde.Core.Common;
using MathHorde.Core.Levels;
using MathHorde.Core.Problems;
using MathHorde.Core.Progress;
using MathHorde.Core.Resources;
using MathHorde.GameLogic;
using MathHorde.GameLogic.Events;
using MathHorde.MathEngine;
using MathHorde.Tests.EditMode.TestSupport;
using NUnit.Framework;

namespace MathHorde.Tests.EditMode.GameLogic
{
    public class MathSessionPipelineTests
    {
        private sealed class Sink : IGameEventSink { public void Publish<T>(in T e) where T : struct, IGameEvent { } }
        private sealed class Fixture
        {
            internal readonly GameClock Clock = new GameClock();
            internal readonly MathEventDirector Director;
            internal readonly GameSessionService Session;
            internal readonly SessionAttemptRecorder Recorder = new SessionAttemptRecorder(2);
            internal readonly ResourceService Resources;
            internal Fixture()
            {
                var sink = new Sink();
                var entries = new[] { new MathEventScheduleEntry(0, ProblemType.ResourceOperationComparison, 1, 6, ResourceType.Health, GateGoal.Maximize),
                    new MathEventScheduleEntry(10, ProblemType.AnswerLaneSelection, 1, 6, ResourceType.Health, GateGoal.Maximize) };
                var level = CombatTestFixtures.BuildLevel(startingHealth: 8, mathEvents: entries,
                    waves: new[] { new WaveDefinition(0, 0, 200, Array.Empty<WaveSpawnEntry>()) });
                Resources = new ResourceService(sink, Clock);
                Director = new MathEventDirector(Resources, sink, new MathEventDirectorSettings(1, 5, 3, 2, 1,
                    new GateGenerationLimits(2, 50, 2, 4), new Dictionary<int, RewardSpec>
                    { [0] = new RewardSpec(ResourceType.Health, 30), [1] = new RewardSpec(ResourceType.Health, 15) }));
                Director.Initialize(level, MathEngine.ResourceGatePreparationTests.Profile(), new MathProblemGenerator(), new DeterministicRandomSource(12));
                Session = new GameSessionService(Clock, new WaveDirector(), new CombatService(Resources, sink), Director, Resources, sink,
                    new GateResolutionService(sink, 2), new RewardService(Resources, sink, Clock, 2), Recorder.Record, () => Recorder.Attempts);
                Session.StartSession(level, new[] { CombatTestFixtures.BuildWeapon() }, CombatTestFixtures.BuildCatalog(), 12);
                Recorder.Begin(Session.SessionId, DateTimeOffset.UnixEpoch);
                Assert.That(Director.PrepareUpcomingEvents(0).IsSuccess, Is.True);
            }
        }
        private static void Exercise(Fixture f)
        {
            for (int frame = 0; frame < 2000; frame++)
            {
                f.Session.Tick(.02f);
                for (int i = 0; i < f.Director.ActiveEvents.Count; i++)
                {
                    var item = f.Director.ActiveEvents[i];
                    if (item.State != MathEventState.AwaitingDecision) continue;
                    for (int j = 0; j < item.Problem.Options.Count; j++)
                        if (item.Problem.Options[j].IsCorrect) { f.Director.OnPlayerLaneSelected(item.Problem.Options[j].Lane, f.Clock.GameTime); break; }
                }
            }
        }
        [Test]
        public void WholeSessionTick_IncludesFirstResolutionRewardAndAnswerBuffer_WithoutAllocations()
        {
            Exercise(new Fixture());
            var f = new Fixture();
            long before = GC.GetAllocatedBytesForCurrentThread();
            Exercise(f);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.EqualTo(0), "Includes first awards, completion, terminal removal and external lane commands.");
            Assert.That(f.Resources.GetCurrent(ResourceType.Health), Is.EqualTo(53));
            Assert.That(f.Recorder.Attempts.Count, Is.Zero, "Materialization is external to Tick.");
            f.Recorder.MaterializePending();
            Assert.That(f.Recorder.Attempts.Count, Is.EqualTo(2));
            f.Recorder.MaterializePending();
            var record = f.Session.Finalize(SessionResult.Victory, FailureReason.None);
            Assert.That(record.TotalQuestions, Is.EqualTo(2)); Assert.That(record.CorrectQuestions, Is.EqualTo(2));
            Assert.That(record.Attempts.Count, Is.EqualTo(2)); Assert.That(record.AppliedRewardEventIds.Count, Is.EqualTo(2));
        }
    }
}
