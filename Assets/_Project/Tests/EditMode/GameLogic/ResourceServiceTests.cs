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
    // path for Ammo/Health/Shield (spec 4.3), and its 2.2 reference scenario
    // (20 ammo -> snapshot -> 12 consumed -> +25 gate delta -> 33) is the
    // primary acceptance test for the whole F1-04 package.
    [TestFixture]
    public class ResourceServiceTests
    {
        private static LevelDefinition BuildLevel(int startingAmmo = 20, int maxAmmo = 200, int startingHealth = 100, int maxHealth = 100, int maxShield = 50)
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
                startingAmmo: startingAmmo,
                maxAmmo: maxAmmo,
                startingHealth: startingHealth,
                maxHealth: maxHealth,
                startingShield: 0,
                maxShield: maxShield,
                requiredMathAccuracy: 0f,
                weaponId: "basic_weapon");
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

            service.Initialize(BuildLevel(startingAmmo: 20, maxAmmo: 200, startingHealth: 100, maxHealth: 100));

            Assert.That(service.GetCurrent(ResourceType.Ammo), Is.EqualTo(20));
            Assert.That(service.GetMax(ResourceType.Ammo), Is.EqualTo(200));
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

            var ammoInitEvents = sink.OfType<ResourceChanged>();
            Assert.That(ammoInitEvents.Count, Is.EqualTo(3), "Ammo + Health + Shield egy LEVEL_INIT eseményt kap egyenként.");
            foreach (var evt in ammoInitEvents)
            {
                Assert.That(evt.Change.ReasonCode, Is.EqualTo("LEVEL_INIT"));
            }
        }

        [Test]
        public void TryConsume_InsufficientAmmo_ReturnsFalseAndLeavesStateUnchanged()
        {
            // "nulla lőszerrel nincs lövés": starting ammo is driven to 0, then
            // a further shot must fail without mutating state.
            var service = CreateService(out var sink, out _);
            service.Initialize(BuildLevel(startingAmmo: 0));
            int publishedBeforeAttempt = sink.PublishedCount;

            bool consumed = service.TryConsume(ResourceType.Ammo, 1, "WEAPON_FIRE", out var change);

            Assert.That(consumed, Is.False);
            Assert.That(service.GetCurrent(ResourceType.Ammo), Is.EqualTo(0));
            Assert.That(sink.PublishedCount, Is.EqualTo(publishedBeforeAttempt), "Sikertelen fogyasztás nem publikál ResourceChanged-et.");
        }

        [Test]
        public void TryConsume_SufficientAmmo_DecrementsAndReturnsTrue()
        {
            var service = CreateService(out _, out _);
            service.Initialize(BuildLevel(startingAmmo: 20));

            bool consumed = service.TryConsume(ResourceType.Ammo, 1, "WEAPON_FIRE", out var change);

            Assert.That(consumed, Is.True);
            Assert.That(change.PreviousValue, Is.EqualTo(20));
            Assert.That(change.NewValue, Is.EqualTo(19));
            Assert.That(change.AppliedDelta, Is.EqualTo(-1));
            Assert.That(service.GetCurrent(ResourceType.Ammo), Is.EqualTo(19));
        }

        [Test]
        public void TryConsume_ExactlyAvailableAmount_SucceedsAndDepletesToZero()
        {
            var service = CreateService(out _, out _);
            service.Initialize(BuildLevel(startingAmmo: 5));

            bool consumed = service.TryConsume(ResourceType.Ammo, 5, "WEAPON_FIRE", out var change);

            Assert.That(consumed, Is.True);
            Assert.That(service.GetCurrent(ResourceType.Ammo), Is.EqualTo(0));
        }

        [Test]
        public void ApplyDelta_AboveMax_ClampsAndReportsWasClamped()
        {
            var service = CreateService(out _, out _);
            service.Initialize(BuildLevel(startingAmmo: 190, maxAmmo: 200));

            var change = service.ApplyDelta(ResourceType.Ammo, 50, "PICKUP");

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
            service.Initialize(BuildLevel(startingAmmo: 20, maxAmmo: 200));

            var change = service.SetMax(ResourceType.Ammo, 10, "LEVEL_INIT");

            Assert.That(service.GetMax(ResourceType.Ammo), Is.EqualTo(10));
            Assert.That(service.GetCurrent(ResourceType.Ammo), Is.EqualTo(10));
            Assert.That(change.WasClamped, Is.True);
        }

        [Test]
        public void CaptureSnapshot_ReturnsCurrentValue()
        {
            var service = CreateService(out _, out _);
            service.Initialize(BuildLevel(startingAmmo: 20));

            int snapshot = service.CaptureSnapshot(ResourceType.Ammo);

            Assert.That(snapshot, Is.EqualTo(20));
        }

        [Test]
        public void SnapshotBasedGateReward_ReferenceScenario_MatchesContract()
        {
            // Contract game-logic.md 2.2:
            //   Initialize: Ammo = 20 (Max 200)
            //   CaptureSnapshot(Ammo) -> 20
            //   harc: TryConsume(Ammo, 1, "WEAPON_FIRE") x 12 -> Current = 8
            //   GateResolution: operationResult = 45, snapshot = 20 => delta = +25
            //   ApplyDelta(Ammo, +25, "MATH_GATE") -> PreviousValue = 8, NewValue = 33, WasClamped = false
            var service = CreateService(out _, out _);
            service.Initialize(BuildLevel(startingAmmo: 20, maxAmmo: 200));

            int snapshot = service.CaptureSnapshot(ResourceType.Ammo);
            Assert.That(snapshot, Is.EqualTo(20));

            for (int i = 0; i < 12; i++)
            {
                bool consumed = service.TryConsume(ResourceType.Ammo, 1, "WEAPON_FIRE", out _);
                Assert.That(consumed, Is.True);
            }

            Assert.That(service.GetCurrent(ResourceType.Ammo), Is.EqualTo(8));

            // The math gate ("x2 vs +25") resolved to the +25 option, whose
            // operation result is 45; the reward is the delta against the
            // SNAPSHOT (20), not against whatever the live value drifted to
            // during combat — this is the whole point of CaptureSnapshot.
            const int operationResult = 45;
            int rewardDelta = operationResult - snapshot;
            Assert.That(rewardDelta, Is.EqualTo(25));

            var change = service.ApplyDelta(ResourceType.Ammo, rewardDelta, "MATH_GATE");

            Assert.That(change.PreviousValue, Is.EqualTo(8));
            Assert.That(change.NewValue, Is.EqualTo(33));
            Assert.That(change.WasClamped, Is.False);
            Assert.That(service.GetCurrent(ResourceType.Ammo), Is.EqualTo(33));
        }

        [Test]
        public void ApplyDelta_NotifiesEventSinkWithMatchingChangeAndGameTime()
        {
            var service = CreateService(out var sink, out var clock);
            service.Initialize(BuildLevel(startingAmmo: 20));
            clock.Advance(5.0f);

            var change = service.ApplyDelta(ResourceType.Ammo, 10, "PICKUP");

            var resourceChangedEvents = sink.OfType<ResourceChanged>();
            var published = resourceChangedEvents[resourceChangedEvents.Count - 1];
            Assert.That(published.Change.Type, Is.EqualTo(ResourceType.Ammo));
            Assert.That(published.Change.PreviousValue, Is.EqualTo(change.PreviousValue));
            Assert.That(published.Change.NewValue, Is.EqualTo(change.NewValue));
            Assert.That(published.Change.ReasonCode, Is.EqualTo("PICKUP"));
            Assert.That(published.GameTime, Is.EqualTo(5.0).Within(1e-6));
        }

        [Test]
        public void TryConsume_Success_AlsoNotifiesEventSink()
        {
            var service = CreateService(out var sink, out _);
            service.Initialize(BuildLevel(startingAmmo: 20));
            int beforeCount = sink.PublishedCount;

            service.TryConsume(ResourceType.Ammo, 1, "WEAPON_FIRE", out _);

            Assert.That(sink.PublishedCount, Is.EqualTo(beforeCount + 1));
            var lastEvent = sink.OfType<ResourceChanged>()[sink.OfType<ResourceChanged>().Count - 1];
            Assert.That(lastEvent.Change.ReasonCode, Is.EqualTo("WEAPON_FIRE"));
        }
    }
}
