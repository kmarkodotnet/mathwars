using System;
using System.Collections.Generic;
using MathHorde.Core.Common;
using MathHorde.Core.Resources;
using MathHorde.GameLogic;
using MathHorde.GameLogic.Events;
using MathHorde.Tests.EditMode.TestSupport;
using NUnit.Framework;

namespace MathHorde.Tests.EditMode.GameLogic
{
    [TestFixture]
    public class RewardServiceTests
    {
        private sealed class EmptySink : IGameEventSink
        {
            public void Publish<T>(in T gameEvent) where T : struct, IGameEvent { }
        }

        private sealed class ReplaySink : IGameEventSink
        {
            internal RewardService Rewards;
            internal GateResolution Resolution;
            internal RewardApplication Replay;
            internal bool Replayed;
            public void Publish<T>(in T gameEvent) where T : struct, IGameEvent
            {
                if (Rewards == null || Replayed || typeof(T) != typeof(ResourceChanged)) return;
                Replayed = true;
                Replay = Rewards.ApplyReward(Resolution);
            }
        }

        private sealed class Fixture
        {
            internal readonly RecordingGameEventSink Sink = new RecordingGameEventSink();
            internal readonly GameClock Clock = new GameClock();
            internal readonly ResourceService Resources;
            internal readonly RewardService Rewards;
            internal Fixture(int capacity = 4, int shield = 20, int maxShield = 200)
            {
                Resources = new ResourceService(Sink, Clock);
                Resources.Initialize(CombatTestFixtures.BuildLevel(startingShield: shield, maxShield: maxShield,
                    startingHealth: 20, maxHealth: 100));
                Rewards = new RewardService(Resources, Sink, Clock, capacity);
                Clock.Advance(3.5f);
            }
        }

        private static GateResolution Resolution(int index = 0, int delta = 25,
            GateResolutionOutcome outcome = GateResolutionOutcome.CorrectRewarded,
            ResourceType resource = ResourceType.Shield)
            => new GateResolution(MathEventId.Create(7, index), outcome,
                outcome == GateResolutionOutcome.CorrectRewarded, 1, 45, 20, delta, resource, default);

        [Test]
        public void CorrectReward_UsesLiveValueAndRawSnapshotDelta_AndPublishesActualChange()
        {
            var f = new Fixture();
            f.Resources.TryConsume(ResourceType.Shield, 12, "WEAPON_FIRE", out _);
            var result = f.Rewards.ApplyReward(Resolution());
            Assert.That(result.EventId, Is.EqualTo(MathEventId.Create(7, 0)));
            Assert.That(result.Status, Is.EqualTo(RewardApplicationStatus.Applied));
            Assert.That(result.RequestedDelta, Is.EqualTo(25));
            Assert.That(result.AppliedDelta, Is.EqualTo(25));
            Assert.That(result.Resource, Is.EqualTo(ResourceType.Shield));
            Assert.That(f.Resources.GetCurrent(ResourceType.Shield), Is.EqualTo(33));
            Assert.That(f.Rewards.AppliedRewardEventIds.Count, Is.EqualTo(1));
            var events = f.Sink.OfType<RewardApplied>();
            Assert.That(events.Count, Is.EqualTo(1));
            Assert.That(events[0].EventId, Is.EqualTo(result.EventId));
            Assert.That(events[0].Resource, Is.EqualTo(result.Resource));
            Assert.That(events[0].AppliedDelta, Is.EqualTo(25));
            Assert.That(events[0].GameTime, Is.EqualTo(3.5));
            var changes = f.Sink.OfType<ResourceChanged>();
            var change = changes[changes.Count - 1].Change;
            Assert.That(change.PreviousValue, Is.EqualTo(8));
            Assert.That(change.NewValue, Is.EqualTo(33));
            Assert.That(change.ReasonCode, Is.EqualTo("MATH_GATE"));
        }

        [TestCase(190, 50, 10, 200)]
        [TestCase(10, -50, -10, 0)]
        [TestCase(20, -10, -10, 10)]
        [TestCase(200, 25, 0, 200)]
        [TestCase(20, 0, 0, 20)]
        [TestCase(20, int.MaxValue, 180, 200)]
        [TestCase(20, int.MinValue, -20, 0)]
        public void CorrectReward_ReportsRawDeltaAndLiveClamping(int shield, int delta, int applied, int final)
        {
            var f = new Fixture(shield: shield);
            var result = f.Rewards.ApplyReward(Resolution(delta: delta));
            Assert.That(result.Status, Is.EqualTo(RewardApplicationStatus.Applied));
            Assert.That(result.RequestedDelta, Is.EqualTo(delta));
            Assert.That(result.AppliedDelta, Is.EqualTo(applied));
            Assert.That(f.Resources.GetCurrent(ResourceType.Shield), Is.EqualTo(final));
            Assert.That(f.Sink.OfType<RewardApplied>()[0].AppliedDelta, Is.EqualTo(applied));
        }

