// GameConfigurationRepositoryTests.cs
// F1-10 — GameConfigurationRepository translates the Configuration/ SOs built in
// F1-09 into Unity-free Core.Levels DTOs. PlayMode, not EditMode: Infrastructure
// references UnityEngine (unity-csharp skill, "Buktatók (ScriptableObject-konfig,
// F1-09)" section) so the mirror dotnet solution cannot link this type.
using System.Collections.Generic;
using System.Linq;
using MathHorde.Core.Levels;
using MathHorde.Core.Progress;
using MathHorde.Infrastructure.Configuration;
using MathHorde.Tests.PlayMode.TestSupport;
using NUnit.Framework;
using UnityEngine;

namespace MathHorde.Tests.PlayMode.Configuration
{
    [TestFixture]
    public class GameConfigurationRepositoryTests
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

        private EnemyDefinitionSO CreateValidEnemy(string id = "basic_bot")
        {
            var enemy = ScriptableObject.CreateInstance<EnemyDefinitionSO>();
            _created.Add(enemy);
            var prefab = new GameObject("dummy_enemy_prefab");
            _created.Add(prefab);
            ConfigTestReflection.SetField(enemy, "_id", id);
            ConfigTestReflection.SetField(enemy, "_prefab", prefab);
            return enemy;
        }

        private WeaponConfigSO CreateValidWeapon(string id = "starter_blaster")
        {
            var weapon = ScriptableObject.CreateInstance<WeaponConfigSO>();
            _created.Add(weapon);
            var projectilePrefab = new GameObject("dummy_projectile_prefab");
            _created.Add(projectilePrefab);
            ConfigTestReflection.SetField(weapon, "_id", id);
            ConfigTestReflection.SetField(weapon, "_projectilePrefab", projectilePrefab);
            return weapon;
        }

        private LevelDefinitionSO CreateValidLevel(
            string id, WeaponConfigSO weapon, EnemyDefinitionSO enemy, IReadOnlyList<int> allowedLanes = null)
        {
            var level = ScriptableObject.CreateInstance<LevelDefinitionSO>();
            _created.Add(level);

            var environmentPrefab = new GameObject("dummy_environment_prefab");
            _created.Add(environmentPrefab);

            var spawn = new LevelDefinitionSO.WaveSpawnEntry();
            ConfigTestReflection.SetField(spawn, "_enemy", enemy);
            ConfigTestReflection.SetField(spawn, "_count", 5);
            ConfigTestReflection.SetField(spawn, "_spawnIntervalSeconds", 0.8f);
            ConfigTestReflection.SetField(spawn, "_allowedLanes", (allowedLanes ?? System.Array.Empty<int>()).ToList());

            var wave = new LevelDefinitionSO.WaveDefinitionEntry();
            ConfigTestReflection.SetField(wave, "_waveIndex", 0);
            ConfigTestReflection.SetField(wave, "_startGameTime", 0f);
            ConfigTestReflection.SetField(wave, "_durationSeconds", 20f);
            ConfigTestReflection.SetField(wave, "_spawns", new List<LevelDefinitionSO.WaveSpawnEntry> { spawn });

            ConfigTestReflection.SetField(level, "_id", id);
            ConfigTestReflection.SetField(level, "_displayNameKey", "level." + id);
            ConfigTestReflection.SetField(level, "_weapon", weapon);
            ConfigTestReflection.SetField(level, "_environmentPrefab", environmentPrefab);
            ConfigTestReflection.SetField(level, "_waves", new List<LevelDefinitionSO.WaveDefinitionEntry> { wave });

            return level;
        }

        private CombatBalanceSO CreateValidCombatBalance()
        {
            var combatBalance = ScriptableObject.CreateInstance<CombatBalanceSO>();
            _created.Add(combatBalance);
            return combatBalance;
        }

        private DeveloperSettingsSO CreateValidDeveloperSettings()
        {
            var devSettings = ScriptableObject.CreateInstance<DeveloperSettingsSO>();
            _created.Add(devSettings);
            return devSettings;
        }

