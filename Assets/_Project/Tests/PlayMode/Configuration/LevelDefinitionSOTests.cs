// LevelDefinitionSOTests.cs
// F1-09 — contract scriptable-object-schema.md 3., 3.1, 3.2. World (F5),
// BossEnemy/BossTriggerGameTime remain F5; F3 extends the valid fixture's schedule.
using System.Collections.Generic;
using MathHorde.Infrastructure.Configuration;
using MathHorde.Tests.PlayMode.TestSupport;
using NUnit.Framework;
using UnityEngine;

namespace MathHorde.Tests.PlayMode.Configuration
{
    [TestFixture]
    public class LevelDefinitionSOTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object obj in _created)
            {
                Object.DestroyImmediate(obj);
            }

            _created.Clear();
        }

        private EnemyDefinitionSO CreateValidEnemy()
        {
            var enemy = ScriptableObject.CreateInstance<EnemyDefinitionSO>();
            _created.Add(enemy);
            var prefab = new GameObject("dummy_enemy_prefab");
            _created.Add(prefab);
            ConfigTestReflection.SetField(enemy, "_prefab", prefab);
            return enemy;
        }

        private LevelDefinitionSO.WaveDefinitionEntry CreateValidWave(EnemyDefinitionSO enemy)
        {
            var spawn = new LevelDefinitionSO.WaveSpawnEntry();
            ConfigTestReflection.SetField(spawn, "_enemy", enemy);
            ConfigTestReflection.SetField(spawn, "_count", 5);
            ConfigTestReflection.SetField(spawn, "_spawnIntervalSeconds", 0.8f);

            var wave = new LevelDefinitionSO.WaveDefinitionEntry();
            ConfigTestReflection.SetField(wave, "_waveIndex", 0);
            ConfigTestReflection.SetField(wave, "_startGameTime", 0f);
            ConfigTestReflection.SetField(wave, "_durationSeconds", 20f);
            ConfigTestReflection.SetField(wave, "_spawns", new List<LevelDefinitionSO.WaveSpawnEntry> { spawn });

            return wave;
        }

        private LevelDefinitionSO CreateValidLevel()
        {
            var level = ScriptableObject.CreateInstance<LevelDefinitionSO>();
            _created.Add(level);

            var weapon = ScriptableObject.CreateInstance<WeaponConfigSO>();
            _created.Add(weapon);

            var environmentPrefab = new GameObject("dummy_environment_prefab");
            _created.Add(environmentPrefab);

            EnemyDefinitionSO enemy = CreateValidEnemy();

            ConfigTestReflection.SetField(level, "_displayNameKey", "level.w1l01");
            ConfigTestReflection.SetField(level, "_weapon", weapon);
            ConfigTestReflection.SetField(level, "_environmentPrefab", environmentPrefab);
            ConfigTestReflection.SetField(level, "_waves", new List<LevelDefinitionSO.WaveDefinitionEntry> { CreateValidWave(enemy) });
            ConfigTestReflection.SetField(level, "_mathEvents", ConfigTestReflection.MathSchedule());

            return level;
        }

        [Test]
        public void Validate_Defaults_ReportsMissingRequiredReferencesAndEmptyWaves()
        {
            var level = ScriptableObject.CreateInstance<LevelDefinitionSO>();
            _created.Add(level);

            IReadOnlyList<string> errors = level.Validate();

            Assert.That(ConfigTestReflection.AnyContains(errors, "DisplayNameKey"), Is.True);
            Assert.That(ConfigTestReflection.AnyContains(errors, "Waves"), Is.True);
            Assert.That(ConfigTestReflection.AnyContains(errors, "Weapon"), Is.True);
            Assert.That(ConfigTestReflection.AnyContains(errors, "EnvironmentPrefab"), Is.True);
        }

        [Test]
        public void Validate_FullyAssembledLevel_ReturnsNoErrors()
        {
            LevelDefinitionSO level = CreateValidLevel();

            IReadOnlyList<string> errors = level.Validate();

            Assert.That(errors.Count, Is.EqualTo(0));
        }

        [Test]
        public void ConfigurationHasNoAmmoInventoryFieldsOrProperties()
        {
            Assert.That(typeof(LevelDefinitionSO).GetProperty("StartingAmmo"), Is.Null);
            Assert.That(typeof(LevelDefinitionSO).GetProperty("MaxAmmo"), Is.Null);
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            Assert.That(typeof(LevelDefinitionSO).GetField("_startingAmmo", flags), Is.Null);
            Assert.That(typeof(LevelDefinitionSO).GetField("_maxAmmo", flags), Is.Null);
        }

        [Test]
        public void Validate_StartingHealthAboveMaxHealth_ReportsInvariantViolation()
        {
            LevelDefinitionSO level = CreateValidLevel();
            ConfigTestReflection.SetField(level, "_maxHealth", 50);
            ConfigTestReflection.SetField(level, "_startingHealth", 60);

            IReadOnlyList<string> errors = level.Validate();

            Assert.That(ConfigTestReflection.AnyContains(errors, "StartingHealth"), Is.True);
        }

        [Test]
        public void Validate_StartingShieldAboveMaxShield_ReportsInvariantViolation()
        {
            LevelDefinitionSO level = CreateValidLevel();
            ConfigTestReflection.SetField(level, "_maxShield", 0);
            ConfigTestReflection.SetField(level, "_startingShield", 10);

            IReadOnlyList<string> errors = level.Validate();

            Assert.That(ConfigTestReflection.AnyContains(errors, "StartingShield"), Is.True);
        }

        [Test]
        public void Validate_WaveSpawnEntryWithoutEnemy_ReportsNestedPath()
        {
            LevelDefinitionSO level = CreateValidLevel();
            var spawn = new LevelDefinitionSO.WaveSpawnEntry();
            ConfigTestReflection.SetField(spawn, "_count", 5);
            ConfigTestReflection.SetField(spawn, "_spawnIntervalSeconds", 0.8f);

            var wave = new LevelDefinitionSO.WaveDefinitionEntry();
            ConfigTestReflection.SetField(wave, "_durationSeconds", 20f);
            ConfigTestReflection.SetField(wave, "_spawns", new List<LevelDefinitionSO.WaveSpawnEntry> { spawn });

            ConfigTestReflection.SetField(level, "_waves", new List<LevelDefinitionSO.WaveDefinitionEntry> { wave });

            IReadOnlyList<string> errors = level.Validate();

            Assert.That(ConfigTestReflection.AnyContains(errors, "Waves[0].Spawns[0].Enemy"), Is.True);
        }

        [Test]
        public void Validate_MaxShieldZero_IsPermittedWithNoShieldMechanic()
        {
            LevelDefinitionSO level = CreateValidLevel();
            ConfigTestReflection.SetField(level, "_maxShield", 0);
            ConfigTestReflection.SetField(level, "_startingShield", 0);

            IReadOnlyList<string> errors = level.Validate();

            Assert.That(errors.Count, Is.EqualTo(0));
        }
    }
}
