using System;
using System.Collections.Generic;
using MathHorde.Core.Common;
using MathHorde.GameLogic;
using NUnit.Framework;

namespace MathHorde.Tests.EditMode.GameLogic
{
    [TestFixture]
    public class MathEventStateMachineTests
    {
        // The accepted graph in ARCHITECTURE.md 5., with ADR-014 session-end edges.
        private static readonly Dictionary<(MathEventState, MathEventTrigger), MathEventState> Edges =
            new Dictionary<(MathEventState, MathEventTrigger), MathEventState>
            {
                [(MathEventState.Inactive, MathEventTrigger.Schedule)] = MathEventState.Preview,
                [(MathEventState.Preview, MathEventTrigger.PreviewElapsed)] = MathEventState.Approaching,
                [(MathEventState.Approaching, MathEventTrigger.DecisionZoneEntered)] = MathEventState.AwaitingDecision,
                [(MathEventState.Approaching, MathEventTrigger.DeadlineReached)] = MathEventState.Expired,
                [(MathEventState.AwaitingDecision, MathEventTrigger.LaneSelected)] = MathEventState.Resolving,
                [(MathEventState.AwaitingDecision, MathEventTrigger.DeadlineReached)] = MathEventState.Expired,
                [(MathEventState.Resolving, MathEventTrigger.RewardApplied)] = MathEventState.Completed,
                [(MathEventState.Inactive, MathEventTrigger.SessionEnded)] = MathEventState.Expired,
                [(MathEventState.Preview, MathEventTrigger.SessionEnded)] = MathEventState.Expired,
                [(MathEventState.Approaching, MathEventTrigger.SessionEnded)] = MathEventState.Expired,
                [(MathEventState.AwaitingDecision, MathEventTrigger.SessionEnded)] = MathEventState.Expired,
                [(MathEventState.Resolving, MathEventTrigger.SessionEnded)] = MathEventState.Expired
            };

        public static IEnumerable<TestCaseData> GraphCases()
        {
            foreach (MathEventState state in Enum.GetValues(typeof(MathEventState)))
            {
                foreach (MathEventTrigger trigger in Enum.GetValues(typeof(MathEventTrigger)))
                {
                    yield return new TestCaseData(state, trigger);
                }
            }
        }

        [TestCaseSource(nameof(GraphCases))]
        public void EveryStateAndTrigger_ObeysContractAndNotifiesExactlyOnce(MathEventState state, MathEventTrigger trigger)
        {
            MathEventStateMachine machine = At(state);
            var notifications = new List<MathEventTransition>();
            machine.Transitioned += notifications.Add;
            bool allowed = Edges.TryGetValue((state, trigger), out MathEventState next);

            Assert.That(machine.CanFire(trigger), Is.EqualTo(allowed));
            Assert.That(machine.State, Is.EqualTo(state), "CanFire must not mutate state.");
            Assert.That(machine.Fire(trigger, 10.5), Is.EqualTo(allowed));
            Assert.That(machine.State, Is.EqualTo(allowed ? next : state));
            Assert.That(notifications.Count, Is.EqualTo(allowed ? 1 : 0));
            if (allowed)
            {
                MathEventTransition transition = notifications[0];
                Assert.That(transition.EventId, Is.EqualTo(machine.EventId));
                Assert.That(transition.From, Is.EqualTo(state));
                Assert.That(transition.To, Is.EqualTo(next));
                Assert.That(transition.Trigger, Is.EqualTo(trigger));
                Assert.That(transition.GameTime, Is.EqualTo(10.5));
            }
        }

        [Test]
        public void DuplicateSelectionAndReward_CannotRepeatResolutionOrCompletion()
        {
            MathEventStateMachine machine = At(MathEventState.AwaitingDecision);
            int resolutionCount = 0;
            int completionCount = 0;
            machine.Transitioned += change =>
            {
                if (change.To == MathEventState.Resolving) resolutionCount++;
                if (change.To == MathEventState.Completed) completionCount++;
            };

            Assert.That(machine.Fire(MathEventTrigger.LaneSelected, 4), Is.True);
            Assert.That(machine.Fire(MathEventTrigger.LaneSelected, 4), Is.False);
            Assert.That(machine.Fire(MathEventTrigger.DeadlineReached, 5), Is.False);
            Assert.That(machine.Fire(MathEventTrigger.RewardApplied, 5), Is.True);
            Assert.That(machine.Fire(MathEventTrigger.RewardApplied, 5), Is.False);
            Assert.That(machine.Fire(MathEventTrigger.SessionEnded, 6), Is.False);
            Assert.That(resolutionCount, Is.EqualTo(1));
            Assert.That(completionCount, Is.EqualTo(1));
            Assert.That(machine.IsTerminal, Is.True);
        }