        [Test]
        public void LiveCapacityChange_DoesNotRecalculateTheRawGateDelta()
        {
            var f = new Fixture(maxShield: 43);
            f.Resources.TryConsume(ResourceType.Shield, 12, "WEAPON_FIRE", out _);
            f.Resources.SetMax(ResourceType.Shield, 30, "UPGRADE");
            var result = f.Rewards.ApplyReward(Resolution());
            Assert.That(result.RequestedDelta, Is.EqualTo(25));
            Assert.That(result.AppliedDelta, Is.EqualTo(22));
            Assert.That(f.Resources.GetCurrent(ResourceType.Shield), Is.EqualTo(30));
        }

        [TestCase(ResourceType.Health)]
        [TestCase(ResourceType.Shield)]
        [TestCase(ResourceType.EnemyArmor)]
        [TestCase(ResourceType.Energy)]
        public void CorrectReward_UsesResolutionResource(ResourceType resource)
        {
            var f = new Fixture();
            int before = f.Resources.GetCurrent(resource);
            var result = f.Rewards.ApplyReward(Resolution(delta: 15, resource: resource));
            Assert.That(result.Resource, Is.EqualTo(resource));
            Assert.That(f.Resources.GetCurrent(resource), Is.EqualTo(before + 15));
            Assert.That(f.Resources.GetCurrent(ResourceType.Shield), Is.EqualTo(resource == ResourceType.Shield ? 35 : 20));
        }

        [TestCase(GateResolutionOutcome.IncorrectNoReward, RewardApplicationStatus.SkippedIncorrect, "INCORRECT")]
        [TestCase(GateResolutionOutcome.Expired, RewardApplicationStatus.SkippedExpired, "EXPIRED")]
        [TestCase(GateResolutionOutcome.Duplicate, RewardApplicationStatus.AlreadyApplied, "ALREADY_APPLIED")]
        public void NonRewardOutcome_NeverMutatesResources_AndCannotBeReplayedAsCorrect(
            GateResolutionOutcome outcome, RewardApplicationStatus status, string reason)
        {
            var f = new Fixture();
            int initialEvents = f.Sink.PublishedCount;
            var result = f.Rewards.ApplyReward(Resolution(delta: -2, outcome: outcome));
            Assert.That(result.Status, Is.EqualTo(status));
            Assert.That(result.RequestedDelta, Is.EqualTo(-2));
            Assert.That(result.AppliedDelta, Is.EqualTo(0));
            Assert.That(f.Sink.PublishedCount, Is.EqualTo(initialEvents + 1));
            var events = f.Sink.OfType<RewardSkipped>();
            Assert.That(events[0].EventId, Is.EqualTo(result.EventId));
            Assert.That(events[0].ReasonCode, Is.EqualTo(reason));
            Assert.That(events[0].GameTime, Is.EqualTo(3.5));
            Assert.That(f.Rewards.ApplyReward(Resolution()).Status, Is.EqualTo(RewardApplicationStatus.AlreadyApplied));
            Assert.That(f.Resources.GetCurrent(ResourceType.Shield), Is.EqualTo(20));
            Assert.That(f.Sink.OfType<RewardApplied>().Count, Is.EqualTo(0));
        }

        [Test]
        public void Replays_IgnoreChangedRewardAndResource_AndPublishSkippedOnly()
        {
            var f = new Fixture(capacity: 1);
            f.Rewards.ApplyReward(Resolution());
            int initialEvents = f.Sink.PublishedCount;
            var replay = f.Rewards.ApplyReward(Resolution(delta: 100, resource: ResourceType.Health));
            Assert.That(replay.Status, Is.EqualTo(RewardApplicationStatus.AlreadyApplied));
            Assert.That(replay.RequestedDelta, Is.EqualTo(100));
            Assert.That(replay.AppliedDelta, Is.EqualTo(0));
            Assert.That(f.Sink.PublishedCount, Is.EqualTo(initialEvents + 1));
            Assert.That(f.Sink.OfType<RewardSkipped>()[0].ReasonCode, Is.EqualTo("ALREADY_APPLIED"));
            Assert.That(f.Resources.GetCurrent(ResourceType.Shield), Is.EqualTo(45));
            Assert.That(f.Resources.GetCurrent(ResourceType.Health), Is.EqualTo(20));
            Assert.That(f.Rewards.AppliedRewardEventIds.Count, Is.EqualTo(1));
        }

