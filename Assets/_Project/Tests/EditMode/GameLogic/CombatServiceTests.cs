using MathHorde.Core.Common;
using MathHorde.Core.Levels;
using MathHorde.Core.Resources;
using MathHorde.GameLogic;
using MathHorde.GameLogic.Events;
using MathHorde.Tests.EditMode.TestSupport;
using NUnit.Framework;

namespace MathHorde.Tests.EditMode.GameLogic
{
    // Contract game-logic.md 3. (spec 4.1, 4.2) + TESTING.md 17.4 catalog:
    // ammo decreases on fire, zero ammo means no shot, hits reduce enemy
    // health, zero health destroys the enemy, reaching the player damages
    // the player, shield absorbs before health.
    [TestFixture]
    public class CombatServiceTests
    {
        private static CombatService CreateService(
            out ResourceService resourceService,
            out RecordingGameEventSink sink,
            LevelDefinition level)
        {
            sink = new RecordingGameEventSink();
            var clock = new GameClock();
            resourceService = new ResourceService(sink, clock);
            resourceService.Initialize(level);
            return new CombatService(resourceService, sink);
        }

        [Test]
        public void Tick_FiresAtTarget_DecreasesAmmo()
        {
            var level = CombatTestFixtures.BuildLevel(startingAmmo: 20, maxAmmo: 200);
            var weapon = CombatTestFixtures.BuildWeapon(ammoPerShot: 1, projectileSpeed: 0f);
            var enemy = CombatTestFixtures.BuildEnemy(maxHealth: 1000);
            var service = CreateService(out var resourceService, out var sink, level);
            service.Initialize(level, weapon, CombatTestFixtures.BuildCatalog(enemy));
            service.SpawnEnemy(enemy.EnemyId, new LaneIndex(0), spawnDepth: 5f);

            service.Tick(gameTime: 0.0, deltaTime: 0.1f);

            Assert.That(resourceService.GetCurrent(ResourceType.Ammo), Is.EqualTo(19));
            Assert.That(sink.OfType<WeaponFired>().Count, Is.EqualTo(1));
        }

        [Test]
        public void Tick_ZeroAmmo_NoShotFired_PublishesWeaponDryFire()
        {
            // "nulla lőszerrel nincs lövés"
            var level = CombatTestFixtures.BuildLevel(startingAmmo: 0, maxAmmo: 200);
            var weapon = CombatTestFixtures.BuildWeapon(projectileSpeed: 0f);
            var enemy = CombatTestFixtures.BuildEnemy(maxHealth: 1000);
            var service = CreateService(out var resourceService, out var sink, level);
            service.Initialize(level, weapon, CombatTestFixtures.BuildCatalog(enemy));
            service.SpawnEnemy(enemy.EnemyId, new LaneIndex(0), spawnDepth: 5f);

            service.Tick(gameTime: 0.0, deltaTime: 0.1f);

            Assert.That(sink.OfType<WeaponFired>(), Is.Empty);
            Assert.That(sink.OfType<WeaponDryFire>().Count, Is.EqualTo(1));
            Assert.That(resourceService.GetCurrent(ResourceType.Ammo), Is.EqualTo(0));
            Assert.That(service.ActiveEnemies[0].CurrentHealth, Is.EqualTo(1000), "Sikertelen tüzelés nem sebezhet.");
        }

        [Test]
        public void Tick_HitscanHit_DecreasesEnemyHealth()
        {
            var level = CombatTestFixtures.BuildLevel(startingAmmo: 20);
            var weapon = CombatTestFixtures.BuildWeapon(damage: 7, projectileSpeed: 0f);
            var enemy = CombatTestFixtures.BuildEnemy(maxHealth: 100);
            var service = CreateService(out _, out var sink, level);
            service.Initialize(level, weapon, CombatTestFixtures.BuildCatalog(enemy));
            int instanceId = service.SpawnEnemy(enemy.EnemyId, new LaneIndex(0), spawnDepth: 5f);

            service.Tick(gameTime: 0.0, deltaTime: 0.1f);

            Assert.That(service.ActiveEnemies[0].CurrentHealth, Is.EqualTo(93));
            var damagedEvents = sink.OfType<EnemyDamaged>();
            Assert.That(damagedEvents.Count, Is.EqualTo(1));
            Assert.That(damagedEvents[0].EnemyInstanceId, Is.EqualTo(instanceId));
            Assert.That(damagedEvents[0].RemainingHealth, Is.EqualTo(93));
        }