        [Test]
        public void GetEnemy_TranslatesAllFieldsToCoreEnemyDefinition()
        {
            EnemyDefinitionSO enemySo = CreateValidEnemy("basic_bot");
            WeaponConfigSO weaponSo = CreateValidWeapon();
            LevelDefinitionSO levelSo = CreateValidLevel("w1-l01", weaponSo, enemySo);

            var repository = new GameConfigurationRepository(
                new[] { enemySo }, new[] { weaponSo }, new[] { levelSo });

            EnemyDefinition enemy = repository.GetEnemy("basic_bot");

            Assert.That(enemy.EnemyId, Is.EqualTo("basic_bot"));
            Assert.That(enemy.Type, Is.EqualTo(enemySo.Type));
            Assert.That(enemy.MaxHealth, Is.EqualTo(enemySo.MaxHealth));
            Assert.That(enemy.MovementSpeed, Is.EqualTo(enemySo.MovementSpeed));
            Assert.That(enemy.ContactDamage, Is.EqualTo(enemySo.ContactDamage));
            Assert.That(enemy.AttackCooldownSeconds, Is.EqualTo(enemySo.AttackCooldownSeconds));
            Assert.That(enemy.RewardValue, Is.EqualTo(enemySo.RewardValue));
            // No PrefabKey field on EnemyDefinitionSO (direct Prefab reference instead) —
            // EnemyId doubles as the catalog key (see GameConfigurationRepository header note).
            Assert.That(enemy.PrefabKey, Is.EqualTo("basic_bot"));
        }

        [Test]
        public void GetAllEnemies_ReturnsFullCatalogKeyedById()
        {
            EnemyDefinitionSO enemySo = CreateValidEnemy("basic_bot");
            WeaponConfigSO weaponSo = CreateValidWeapon();
            LevelDefinitionSO levelSo = CreateValidLevel("w1-l01", weaponSo, enemySo);

            var repository = new GameConfigurationRepository(
                new[] { enemySo }, new[] { weaponSo }, new[] { levelSo });

            IReadOnlyDictionary<string, EnemyDefinition> all = repository.GetAllEnemies();

            Assert.That(all.Count, Is.EqualTo(1));
            Assert.That(all.ContainsKey("basic_bot"), Is.True);
        }

        [Test]
        public void GetEnemy_UnknownId_ThrowsKeyNotFoundException()
        {
            EnemyDefinitionSO enemySo = CreateValidEnemy("basic_bot");
            WeaponConfigSO weaponSo = CreateValidWeapon();
            LevelDefinitionSO levelSo = CreateValidLevel("w1-l01", weaponSo, enemySo);

            var repository = new GameConfigurationRepository(
                new[] { enemySo }, new[] { weaponSo }, new[] { levelSo });

            Assert.That(() => repository.GetEnemy("no_such_enemy"), Throws.TypeOf<KeyNotFoundException>());
        }

        [Test]
        public void GetWeapon_TranslatesAllFieldsToCoreWeaponDefinition()
        {
            EnemyDefinitionSO enemySo = CreateValidEnemy();
            WeaponConfigSO weaponSo = CreateValidWeapon("starter_blaster");
            LevelDefinitionSO levelSo = CreateValidLevel("w1-l01", weaponSo, enemySo);

            var repository = new GameConfigurationRepository(
                new[] { enemySo }, new[] { weaponSo }, new[] { levelSo });

            WeaponDefinition weapon = repository.GetWeapon("starter_blaster");

            Assert.That(weapon.WeaponId, Is.EqualTo("starter_blaster"));
            Assert.That(weapon.Damage, Is.EqualTo(weaponSo.Damage));
            Assert.That(weapon.FireRate, Is.EqualTo(weaponSo.FireRate));
            Assert.That(weapon.Range, Is.EqualTo(weaponSo.Range));
            Assert.That(weapon.AmmoPerShot, Is.EqualTo(weaponSo.AmmoPerShot));
            Assert.That(weapon.ProjectileSpeed, Is.EqualTo(weaponSo.ProjectileSpeed));
            Assert.That(weapon.TargetingMode, Is.EqualTo(weaponSo.TargetingMode));
            // Core.Levels.WeaponDefinition has one VisualEffectKey field, the SO has two
            // (Muzzle/Hit) — MuzzleEffectKey is the mapped one (see repository header note).
            Assert.That(weapon.VisualEffectKey, Is.EqualTo(weaponSo.MuzzleEffectKey));
        }