        [TestCase(MathEventState.Approaching)]
        [TestCase(MathEventState.AwaitingDecision)]
        [TestCase(MathEventState.Resolving)]
        public void ExpiredEvent_RejectsLateSelectionAndReward(MathEventState state)
        {
            MathEventStateMachine machine = At(state);
            MathEventTrigger trigger = state == MathEventState.Resolving
                ? MathEventTrigger.SessionEnded : MathEventTrigger.DeadlineReached;
            Assert.That(machine.Fire(trigger, 5), Is.True);
            Assert.That(machine.Fire(MathEventTrigger.LaneSelected, 6), Is.False);
            Assert.That(machine.Fire(MathEventTrigger.RewardApplied, 6), Is.False);
            Assert.That(machine.State, Is.EqualTo(MathEventState.Expired));
            Assert.That(machine.IsTerminal, Is.True);
        }

        [TestCase(-1.0)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void InvalidGameTime_RejectsWithoutChangingState(double time)
        {
            var machine = new MathEventStateMachine(MathEventId.Create(1, 0));
            Assert.That(() => machine.Fire(MathEventTrigger.Schedule, time), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(machine.State, Is.EqualTo(MathEventState.Inactive));
        }

        [Test]
        public void SharedGameTime_MayBeEqualButCannotMoveBackwards()
        {
            var machine = new MathEventStateMachine(MathEventId.Create(1, 0));
            machine.Fire(MathEventTrigger.Schedule, 2);
            Assert.That(machine.Fire(MathEventTrigger.PreviewElapsed, 2), Is.True);
            Assert.That(() => machine.Fire(MathEventTrigger.DecisionZoneEntered, 1), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(machine.State, Is.EqualTo(MathEventState.Approaching));
        }

        [Test]
        public void UnknownTrigger_IsIgnoredWithoutChangingState()
        {
            MathEventStateMachine machine = At(MathEventState.Preview);
            Assert.That(machine.CanFire((MathEventTrigger)999), Is.False);
            Assert.That(machine.Fire((MathEventTrigger)999, 1), Is.False);
            Assert.That(machine.State, Is.EqualTo(MathEventState.Preview));
        }

        [Test]
        public void TenThousandGameTimeSteps_NeedNoCombatAndNeverPauseSharedClock()
        {
            var clock = new GameClock();
            var pending = At(MathEventState.AwaitingDecision);
            var completed = At(MathEventState.Resolving);
            for (int step = 0; step < 10000; step++)
            {
                clock.Advance(0.01f);
                double time = clock.GameTime;
                if (step == 5000)
                {
                    Assert.That(pending.Fire(MathEventTrigger.DeadlineReached, time), Is.True);
                    Assert.That(completed.Fire(MathEventTrigger.RewardApplied, time), Is.True);
                }
                else
                {
                    Assert.That(pending.Fire(MathEventTrigger.PreviewElapsed, time), Is.False);
                    Assert.That(completed.Fire(MathEventTrigger.Schedule, time), Is.False);
                }
            }
            Assert.That(clock.IsPaused, Is.False);
            Assert.That(clock.GameTime, Is.GreaterThan(99));
            Assert.That(pending.State, Is.EqualTo(MathEventState.Expired));
            Assert.That(completed.State, Is.EqualTo(MathEventState.Completed));
            Assert.That(pending.EventId, Is.EqualTo(completed.EventId));
            Assert.That(pending.State, Is.Not.EqualTo(completed.State), "Event instances must not share lifecycle state.");
        }

        private static MathEventStateMachine At(MathEventState state)
        {
            var machine = new MathEventStateMachine(MathEventId.Create(42, 7));
            var path = new[] { MathEventTrigger.Schedule, MathEventTrigger.PreviewElapsed,
                MathEventTrigger.DecisionZoneEntered, MathEventTrigger.LaneSelected, MathEventTrigger.RewardApplied };
            if (state == MathEventState.Expired)
            {
                machine.Fire(MathEventTrigger.SessionEnded, 0);
                return machine;
            }
            for (int i = 0; i < (int)state; i++) machine.Fire(path[i], 0);
            return machine;
        }
    }
}