        [Test]
        public void Tick_EnemyHealthReachesZero_IsDestroyedAndEnemyKilledPublished()
        {
            // "nulla életerőnél az ellenség megsemmisül"
            var level = CombatTestFixtures.BuildLevel(startingAmmo: 20);
            var weapon = CombatTestFixtures.BuildWeapon(damage: 50, projectileSpeed: 0f);
            var enemy = CombatTestFixtures.BuildEnemy(maxHealth: 10, rewardValue: 3);
            var service = CreateService(out _, out var sink, level);
            service.Initialize(level, weapon, CombatTestFixtures.BuildCatalog(enemy));
            int instanceId = service.SpawnEnemy(enemy.EnemyId, new LaneIndex(0), spawnDepth: 5f);

            service.Tick(gameTime: 0.0, deltaTime: 0.1f);

            Assert.That(service.ActiveEnemies, Is.Empty, "A halott ellenség nem lehet az ActiveEnemies listában.");
            var killedEvents = sink.OfType<EnemyKilled>();
            Assert.That(killedEvents.Count, Is.EqualTo(1));
            Assert.That(killedEvents[0].EnemyInstanceId, Is.EqualTo(instanceId));
            Assert.That(killedEvents[0].RewardValue, Is.EqualTo(3));
        }

        [Test]
        public void Tick_EnemyReachesPlayer_DamagesPlayerHealth()
        {
            // "az ellenség elérése sebzi a játékost" — ammo=0 so the weapon
            // cannot also kill the enemy the same tick, isolating contact damage.
            var level = CombatTestFixtures.BuildLevel(startingAmmo: 0, startingHealth: 100, maxHealth: 100);
            var weapon = CombatTestFixtures.BuildWeapon(projectileSpeed: 0f);
            var enemy = CombatTestFixtures.BuildEnemy(maxHealth: 1000, contactDamage: 12);
            var service = CreateService(out var resourceService, out var sink, level);
            service.Initialize(level, weapon, CombatTestFixtures.BuildCatalog(enemy));
            service.SpawnEnemy(enemy.EnemyId, new LaneIndex(0), spawnDepth: 0f);

            service.Tick(gameTime: 0.0, deltaTime: 0.1f);

            Assert.That(resourceService.GetCurrent(ResourceType.Health), Is.EqualTo(88));
            Assert.That(sink.OfType<EnemyReachedPlayer>().Count, Is.EqualTo(1));
            var playerDamaged = sink.OfType<PlayerDamaged>();
            Assert.That(playerDamaged.Count, Is.EqualTo(1));
            Assert.That(playerDamaged[0].Damage, Is.EqualTo(12));
            Assert.That(playerDamaged[0].RemainingHealth, Is.EqualTo(88));
        }

        [Test]
        public void Tick_PlayerHealthReachesZero_IsPlayerAliveBecomesFalse()
        {
            var level = CombatTestFixtures.BuildLevel(startingAmmo: 0, startingHealth: 5, maxHealth: 100);
            var weapon = CombatTestFixtures.BuildWeapon(projectileSpeed: 0f);
            var enemy = CombatTestFixtures.BuildEnemy(maxHealth: 1000, contactDamage: 50);
            var service = CreateService(out _, out _, level);
            service.Initialize(level, weapon, CombatTestFixtures.BuildCatalog(enemy));
            service.SpawnEnemy(enemy.EnemyId, new LaneIndex(0), spawnDepth: 0f);

            service.Tick(gameTime: 0.0, deltaTime: 0.1f);

            Assert.That(service.IsPlayerAlive, Is.False);
        }

        [Test]
        public void Tick_ContactDamage_DrainsShieldBeforeHealth()
        {
            // Contract game-logic.md 3. rule 5 (GAME_DESIGN.md 6.1 shield order).
            var level = CombatTestFixtures.BuildLevel(startingAmmo: 0, startingHealth: 100, maxHealth: 100, startingShield: 5, maxShield: 20);
            var weapon = CombatTestFixtures.BuildWeapon(projectileSpeed: 0f);
            var enemy = CombatTestFixtures.BuildEnemy(maxHealth: 1000, contactDamage: 8);
            var service = CreateService(out var resourceService, out _, level);
            service.Initialize(level, weapon, CombatTestFixtures.BuildCatalog(enemy));
            service.SpawnEnemy(enemy.EnemyId, new LaneIndex(0), spawnDepth: 0f);

            service.Tick(gameTime: 0.0, deltaTime: 0.1f);

            Assert.That(resourceService.GetCurrent(ResourceType.Shield), Is.EqualTo(0), "Az 5 pajzs teljesen elfogy.");
            Assert.That(resourceService.GetCurrent(ResourceType.Health), Is.EqualTo(97), "A maradék 3 sebzés megy az életerőre (100-3).");
        }