        [Test]
        public void GetWeapon_UnknownId_ThrowsKeyNotFoundException()
        {
            EnemyDefinitionSO enemySo = CreateValidEnemy();
            WeaponConfigSO weaponSo = CreateValidWeapon();
            LevelDefinitionSO levelSo = CreateValidLevel("w1-l01", weaponSo, enemySo);

            var repository = new GameConfigurationRepository(
                new[] { enemySo }, new[] { weaponSo }, new[] { levelSo });

            Assert.That(() => repository.GetWeapon("no_such_weapon"), Throws.TypeOf<KeyNotFoundException>());
        }

        [Test]
        public void GetLevel_TranslatesWavesAndSpawnsIncludingAllowedLanes()
        {
            EnemyDefinitionSO enemySo = CreateValidEnemy("basic_bot");
            WeaponConfigSO weaponSo = CreateValidWeapon("starter_blaster");
            LevelDefinitionSO levelSo = CreateValidLevel("w1-l01", weaponSo, enemySo, new[] { 0, 2 });

            var repository = new GameConfigurationRepository(
                new[] { enemySo }, new[] { weaponSo }, new[] { levelSo });

            LevelDefinition level = repository.GetLevel(new LevelId("w1-l01"));

            Assert.That(level.Id.Value, Is.EqualTo("w1-l01"));
            Assert.That(level.WeaponId, Is.EqualTo("starter_blaster"));
            Assert.That(level.LaneCount, Is.EqualTo(levelSo.LaneCount));
            Assert.That(level.Waves.Count, Is.EqualTo(1));
            Assert.That(level.Waves[0].Spawns.Count, Is.EqualTo(1));
            Assert.That(level.Waves[0].Spawns[0].EnemyId, Is.EqualTo("basic_bot"));
            Assert.That(level.Waves[0].Spawns[0].AllowedLanes.Count, Is.EqualTo(2));
            Assert.That(level.Waves[0].Spawns[0].AllowedLanes[0].Value, Is.EqualTo(0));
            Assert.That(level.Waves[0].Spawns[0].AllowedLanes[1].Value, Is.EqualTo(2));

            // F3/F5-tagged fields LevelDefinitionSO does not carry yet (see F1-09's own
            // header note) translate to the documented "not present" defaults.
            Assert.That(level.WorldId.Value, Is.EqualTo(string.Empty));
            Assert.That(level.MathEvents.Count, Is.EqualTo(0));
            Assert.That(level.BossEnemyId, Is.Null);
            Assert.That(level.RequiredMathAccuracy, Is.EqualTo(0f));
        }

        [Test]
        public void GetLevel_UnknownId_ThrowsKeyNotFoundException()
        {
            EnemyDefinitionSO enemySo = CreateValidEnemy();
            WeaponConfigSO weaponSo = CreateValidWeapon();
            LevelDefinitionSO levelSo = CreateValidLevel("w1-l01", weaponSo, enemySo);

            var repository = new GameConfigurationRepository(
                new[] { enemySo }, new[] { weaponSo }, new[] { levelSo });

            Assert.That(() => repository.GetLevel(new LevelId("no_such_level")), Throws.TypeOf<KeyNotFoundException>());
        }

