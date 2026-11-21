using System;
using System.Collections.Generic;
using MathHorde.Core.Common;
using MathHorde.Core.Levels;
using MathHorde.Core.Progress;
using NUnit.Framework;

namespace MathHorde.Tests.EditMode.Levels
{
    [TestFixture]
    public class LevelDefinitionTests
    {
        [Test]
        public void EnemyType_HasExactlyTheFiveContractedValues()
        {
            // Contract core-domain.md 7. (ADR-008, revision r2): Swarmlet is
            // the fifth value, needed by the accepted balance model
            // (balance/difficulty-model.md "swarm ratio" from CL2).
            var values = (EnemyType[])Enum.GetValues(typeof(EnemyType));

            Assert.That(values, Is.EquivalentTo(new[]
            {
                EnemyType.Basic, EnemyType.Fast, EnemyType.Armored, EnemyType.Boss, EnemyType.Swarmlet
            }));
        }

        [Test]
        public void EnemyType_Swarmlet_HasExplicitValueFour()
        {
            // The enum rule (core-domain.md 0.) requires persisted enums to
            // carry explicit numeric values — this pins Swarmlet's value so a
            // future edit cannot silently renumber it.
            Assert.That((int)EnemyType.Swarmlet, Is.EqualTo(4));
        }

        [Test]
        public void EnemyDefinition_ValidValues_ConstructsSuccessfully()
        {
            var enemy = new EnemyDefinition(
                enemyId: "basic_bot",
                type: EnemyType.Basic,
                maxHealth: 10,
                movementSpeed: 1.5f,
                contactDamage: 5,
                attackCooldownSeconds: 1.0f,
                prefabKey: "enemy_basic");

            Assert.That(enemy.EnemyId, Is.EqualTo("basic_bot"));
            Assert.That(enemy.MaxHealth, Is.EqualTo(10));
        }

        [TestCase(0)]
        [TestCase(-5)]
        public void EnemyDefinition_NonPositiveMaxHealth_Throws(int maxHealth)
        {
            Assert.That(
                () => new EnemyDefinition("basic_bot", EnemyType.Basic, maxHealth, 1f, 0, 0f, "key"),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void EnemyDefinition_EmptyEnemyId_Throws()
        {
            Assert.That(
                () => new EnemyDefinition(string.Empty, EnemyType.Basic, 10, 1f, 0, 0f, "key"),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void WeaponDefinition_ValidValues_ConstructsSuccessfully()
        {
            var weapon = new WeaponDefinition(
                weaponId: "basic_weapon",
                damage: 10,
                fireRate: 2f,
                range: 15f,
                projectileSpeed: 0f,
                targetingMode: TargetingMode.OwnLaneThenAdjacent,
                visualEffectKey: "fx_basic", displayNameKey: "weapon.test");

            Assert.That(typeof(WeaponDefinition).GetProperty("AmmoPerShot"), Is.Null);
            Assert.That(weapon.ProjectileSpeed, Is.EqualTo(0f), "0 => hitscan, explicitly allowed by the contract.");
        }

        [Test]
        public void WeaponDefinition_NonPositiveDamage_Throws()
        {
            Assert.That(
                () => new WeaponDefinition("w", 0, 1f, 10f, projectileSpeed: 0f, TargetingMode.GlobalNearest, "fx", displayNameKey: "weapon.test"),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void WaveSpawnEntry_NoAllowedLanesGiven_DefaultsToEmptyMeaningAnyLane()
        {
            var entry = new WaveSpawnEntry("basic_bot", count: 3, spawnIntervalSeconds: 0.5f);

            Assert.That(entry.AllowedLanes, Is.Empty);
        }

        [Test]
        public void WaveSpawnEntry_NonPositiveSpawnInterval_Throws()
        {
            Assert.That(
                () => new WaveSpawnEntry("basic_bot", 3, 0f),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void WaveDefinition_NonPositiveDuration_Throws()
        {
            Assert.That(
                () => new WaveDefinition(waveIndex: 0, startGameTime: 0, durationSeconds: 0),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void WaveDefinition_ValidValues_CarriesSpawns()
        {
            var spawns = new List<WaveSpawnEntry>
            {
                new WaveSpawnEntry("basic_bot", 4, 0.8f, new[] { new LaneIndex(0), new LaneIndex(1) })
            };

            var wave = new WaveDefinition(0, 10.0, 30.0, spawns);

            Assert.That(wave.Spawns.Count, Is.EqualTo(1));
            Assert.That(wave.Spawns[0].AllowedLanes.Count, Is.EqualTo(2));
        }

        [TestCase(1)]
        [TestCase(5)]
        public void LevelDefinition_LaneCountOutOfRange_Throws(int laneCount)
        {
            Assert.That(() => BuildLevel(laneCount: laneCount), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void LevelDefinition_StartingHealthAboveMaxHealth_Throws()
        {
            Assert.That(() => BuildLevel(startingHealth: 999, maxHealth: 200), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void LevelDefinition_ZeroMaxShield_IsValidAndMeansNoShieldMechanic()
        {
            // L4: MaxShield == 0 is explicitly allowed — a level with no
            // shield mechanic (core-domain.md, note after L4/L5).
            var level = BuildLevel(startingShield: 0, maxShield: 0);

            Assert.That(level.StartingShield, Is.EqualTo(0));
            Assert.That(level.MaxShield, Is.EqualTo(0));
        }

        [Test]
        public void LevelDefinition_NegativeMaxShield_Throws()
        {
            // L4: MaxShield >= 0.
            Assert.That(() => BuildLevel(startingShield: 0, maxShield: -1), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void LevelDefinition_StartingShieldAboveMaxShield_Throws()
        {
            // L5: 0 <= StartingShield <= MaxShield.
            Assert.That(() => BuildLevel(startingShield: 60, maxShield: 50), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void LevelDefinition_StartingShieldWithinMaxShield_IsValid()
        {
            var level = BuildLevel(startingShield: 0, maxShield: 50);

            Assert.That(level.MaxShield, Is.EqualTo(50));
        }

        [Test]
        public void LevelDefinition_ValidValues_ConstructsSuccessfully()
        {
            var level = BuildLevel();

            Assert.That(level.Id.Value, Is.EqualTo("w1-l01"));
            Assert.That(level.WorldId.Value, Is.EqualTo("w1"));
            Assert.That(level.LaneCount, Is.EqualTo(3));
        }

        private static LevelDefinition BuildLevel(
            int laneCount = 3,
            int startingHealth = 100,
            int maxHealth = 100,
            int startingShield = 0,
            int maxShield = 50)
        {
            return new LevelDefinition(
                id: new LevelId("w1-l01"),
                worldId: new WorldId("w1"),
                displayNameKey: "level.w1l01.name",
                laneCount: laneCount,
                targetDurationSeconds: 200,
                waves: null,
                mathEvents: null,
                bossEnemyId: null,
                startingHealth: startingHealth,
                maxHealth: maxHealth,
                startingShield: startingShield,
                maxShield: maxShield,
                requiredMathAccuracy: 0f,
                weaponId: "basic_weapon", availableWeaponIds: new[] { "basic_weapon" });
        }
    }
}