        [Test]
        public void Tick_OwnLaneThenAdjacent_PrefersNearestInPlayerLaneOverOtherLanes()
        {
            var level = CombatTestFixtures.BuildLevel(startingAmmo: 20, laneCount: 3);
            var weapon = CombatTestFixtures.BuildWeapon(damage: 1, projectileSpeed: 0f, targetingMode: TargetingMode.OwnLaneThenAdjacent);
            var enemy = CombatTestFixtures.BuildEnemy(maxHealth: 100);
            var service = CreateService(out _, out var sink, level);
            service.Initialize(level, weapon, CombatTestFixtures.BuildCatalog(enemy));
            service.MovePlayerToLane(new LaneIndex(1));

            int farInOwnLane = service.SpawnEnemy(enemy.EnemyId, new LaneIndex(1), spawnDepth: 8f);
            int nearInAdjacentLane = service.SpawnEnemy(enemy.EnemyId, new LaneIndex(0), spawnDepth: 1f);

            service.Tick(gameTime: 0.0, deltaTime: 0.1f);

            var damaged = sink.OfType<EnemyDamaged>();
            Assert.That(damaged.Count, Is.EqualTo(1));
            Assert.That(damaged[0].EnemyInstanceId, Is.EqualTo(farInOwnLane), "A saját sáv előnyt kap, még ha messzebb is van, mint a szomszéd sávé.");
            Assert.That(nearInAdjacentLane, Is.Not.EqualTo(farInOwnLane));
        }

        [Test]
        public void Tick_OwnLaneEmpty_FallsBackToAdjacentLane()
        {
            var level = CombatTestFixtures.BuildLevel(startingAmmo: 20, laneCount: 3);
            var weapon = CombatTestFixtures.BuildWeapon(damage: 1, projectileSpeed: 0f, targetingMode: TargetingMode.OwnLaneThenAdjacent);
            var enemy = CombatTestFixtures.BuildEnemy(maxHealth: 100);
            var service = CreateService(out _, out var sink, level);
            service.Initialize(level, weapon, CombatTestFixtures.BuildCatalog(enemy));
            service.MovePlayerToLane(new LaneIndex(1));
            int adjacentEnemy = service.SpawnEnemy(enemy.EnemyId, new LaneIndex(2), spawnDepth: 3f);

            service.Tick(gameTime: 0.0, deltaTime: 0.1f);

            var damaged = sink.OfType<EnemyDamaged>();
            Assert.That(damaged.Count, Is.EqualTo(1));
            Assert.That(damaged[0].EnemyInstanceId, Is.EqualTo(adjacentEnemy));
        }

        [Test]
        public void Tick_ProjectileWeapon_DamageIsDelayedUntilArrival()
        {
            var level = CombatTestFixtures.BuildLevel(startingAmmo: 20);
            var weapon = CombatTestFixtures.BuildWeapon(damage: 9, projectileSpeed: 10f);
            var enemy = CombatTestFixtures.BuildEnemy(maxHealth: 100, movementSpeed: 0.0001f);
            var service = CreateService(out _, out var sink, level);
            service.Initialize(level, weapon, CombatTestFixtures.BuildCatalog(enemy));
            service.SpawnEnemy(enemy.EnemyId, new LaneIndex(0), spawnDepth: 1f);

            // Fires on this tick but the projectile (distance 1, speed 10) needs 0.1s to arrive.
            service.Tick(gameTime: 0.0, deltaTime: 0.05f);
            Assert.That(sink.OfType<EnemyDamaged>(), Is.Empty, "A lövedék még úton van.");

            service.Tick(gameTime: 0.05, deltaTime: 0.06f);
            Assert.That(sink.OfType<EnemyDamaged>().Count, Is.EqualTo(1), "A lövedék megérkezett és sebzett.");
        }

        [Test]
        public void SpawnEnemy_UnknownEnemyId_Throws()
        {
            var level = CombatTestFixtures.BuildLevel();
            var weapon = CombatTestFixtures.BuildWeapon();
            var service = CreateService(out _, out _, level);
            service.Initialize(level, weapon, CombatTestFixtures.BuildCatalog());

            Assert.That(
                () => service.SpawnEnemy("no_such_enemy", new LaneIndex(0), 5f),
                Throws.TypeOf<System.ArgumentException>());
        }

        [Test]
        public void SpawnEnemy_InstanceIdsAreNeverReusedWithinASession()
        {
            var level = CombatTestFixtures.BuildLevel(startingAmmo: 20);
            var weapon = CombatTestFixtures.BuildWeapon(damage: 50, projectileSpeed: 0f);
            var enemy = CombatTestFixtures.BuildEnemy(maxHealth: 1);
            var service = CreateService(out _, out _, level);
            service.Initialize(level, weapon, CombatTestFixtures.BuildCatalog(enemy));

            int first = service.SpawnEnemy(enemy.EnemyId, new LaneIndex(0), 5f);
            service.Tick(0.0, 0.1f); // kills it, frees its pooled slot
            int second = service.SpawnEnemy(enemy.EnemyId, new LaneIndex(0), 5f);

            Assert.That(second, Is.Not.EqualTo(first));
        }
    }
}