        [Test]
        public void Constructor_InvalidEnemyAsset_ThrowsConfigurationValidationExceptionNamingTheProblem()
        {
            var brokenEnemy = ScriptableObject.CreateInstance<EnemyDefinitionSO>();
            _created.Add(brokenEnemy);
            // No Prefab assigned -> EnemyDefinitionSO.Validate() reports it.
            WeaponConfigSO weaponSo = CreateValidWeapon();

            ConfigurationValidationException thrown = null;
            try
            {
                _ = new GameConfigurationRepository(
                    new[] { brokenEnemy }, new[] { weaponSo }, System.Array.Empty<LevelDefinitionSO>());
            }
            catch (ConfigurationValidationException ex)
            {
                thrown = ex;
            }

            Assert.That(thrown, Is.Not.Null);
            Assert.That(ConfigTestReflection.AnyContains(thrown.Errors, "Prefab"), Is.True);
        }

        [Test]
        public void Constructor_DuplicateEnemyId_ThrowsConfigurationValidationException()
        {
            EnemyDefinitionSO enemyA = CreateValidEnemy("basic_bot");
            EnemyDefinitionSO enemyB = CreateValidEnemy("basic_bot");
            WeaponConfigSO weaponSo = CreateValidWeapon();

            Assert.That(
                () => new GameConfigurationRepository(
                    new[] { enemyA, enemyB }, new[] { weaponSo }, System.Array.Empty<LevelDefinitionSO>()),
                Throws.TypeOf<ConfigurationValidationException>());
        }

        [Test]
        public void Constructor_LevelReferencesWeaponNotInSuppliedCatalog_ThrowsConfigurationValidationException()
        {
            EnemyDefinitionSO enemySo = CreateValidEnemy();
            WeaponConfigSO weaponSo = CreateValidWeapon();
            LevelDefinitionSO levelSo = CreateValidLevel("w1-l01", weaponSo, enemySo);

            // Weapon catalog passed to the constructor deliberately omits weaponSo.
            Assert.That(
                () => new GameConfigurationRepository(
                    new[] { enemySo }, System.Array.Empty<WeaponConfigSO>(), new[] { levelSo }),
                Throws.TypeOf<ConfigurationValidationException>());
        }

        [Test]
        public void Constructor_LevelReferencesEnemyNotInSuppliedCatalog_ThrowsConfigurationValidationException()
        {
            EnemyDefinitionSO enemySo = CreateValidEnemy();
            WeaponConfigSO weaponSo = CreateValidWeapon();
            LevelDefinitionSO levelSo = CreateValidLevel("w1-l01", weaponSo, enemySo);

            // Enemy catalog passed to the constructor deliberately omits enemySo.
            Assert.That(
                () => new GameConfigurationRepository(
                    System.Array.Empty<EnemyDefinitionSO>(), new[] { weaponSo }, new[] { levelSo }),
                Throws.TypeOf<ConfigurationValidationException>());
        }

        [Test]
        public void Constructor_NullEnemyAssetList_ThrowsArgumentNullException()
        {
            WeaponConfigSO weaponSo = CreateValidWeapon();

            Assert.That(
                () => new GameConfigurationRepository(null, new[] { weaponSo }, System.Array.Empty<LevelDefinitionSO>()),
                Throws.TypeOf<System.ArgumentNullException>());
        }