        [Test]
        public void SynchronousResourceEventReplay_CannotCreditTwice()
        {
            var sink = new ReplaySink(); var clock = new GameClock();
            var resources = new ResourceService(sink, clock);
            resources.Initialize(CombatTestFixtures.BuildLevel(startingShield: 20, maxShield: 200));
            sink.Rewards = new RewardService(resources, sink, clock, 1);
            sink.Resolution = Resolution();
            var result = sink.Rewards.ApplyReward(sink.Resolution);
            Assert.That(result.Status, Is.EqualTo(RewardApplicationStatus.Applied));
            Assert.That(sink.Replayed, Is.True);
            Assert.That(sink.Replay.Status, Is.EqualTo(RewardApplicationStatus.AlreadyApplied));
            Assert.That(resources.GetCurrent(ResourceType.Shield), Is.EqualTo(45));
        }

        [Test]
        public void Restore_CopiesAndDeduplicatesInput_ReplacesJournalAndReservesNewEvents()
        {
            var f = new Fixture(capacity: 1);
            f.Rewards.ApplyReward(Resolution());
            var saved = new List<Guid> { MathEventId.Create(7, 1).Value, MathEventId.Create(7, 1).Value };
            int before = f.Sink.PublishedCount;
            f.Rewards.RestoreAppliedRewards(saved);
            saved.Clear();
            Assert.That(f.Sink.PublishedCount, Is.EqualTo(before));
            Assert.That(f.Rewards.AppliedRewardEventIds.Count, Is.EqualTo(1));
            Assert.That(f.Rewards.ApplyReward(Resolution(1)).Status, Is.EqualTo(RewardApplicationStatus.AlreadyApplied));
            Assert.That(f.Rewards.ApplyReward(Resolution()).Status, Is.EqualTo(RewardApplicationStatus.Applied));
            Assert.That(f.Resources.GetCurrent(ResourceType.Shield), Is.EqualTo(70));
            f.Rewards.RestoreAppliedRewards(Array.Empty<Guid>());
            Assert.That(f.Rewards.AppliedRewardEventIds.Count, Is.EqualTo(0));
            Assert.That(f.Rewards.ApplyReward(Resolution()).Status, Is.EqualTo(RewardApplicationStatus.Applied));
        }

        [Test]
        public void SavedJournal_RestoresIntoAnotherService_AndCannotBeMutatedByReaders()
        {
            var f = new Fixture();
            var journal = f.Rewards.AppliedRewardEventIds;
            f.Rewards.ApplyReward(Resolution());
            f.Rewards.ApplyReward(Resolution(1, outcome: GateResolutionOutcome.IncorrectNoReward));
            Assert.That(journal.Count, Is.EqualTo(2));
            Assert.That(() => ((ICollection<Guid>)journal).Clear(), Throws.TypeOf<NotSupportedException>());
            IRewardService restored = new RewardService(f.Resources, f.Sink, f.Clock, 1);
            restored.RestoreAppliedRewards(journal);
            Assert.That(restored.ApplyReward(Resolution()).Status, Is.EqualTo(RewardApplicationStatus.AlreadyApplied));
            Assert.That(restored.ApplyReward(Resolution(1)).Status, Is.EqualTo(RewardApplicationStatus.AlreadyApplied));
            Assert.That(f.Resources.GetCurrent(ResourceType.Shield), Is.EqualTo(45));
        }

        [TestCase(ResourceType.Shield, 1, "COMBAT_REWARD", 20, 22)]
        [TestCase(ResourceType.Health, 15, "BOSS_PHASE", 20, 50)]
        public void CombatReward_AlwaysDelegatesWithoutMathJournal(
            ResourceType resource, int delta, string reason, int initial, int final)
        {
            var f = new Fixture(capacity: 0);
            Assert.That(f.Resources.GetCurrent(resource), Is.EqualTo(initial));
            f.Rewards.ApplyCombatReward(resource, delta, reason);
            var result = f.Rewards.ApplyCombatReward(resource, delta, reason);
            Assert.That(result.RequestedDelta, Is.EqualTo(delta));
            Assert.That(result.ReasonCode, Is.EqualTo(reason));
            Assert.That(f.Resources.GetCurrent(resource), Is.EqualTo(final));
            Assert.That(f.Rewards.AppliedRewardEventIds.Count, Is.EqualTo(0));
            Assert.That(f.Sink.OfType<RewardApplied>().Count, Is.EqualTo(0));
            Assert.That(f.Sink.OfType<RewardSkipped>().Count, Is.EqualTo(0));
        }

