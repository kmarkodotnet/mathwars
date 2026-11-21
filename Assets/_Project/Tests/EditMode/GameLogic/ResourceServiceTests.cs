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
    // Contract game-logic.md 2. — ResourceService is the single mutation
    // path for Shield/Health/Shield (spec 4.3), and its 2.2 reference scenario
    // (20 shield -> snapshot -> 12 consumed -> +25 gate delta -> 33) is the
    // primary acceptance test for the whole F1-04 package.
    [TestFixture]
    public class ResourceServiceTests
    {
        private static LevelDefinition BuildLevel(int startingShield = 20, int shieldCapacity = 200, int startingHealth = 100, int maxHealth = 100, int? maxShield = null)
        {
            return new LevelDefinition(
                id: new LevelId("w1-l01"),
                worldId: new WorldId("w1"),
                displayNameKey: "level.w1l01.name",
                laneCount: 3,
                targetDurationSeconds: 200,
                waves: null,
                mathEvents: null,
                bossEnemyId: null,
                startingHealth: startingHealth,
                maxHealth: maxHealth,
                startingShield: maxShield == 0 ? 0 : startingShield,
                maxShield: maxShield ?? shieldCapacity,
                requiredMathAccuracy: 0f,
                weaponId: "basic_weapon", availableWeaponIds: new[] { "basic_weapon" });
        }

        private static ResourceService CreateService(out RecordingGameEventSink sink, out GameClock clock)
        {
            sink = new RecordingGameEventSink();
            clock = new GameClock();
            return new ResourceService(sink, clock);
        }

        [Test]
        public void Initialize_FillsPoolsFromLevelDefinition()
        {
            var service = CreateService(out _, out _);

            service.Initialize(BuildLevel(startingShield: 20, shieldCapacity: 200, startingHealth: 100, maxHealth: 100));

            Assert.That(service.GetCurrent(ResourceType.Shield), Is.EqualTo(20));
            Assert.That(service.GetMax(ResourceType.Shield), Is.EqualTo(200));
            Assert.That(service.GetCurrent(ResourceType.Health), Is.EqualTo(100));
            Assert.That(service.GetMax(ResourceType.Health), Is.EqualTo(100));
        }

        [Test]
        public void Initialize_MaxShieldZero_CreatesNoShieldPoolAndReadsAsZero()
        {
            // Contract game-logic.md 2.2: MaxShield == 0 means NO shield pool
            // is created at all (not a Max = max(StartingShield, 1) workaround).
            var service = CreateService(out _, out _);

            service.Initialize(BuildLevel(maxShield: 0));

            Assert.That(service.GetCurrent(ResourceType.Shield), Is.EqualTo(0));
            Assert.That(service.GetMax(ResourceType.Shield), Is.EqualTo(0));
        }

        [Test]
        public void Initialize_MaxShieldPositive_CreatesShieldPoolWithThatMax()
        {
            var service = CreateService(out _, out _);

            service.Initialize(BuildLevel(maxShield: 50));

            Assert.That(service.GetMax(ResourceType.Shield), Is.EqualTo(50));
        }

        [Test]
        public void Initialize_PublishesLevelInitResourceChangedEvents()
        {
            var service = CreateService(out var sink, out _);

            service.Initialize(BuildLevel());

            var shieldInitEvents = sink.OfType<ResourceChanged>();
            Assert.That(shieldInitEvents.Count, Is.EqualTo(2), "Health + Shield egy LEVEL_INIT eseményt kap egyenként.");
            foreach (var evt in shieldInitEvents)
            {
                Assert.That(evt.Change.ReasonCode, Is.EqualTo("LEVEL_INIT"));
            }
        }

        [Test]
        public void TryConsume_InsufficientShield_ReturnsFalseAndLeavesStateUnchanged()
        {
            // "nulla lőszerrel nincs lövés": starting shield is driven to 0, then
            // a further shot must fail without mutating state.
            var service = CreateService(out var sink, out _);
            service.Initialize(BuildLevel(startingShield: 0));
            int publishedBeforeAttempt = sink.PublishedCount;

            bool consumed = service.TryConsume(ResourceType.Shield, 1, "WEAPON_FIRE", out var change);

            Assert.That(consumed, Is.False);
            Assert.That(service.GetCurrent(ResourceType.Shield), Is.EqualTo(0));
            Assert.That(sink.PublishedCount, Is.EqualTo(publishedBeforeAttempt), "Sikertelen fogyasztás nem publikál ResourceChanged-et.");
        }

        [Test]
        public void TryConsume_SufficientShield_DecrementsAndReturnsTrue()
        {
            var service = CreateService(out _, out _);
            service.Initialize(BuildLevel(startingShield: 20));

            bool consumed = service.TryConsume(ResourceType.Shield, 1, "WEAPON_FIRE", out var change);

            Assert.That(consumed, Is.True);
            Assert.That(change.PreviousValue, Is.EqualTo(20));
            Assert.That(change.NewValue, Is.EqualTo(19));
            Assert.That(change.AppliedDelta, Is.EqualTo(-1));
            Assert.That(service.GetCurrent(ResourceType.Shield), Is.EqualTo(19));
        }

        [Test]
        public void TryConsume_ExactlyAvailableAmount_SucceedsAndDepletesToZero()
        {
            var service = CreateService(out _, out _);
            service.Initialize(BuildLevel(startingShield: 5));

            bool consumed = service.TryConsume(ResourceType.Shield, 5, "WEAPON_FIRE", out var change);

            Assert.That(consumed, Is.True);
            Assert.That(service.GetCurrent(ResourceType.Shield), Is.EqualTo(0));
        }

        [Test]
        public void ApplyDelta_AboveMax_ClampsAndReportsWasClamped()
        {
            var service = CreateService(out _, out _);
            service.Initialize(BuildLevel(startingShield: 190, shieldCapacity: 200));

            var change = service.ApplyDelta(ResourceType.Shield, 50, "PICKUP");

            Assert.That(change.NewValue, Is.EqualTo(200));
            Assert.That(change.RequestedDelta, Is.EqualTo(50));
            Assert.That(change.AppliedDelta, Is.EqualTo(10));
            Assert.That(change.WasClamped, Is.True);
        }

        [Test]
        public void ApplyDelta_BelowMin_ClampsToZeroAndReportsWasClamped()
        {
            var service = CreateService(out _, out _);
            service.Initialize(BuildLevel(startingHealth: 10, maxHealth: 100));

            var change = service.ApplyDelta(ResourceType.Health, -50, "ENEMY_CONTACT");

            Assert.That(change.NewValue, Is.EqualTo(0));
            Assert.That(change.AppliedDelta, Is.EqualTo(-10));
            Assert.That(change.WasClamped, Is.True);
        }

        [Test]
        public void SetMax_LowerThanCurrent_ClampsCurrentDown()
        {
            var service = CreateService(out _, out _);
            service.Initialize(BuildLevel(startingShield: 20, shieldCapacity: 200));

            var change = service.SetMax(ResourceType.Shield, 10, "LEVEL_INIT");

            Assert.That(service.GetMax(ResourceType.Shield), Is.EqualTo(10));
            Assert.That(service.GetCurrent(ResourceType.Shield), Is.EqualTo(10));
            Assert.That(change.WasClamped, Is.True);
        }

        [Test]
        public void CaptureSnapshot_ReturnsCurrentValue()
        {
            var service = CreateService(out _, out _);
            service.Initialize(BuildLevel(startingShield: 20));

            int snapshot = service.CaptureSnapshot(ResourceType.Shield);

            Assert.That(snapshot, Is.EqualTo(20));
        }

        [Test]
        public void State_IsCachedUntilMutation_AndPreviousSnapshotsRemainImmutable()
        {
            var service = CreateService(out _, out _);
            service.Initialize(BuildLevel(startingShield: 20));
            var original = service.State;
            Assert.That(service.State, Is.SameAs(original));
            service.ApplyDelta(ResourceType.Shield, 25, "MATH_GATE");
            var updated = service.State;
            Assert.That(updated.GetCurrent(ResourceType.Shield), Is.EqualTo(45));
            Assert.That(original.GetCurrent(ResourceType.Shield), Is.EqualTo(20));
            Assert.That(service.State, Is.SameAs(updated));
            service.SetMax(ResourceType.Shield, 30, "UPGRADE");
            Assert.That(service.State.GetCurrent(ResourceType.Shield), Is.EqualTo(30));
            Assert.That(updated.GetMax(ResourceType.Shield), Is.EqualTo(200));
            service.Initialize(BuildLevel(startingHealth: 10, maxShield: 0));
            Assert.That(service.State.GetCurrent(ResourceType.Health), Is.EqualTo(10));
            Assert.That(service.State.Pools.ContainsKey(ResourceType.Shield), Is.False);
            Assert.That(original.Pools.ContainsKey(ResourceType.Shield), Is.True);
        }

        [Test]
        public void UnconfiguredPools_KeepZeroReadsAndCanBeAddedWithoutChangingOldSnapshot()
        {
            var service = CreateService(out _, out _);
            var empty = service.State;
            Assert.That(empty.Pools.Count, Is.EqualTo(0));
            Assert.That(service.GetCurrent(ResourceType.Energy), Is.EqualTo(0));
            Assert.That(service.GetMax(ResourceType.Energy), Is.EqualTo(0));
            service.ApplyDelta(ResourceType.Energy, 10, "MATH_GATE");
            Assert.That(service.GetCurrent(ResourceType.Energy), Is.EqualTo(10));
            Assert.That(service.GetMax(ResourceType.Energy), Is.EqualTo(int.MaxValue));
            Assert.That(service.State.Pools.Count, Is.EqualTo(1));
            Assert.That(empty.Pools.Count, Is.EqualTo(0));
        }

        [Test]
        public void SnapshotBasedGateReward_ReferenceScenario_MatchesContract()
        {
            // Contract game-logic.md 2.2:
            //   Initialize: Shield = 20 (Max 200)
            //   CaptureSnapshot(Shield) -> 20
            //   harc: TryConsume(Shield, 1, "WEAPON_FIRE") x 12 -> Current = 8
            //   GateResolution: operationResult = 45, snapshot = 20 => delta = +25
            //   ApplyDelta(Shield, +25, "MATH_GATE") -> PreviousValue = 8, NewValue = 33, WasClamped = false
            var service = CreateService(out _, out _);
            service.Initialize(BuildLevel(startingShield: 20, shieldCapacity: 200));

            int snapshot = service.CaptureSnapshot(ResourceType.Shield);
            Assert.That(snapshot, Is.EqualTo(20));

            for (int i = 0; i < 12; i++)
            {
                bool consumed = service.TryConsume(ResourceType.Shield, 1, "WEAPON_FIRE", out _);
                Assert.That(consumed, Is.True);
            }

            Assert.That(service.GetCurrent(ResourceType.Shield), Is.EqualTo(8));

            // The math gate ("x2 vs +25") resolved to the +25 option, whose
            // operation result is 45; the reward is the delta against the
            // SNAPSHOT (20), not against whatever the live value drifted to
            // during combat — this is the whole point of CaptureSnapshot.
            const int operationResult = 45;
            int rewardDelta = operationResult - snapshot;
            Assert.That(rewardDelta, Is.EqualTo(25));

            var change = service.ApplyDelta(ResourceType.Shield, rewardDelta, "MATH_GATE");

            Assert.That(change.PreviousValue, Is.EqualTo(8));
            Assert.That(change.NewValue, Is.EqualTo(33));
            Assert.That(change.WasClamped, Is.False);
            Assert.That(service.GetCurrent(ResourceType.Shield), Is.EqualTo(33));
        }

        [Test]
        public void ApplyDelta_NotifiesEventSinkWithMatchingChangeAndGameTime()
        {
            var service = CreateService(out var sink, out var clock);
            service.Initialize(BuildLevel(startingShield: 20));
            clock.Advance(5.0f);

            var change = service.ApplyDelta(ResourceType.Shield, 10, "PICKUP");

            var resourceChangedEvents = sink.OfType<ResourceChanged>();
            var published = resourceChangedEvents[resourceChangedEvents.Count - 1];
            Assert.That(published.Change.Type, Is.EqualTo(ResourceType.Shield));
            Assert.That(published.Change.PreviousValue, Is.EqualTo(change.PreviousValue));
            Assert.That(published.Change.NewValue, Is.EqualTo(change.NewValue));
            Assert.That(published.Change.ReasonCode, Is.EqualTo("PICKUP"));
            Assert.That(published.GameTime, Is.EqualTo(5.0).Within(1e-6));
        }

        [Test]
        public void TryConsume_Success_AlsoNotifiesEventSink()
        {
            var service = CreateService(out var sink, out _);
            service.Initialize(BuildLevel(startingShield: 20));
            int beforeCount = sink.PublishedCount;

            service.TryConsume(ResourceType.Shield, 1, "WEAPON_FIRE", out _);

            Assert.That(sink.PublishedCount, Is.EqualTo(beforeCount + 1));
            var lastEvent = sink.OfType<ResourceChanged>()[sink.OfType<ResourceChanged>().Count - 1];
            Assert.That(lastEvent.Change.ReasonCode, Is.EqualTo("WEAPON_FIRE"));
        }
    }
}