        [Test]
        public void GetCombatModifiers_WithCombatBalanceSupplied_ReturnsTranslatedMultipliers()
        {
            EnemyDefinitionSO enemySo = CreateValidEnemy();
            WeaponConfigSO weaponSo = CreateValidWeapon();
            LevelDefinitionSO levelSo = CreateValidLevel("w1-l01", weaponSo, enemySo);
            CombatBalanceSO combatBalance = CreateValidCombatBalance();
            ConfigTestReflection.SetField(combatBalance, "_enemySpeedMultiplier", 1.3f);
            ConfigTestReflection.SetField(combatBalance, "_enemyHealthMultiplier", 1.4f);
            ConfigTestReflection.SetField(combatBalance, "_enemyDamageMultiplier", 1.5f);
            ConfigTestReflection.SetField(combatBalance, "_spawnRateMultiplier", 1.6f);
            ConfigTestReflection.SetField(combatBalance, "_ammoConsumptionMultiplier", 1.2f);

            var repository = new GameConfigurationRepository(
                new[] { enemySo }, new[] { weaponSo }, new[] { levelSo }, combatBalance);

            MathHorde.GameLogic.CombatModifiers modifiers = repository.GetCombatModifiers();

            Assert.That(modifiers.EnemySpeedMultiplier, Is.EqualTo(1.3f));
            Assert.That(modifiers.EnemyHealthMultiplier, Is.EqualTo(1.4f));
            Assert.That(modifiers.EnemyDamageMultiplier, Is.EqualTo(1.5f));
            Assert.That(modifiers.SpawnRateMultiplier, Is.EqualTo(1.6f));
            Assert.That(modifiers.AmmoConsumptionMultiplier, Is.EqualTo(1.2f));
        }

        [Test]
        public void GetCombatModifiers_WithoutCombatBalanceSupplied_ThrowsInvalidOperationException()
        {
            EnemyDefinitionSO enemySo = CreateValidEnemy();
            WeaponConfigSO weaponSo = CreateValidWeapon();
            LevelDefinitionSO levelSo = CreateValidLevel("w1-l01", weaponSo, enemySo);

            var repository = new GameConfigurationRepository(
                new[] { enemySo }, new[] { weaponSo }, new[] { levelSo });

            Assert.That(() => repository.GetCombatModifiers(), Throws.TypeOf<System.InvalidOperationException>());
        }

        [Test]
        public void Constructor_InvalidCombatBalance_ThrowsConfigurationValidationException()
        {
            EnemyDefinitionSO enemySo = CreateValidEnemy();
            WeaponConfigSO weaponSo = CreateValidWeapon();
            LevelDefinitionSO levelSo = CreateValidLevel("w1-l01", weaponSo, enemySo);
            CombatBalanceSO combatBalance = CreateValidCombatBalance();
            ConfigTestReflection.SetField(combatBalance, "_enemySpeedMultiplier", 9.9f); // out of [0.5, 2.0]

            Assert.That(
                () => new GameConfigurationRepository(
                    new[] { enemySo }, new[] { weaponSo }, new[] { levelSo }, combatBalance),
                Throws.TypeOf<ConfigurationValidationException>());
        }

        [Test]
        public void Constructor_ValidDeveloperSettingsSupplied_DoesNotThrow()
        {
            EnemyDefinitionSO enemySo = CreateValidEnemy();
            WeaponConfigSO weaponSo = CreateValidWeapon();
            LevelDefinitionSO levelSo = CreateValidLevel("w1-l01", weaponSo, enemySo);
            DeveloperSettingsSO devSettings = CreateValidDeveloperSettings();

            Assert.That(
                () => new GameConfigurationRepository(
                    new[] { enemySo }, new[] { weaponSo }, new[] { levelSo },
                    combatBalance: null, developerSettings: devSettings),
                Throws.Nothing);
        }

        [Test]
        public void Constructor_InvalidDeveloperSettings_ThrowsConfigurationValidationException()
        {
            EnemyDefinitionSO enemySo = CreateValidEnemy();
            WeaponConfigSO weaponSo = CreateValidWeapon();
            LevelDefinitionSO levelSo = CreateValidLevel("w1-l01", weaponSo, enemySo);
            DeveloperSettingsSO devSettings = CreateValidDeveloperSettings();
            ConfigTestReflection.SetField(devSettings, "_forceChallengeLevel", 99); // out of [0, 10]

            Assert.That(
                () => new GameConfigurationRepository(
                    new[] { enemySo }, new[] { weaponSo }, new[] { levelSo },
                    combatBalance: null, developerSettings: devSettings),
                Throws.TypeOf<ConfigurationValidationException>());
        }
    }
}