        [Test]
        public void ConstructorAndRestore_RejectInvalidInputs()
        {
            var f = new Fixture();
            Assert.That(() => new RewardService(null, f.Sink, f.Clock, 1), Throws.TypeOf<ArgumentNullException>());
            Assert.That(() => new RewardService(f.Resources, null, f.Clock, 1), Throws.TypeOf<ArgumentNullException>());
            Assert.That(() => new RewardService(f.Resources, f.Sink, null, 1), Throws.TypeOf<ArgumentNullException>());
            Assert.That(() => new RewardService(f.Resources, f.Sink, f.Clock, -1), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => f.Rewards.RestoreAppliedRewards(null), Throws.TypeOf<ArgumentNullException>());
            Assert.That(() => f.Rewards.ApplyReward(Resolution(outcome: (GateResolutionOutcome)99)),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(f.Rewards.AppliedRewardEventIds.Count, Is.EqualTo(0));
        }

        [Test]
        public void CapacityExhaustion_FailsBeforeAnyMutation_AndRestoringCanReserveAgain()
        {
            var f = new Fixture(capacity: 1);
            f.Rewards.ApplyReward(Resolution());
            int before = f.Sink.PublishedCount;
            Assert.That(() => f.Rewards.ApplyReward(Resolution(1)), Throws.TypeOf<InvalidOperationException>());
            Assert.That(f.Rewards.AppliedRewardEventIds.Count, Is.EqualTo(1));
            Assert.That(f.Sink.PublishedCount, Is.EqualTo(before));
            Assert.That(f.Resources.GetCurrent(ResourceType.Shield), Is.EqualTo(45));
            f.Rewards.RestoreAppliedRewards(f.Rewards.AppliedRewardEventIds);
            Assert.That(f.Rewards.ApplyReward(Resolution(1)).Status, Is.EqualTo(RewardApplicationStatus.Applied));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TenThousandFirstRewardsAndReplays_WithRealResourceMutations_AllocateZeroBytes(bool restore)
        {
            const int count = 10000;
            // Measure the hot path after runtime/generic dispatch initialization.
            // A separate journal preserves ALL 10000 first applications below;
            // neither their fresh-service state nor the strict zero-byte oracle changes.
            var warmSink = new EmptySink(); var warmClock = new GameClock();
            var warmResources = new ResourceService(warmSink, warmClock);
            warmResources.Initialize(CombatTestFixtures.BuildLevel(startingShield: 20, maxShield: 100000));
            var warmRewards = new RewardService(warmResources, warmSink, warmClock, 100);
            for (int i = 0; i < 100; i++)
            {
                var warmResolution = Resolution(i, 2, (GateResolutionOutcome)(i % 4));
                warmRewards.ApplyReward(warmResolution); warmRewards.ApplyReward(warmResolution);
                warmRewards.ApplyCombatReward(ResourceType.Shield, 1, "COMBAT_REWARD");
                warmResources.TryConsume(ResourceType.Shield, 1, "WEAPON_FIRE", out _);
                warmResources.GetMax(ResourceType.Shield); warmResources.CaptureSnapshot(ResourceType.Shield);
            }
            var sink = new EmptySink(); var clock = new GameClock();
            var resources = new ResourceService(sink, clock);
            resources.Initialize(CombatTestFixtures.BuildLevel(startingShield: 20, maxShield: 100000));
            var rewards = new RewardService(resources, sink, clock, count);
            if (restore) rewards.RestoreAppliedRewards(new[] { MathEventId.Create(8, 0).Value });
            int applied = 0, duplicate = 0;
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < count; i++)
            {
                var resolution = Resolution(i, 2, (GateResolutionOutcome)(i % 4));
                if (rewards.ApplyReward(resolution).Status == RewardApplicationStatus.Applied) applied++;
                if (rewards.ApplyReward(resolution).Status == RewardApplicationStatus.AlreadyApplied) duplicate++;
                rewards.ApplyCombatReward(ResourceType.Shield, 1, "COMBAT_REWARD");
                resources.TryConsume(ResourceType.Shield, 1, "WEAPON_FIRE", out _);
                resources.GetMax(ResourceType.Shield);
                resources.CaptureSnapshot(ResourceType.Shield);
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.EqualTo(0));
            Assert.That(applied, Is.EqualTo(count / 4));
            Assert.That(duplicate, Is.EqualTo(count));
            Assert.That(rewards.AppliedRewardEventIds.Count, Is.EqualTo(count + (restore ? 1 : 0)));
            Assert.That(resources.GetCurrent(ResourceType.Shield), Is.EqualTo(5020));
        }
    }
}
