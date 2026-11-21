using System;
using System.Collections.Generic;
using MathHorde.Core.Common;
using MathHorde.Core.Levels;
using MathHorde.Core.Progress;
using MathHorde.GameLogic;
using MathHorde.GameLogic.Events;
using MathHorde.Tests.EditMode.TestSupport;
using NUnit.Framework;

namespace MathHorde.Tests.EditMode.GameLogic
{
    public class WeaponSelectionTests
    {
        private sealed class EmptySink : IGameEventSink
        {
            public void Publish<T>(in T value) where T : struct, IGameEvent { }
        }
        private static LevelDefinition Level() => CombatTestFixtures.BuildLevel(
            waves: new[] { new WaveDefinition(0, 0, 1000) }, weaponId: "slow",
            availableWeaponIds: new[] { "slow", "fast" });
        private static WeaponDefinition[] Weapons(float projectileSpeed = 0) => new[] {
            CombatTestFixtures.BuildWeapon("slow", damage: 5, fireRate: 1, projectileSpeed: projectileSpeed),
            CombatTestFixtures.BuildWeapon("fast", damage: 10, fireRate: 4) };

        [Test]
        public void LevelCatalogCopiesIdsAndEnforcesOneToFourDistinctIdsIncludingStarter()
        {
            var ids = new[] { "slow", "fast" };
            var level = CombatTestFixtures.BuildLevel(weaponId: "slow", availableWeaponIds: ids);
            ids[0] = "mutated";
            Assert.That(level.AvailableWeaponIds[0], Is.EqualTo("slow"));
            Assert.That(() => ((IList<string>)level.AvailableWeaponIds)[0] = "mutated", Throws.TypeOf<NotSupportedException>());
            foreach (var invalid in new[] { new string[0], new[] { "slow", "slow" }, new[] { "slow", "" }, new[] { "fast" }, new[] { "slow", "2", "3", "4", "5" } })
                Assert.That(() => CombatTestFixtures.BuildLevel(weaponId: "slow", availableWeaponIds: invalid), Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void Selection_InitialSameInvalidAndOneEvent_CatalogIsCopied()
        {
            var level = Level(); var weapons = Weapons(); var sink = new RecordingGameEventSink();
            var resources = new ResourceService(sink, new GameClock()); resources.Initialize(level);
            var combat = new CombatService(resources, sink);
            combat.Initialize(level, weapons, CombatTestFixtures.BuildCatalog());
            weapons[1] = CombatTestFixtures.BuildWeapon("foreign");
            Assert.That(combat.CurrentWeaponId, Is.EqualTo("slow"));
            Assert.That(combat.SelectWeapon("slow", 2).Value, Is.False);
            foreach (string id in new[] { null, "", "missing" })
                Assert.That(combat.SelectWeapon(id, 2).ErrorCode, Is.EqualTo("WEAPON_NOT_AVAILABLE"));
            Assert.That(combat.SelectWeapon("fast", 2).Value, Is.True);
            var changes = sink.OfType<WeaponChanged>();
            Assert.That(changes.Count, Is.EqualTo(1));
            Assert.That(changes[0].PreviousWeaponId, Is.EqualTo("slow"));
            Assert.That(changes[0].WeaponId, Is.EqualTo("fast"));
            Assert.That(changes[0].GameTime, Is.EqualTo(2));
            Assert.That(sink.OfType<WeaponFired>().Count, Is.Zero);
        }

        [Test]
        public void SwitchingPreservesOldCooldown_AndRepeatedSameTypeDoesNotDelayShot()
        {
            var level = Level(); var sink = new RecordingGameEventSink();
            var resources = new ResourceService(sink, new GameClock()); resources.Initialize(level);
            var combat = new CombatService(resources, sink);
            var enemy = CombatTestFixtures.BuildEnemy(maxHealth: 1000, movementSpeed: .001f);
            combat.Initialize(level, Weapons(), CombatTestFixtures.BuildCatalog(enemy));
            combat.SpawnEnemy(enemy.EnemyId, new LaneIndex(0), 5);
            combat.Tick(0, .01f);
            combat.SelectWeapon("fast", .1);
            combat.Tick(.35, .01f);
            Assert.That(sink.OfType<WeaponFired>().Count, Is.EqualTo(1), "Old nextShotAt=1 remains authoritative.");
            combat.SelectWeapon("slow", .4); // nextShotAt=1.4
            combat.SelectWeapon("fast", .41);
            combat.SelectWeapon("fast", 1.3); // same id cannot postpone to 1.55
            combat.Tick(1.39, .01f);
            Assert.That(sink.OfType<WeaponFired>().Count, Is.EqualTo(1));
            combat.Tick(1.4, .01f);
            Assert.That(sink.OfType<WeaponFired>().Count, Is.EqualTo(2));
            Assert.That(combat.ActiveEnemies[0].CurrentHealth, Is.EqualTo(985));
        }

        [Test]
        public void InFlightProjectileRetainsOriginalDamageAndSpeedAfterSelection()
        {
            var level = Level(); var sink = new RecordingGameEventSink();
            var resources = new ResourceService(sink, new GameClock()); resources.Initialize(level);
            var combat = new CombatService(resources, sink);
            var enemy = CombatTestFixtures.BuildEnemy(maxHealth: 1000, movementSpeed: .001f);
            combat.Initialize(level, Weapons(1), CombatTestFixtures.BuildCatalog(enemy));
            combat.SpawnEnemy(enemy.EnemyId, new LaneIndex(0), 5);
            combat.Tick(0, .1f);
            combat.SelectWeapon("fast", .1);
            combat.Tick(.2, 4f); // original projectile has travelled only 4.1/5
            Assert.That(sink.OfType<EnemyDamaged>().Count, Is.Zero);
            combat.Tick(.3, 1f);
            Assert.That(sink.OfType<EnemyDamaged>().Count, Is.EqualTo(1));
            Assert.That(sink.OfType<EnemyDamaged>()[0].Damage, Is.EqualTo(5));
        }

        [Test]
        public void SessionGuardsPauseDeathFinalizationAndRestart_AndCopiesAvailableWeapons()
        {
            var level = Level(); var weapons = Weapons(); var sink = new RecordingGameEventSink(); var clock = new GameClock();
            var resources = new ResourceService(sink, clock); var combat = new CombatService(resources, sink);
            var session = new GameSessionService(clock, new WaveDirector(), combat, new NullMathEventDirector(), resources, sink);
            Assert.That(session.SelectWeapon("fast").ErrorCode, Is.EqualTo("SESSION_NOT_RUNNING"));
            var killer = CombatTestFixtures.BuildEnemy(contactDamage: 1000);
            var enemies = CombatTestFixtures.BuildCatalog(killer);
            session.StartSession(level, weapons, enemies, 7);
            weapons[1] = CombatTestFixtures.BuildWeapon("foreign");
            Assert.That(session.AvailableWeapons[1].WeaponId, Is.EqualTo("fast"));
            Assert.That(() => ((IList<WeaponDefinition>)session.AvailableWeapons)[0] = weapons[0], Throws.TypeOf<NotSupportedException>());
            session.Pause();
            Assert.That(session.SelectWeapon("fast").ErrorCode, Is.EqualTo("SESSION_PAUSED"));
            session.Resume();
            Assert.That(session.SelectWeapon("fast").Value, Is.True);
            Assert.That(session.SelectWeapon("fast").Value, Is.False);
            Assert.That(session.SelectWeapon(null).ErrorCode, Is.EqualTo("WEAPON_NOT_AVAILABLE"));
            combat.SpawnEnemy(killer.EnemyId, new LaneIndex(0), 0);
            combat.Tick(0, 0);
            Assert.That(session.SelectWeapon("slow").ErrorCode, Is.EqualTo("SESSION_NOT_RUNNING"), "Dead combat is rejected before session evaluation.");
            session.Tick(.01f);
            Assert.That(session.SelectWeapon("slow").ErrorCode, Is.EqualTo("SESSION_NOT_RUNNING"));
            session.StartSession(level, Weapons(), enemies, 8);
            Assert.That(session.CurrentWeaponId, Is.EqualTo("slow"));
            session.Finalize(SessionResult.Victory, FailureReason.None);
            Assert.That(session.SelectWeapon("fast").ErrorCode, Is.EqualTo("SESSION_NOT_RUNNING"));
        }

        [Test]
        public void InvalidStartupCatalogsAreRejectedBeforeSessionRuns()
        {
            var level = Level(); var sink = new EmptySink(); var clock = new GameClock();
            var resources = new ResourceService(sink, clock); var combat = new CombatService(resources, sink);
            var session = new GameSessionService(clock, new WaveDirector(), combat, new NullMathEventDirector(), resources, sink);
            var valid = Weapons();
            foreach (var invalid in new WeaponDefinition[][] { null, new WeaponDefinition[0], new[] { valid[0] }, new[] { valid[0], valid[0] }, new[] { valid[0], null }, new[] { valid[1], valid[0] } })
            {
                Assert.That(() => session.StartSession(level, invalid, CombatTestFixtures.BuildCatalog(), 1), Throws.TypeOf<ArgumentException>());
                Assert.That(session.IsRunning, Is.False);
            }
        }

        [Test]
        public void TenThousandTicksAndSelectionsIncludingErrorsAllocateZeroManagedBytes()
        {
            var level = Level(); var sink = new EmptySink(); var clock = new GameClock();
            var resources = new ResourceService(sink, clock); var combat = new CombatService(resources, sink);
            var session = new GameSessionService(clock, new WaveDirector(), combat, new NullMathEventDirector(), resources, sink);
            var enemy = CombatTestFixtures.BuildEnemy(maxHealth: 1000000, movementSpeed: .00001f);
            session.StartSession(level, Weapons(), CombatTestFixtures.BuildCatalog(enemy), 7);
            combat.SpawnEnemy(enemy.EnemyId, new LaneIndex(0), 5);
            for (int i = 0; i < 100; i++) Exercise(session, i);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10000; i++) Exercise(session, i);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero);
        }
        private static void Exercise(GameSessionService session, int index)
        {
            session.SelectWeapon((index & 1) == 0 ? "slow" : "fast");
            session.SelectWeapon(null); session.SelectWeapon(""); session.SelectWeapon("missing");
            session.Tick(.01f);
            session.Pause(); session.SelectWeapon("fast"); session.Resume();
        }
    }
}
