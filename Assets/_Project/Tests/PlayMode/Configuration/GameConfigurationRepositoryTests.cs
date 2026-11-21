// GameConfigurationRepositoryTests.cs
// F1-10 — GameConfigurationRepository translates the Configuration/ SOs built in
// F1-09 into Unity-free Core.Levels DTOs. PlayMode, not EditMode: Infrastructure
// references UnityEngine (unity-csharp skill, "Buktatók (ScriptableObject-konfig,
// F1-09)" section) so the mirror dotnet solution cannot link this type.
using System.Collections.Generic;
using System.Linq;
using MathHorde.Core.Levels;
using MathHorde.Core.Problems;
using MathHorde.Core.Progress;
using MathHorde.Core.Skills;
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
            ConfigTestReflection.SetField(level, "_availableWeapons", new List<WeaponConfigSO> { weapon });
            ConfigTestReflection.SetField(level, "_environmentPrefab", environmentPrefab);
            ConfigTestReflection.SetField(level, "_waves", new List<LevelDefinitionSO.WaveDefinitionEntry> { wave });
            ConfigTestReflection.SetField(level, "_mathEvents", ConfigTestReflection.MathSchedule());

            return level;
        }

        private CombatBalanceSO CreateValidCombatBalance()
        {
            var combatBalance = ScriptableObject.CreateInstance<CombatBalanceSO>();
            _created.Add(combatBalance);
            return combatBalance;
        }

        [Test]
        public void AvailableWeaponsTranslateInOrder_WithNamesAndImmutableCopiedIds()
        {
            var enemy = CreateValidEnemy(); var starter = CreateValidWeapon(); var heavy = CreateValidWeapon("heavy");
            ConfigTestReflection.SetField(starter, "_displayNameKey", "weapon.starter");
            ConfigTestReflection.SetField(heavy, "_displayNameKey", "weapon.heavy");
            var level = CreateValidLevel("weapon-level", starter, enemy);
            var references = new List<WeaponConfigSO> { starter, heavy };
            ConfigTestReflection.SetField(level, "_availableWeapons", references);
            var repository = new GameConfigurationRepository(new[] { enemy }, new[] { starter, heavy }, new[] { level });
            references.Clear();
            var domain = repository.GetLevel(new LevelId("weapon-level"));
            Assert.That(domain.AvailableWeaponIds.Count, Is.EqualTo(2));
            Assert.That(domain.AvailableWeaponIds[1], Is.EqualTo("heavy"));
            Assert.That(repository.GetWeapon("heavy").DisplayNameKey, Is.EqualTo("weapon.heavy"));
            Assert.That(() => ((IList<string>)domain.AvailableWeaponIds)[0] = "other", Throws.TypeOf<System.NotSupportedException>());
        }

        [TestCase("null")]
        [TestCase("empty")]
        [TestCase("duplicate")]
        [TestCase("missing-reference")]
        [TestCase("unlisted-starter")]
        [TestCase("foreign-reference")]
        [TestCase("unresolved")]
        [TestCase("too-many")]
        public void InvalidAvailableWeaponAssetsAreConfigurationErrors(string variant)
        {
            var enemy = CreateValidEnemy(); var starter = CreateValidWeapon(); var heavy = CreateValidWeapon("heavy");
            var level = CreateValidLevel("weapon-level", starter, enemy);
            var references = new List<WeaponConfigSO> { starter, heavy };
            if (variant == "null") references = null;
            if (variant == "empty") references.Clear();
            if (variant == "duplicate") references.Add(starter);
            if (variant == "missing-reference") references[1] = null;
            if (variant == "unlisted-starter") references.RemoveAt(0);
            if (variant == "foreign-reference") references[1] = CreateValidWeapon("heavy");
            if (variant == "too-many") references.AddRange(new[] { CreateValidWeapon("3"), CreateValidWeapon("4"), CreateValidWeapon("5") });
            ConfigTestReflection.SetField(level, "_availableWeapons", references);
            var catalog = variant == "unresolved" ? new[] { starter } : new[] { starter, heavy };
            Assert.That(() => new GameConfigurationRepository(new[] { enemy }, catalog, new[] { level }), Throws.TypeOf<ConfigurationValidationException>());
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
            Assert.That(typeof(MathHorde.Core.Levels.EnemyDefinition).GetProperty("RewardValue"), Is.Null);
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
            Assert.That(typeof(MathHorde.Core.Levels.WeaponDefinition).GetProperty("AmmoPerShot"), Is.Null);
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

            // World/boss still await F5; F3 schedule is now translated.
            Assert.That(level.WorldId.Value, Is.EqualTo(string.Empty));
            Assert.That(level.MathEvents.Count, Is.EqualTo(10));
            Assert.That(level.MathEvents[0].ProblemType, Is.EqualTo(ProblemType.ResourceOperationComparison));
            Assert.That(level.MathEvents[0].TriggerGameTime, Is.EqualTo(10));
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

            var repository = new GameConfigurationRepository(
                new[] { enemySo }, new[] { weaponSo }, new[] { levelSo }, combatBalance);

            MathHorde.GameLogic.CombatModifiers modifiers = repository.GetCombatModifiers();

            Assert.That(modifiers.EnemySpeedMultiplier, Is.EqualTo(1.3f));
            Assert.That(modifiers.EnemyHealthMultiplier, Is.EqualTo(1.4f));
            Assert.That(modifiers.EnemyDamageMultiplier, Is.EqualTo(1.5f));
            Assert.That(modifiers.SpawnRateMultiplier, Is.EqualTo(1.6f));
            Assert.That(typeof(MathHorde.GameLogic.CombatModifiers).GetProperty("AmmoConsumptionMultiplier"), Is.Null);
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

        // --- F2-10: MathDifficultyTableSO -> Core.Skills.DifficultyProfile ---

        private MathDifficultyTableSO CreateValidMathDifficultyTable()
        {
            // CreateInstance already carries the designed 10 rows (the SO's field
            // default is the contract's "Default (szint 1 -> 10)" column).
            var table = ScriptableObject.CreateInstance<MathDifficultyTableSO>();
            _created.Add(table);
            return table;
        }

        private GameConfigurationRepository CreateRepositoryWithDifficultyTable(
            MathDifficultyTableSO table)
        {
            EnemyDefinitionSO enemySo = CreateValidEnemy();
            WeaponConfigSO weaponSo = CreateValidWeapon();
            LevelDefinitionSO levelSo = CreateValidLevel("w1-l01", weaponSo, enemySo);

            return new GameConfigurationRepository(
                new[] { enemySo }, new[] { weaponSo }, new[] { levelSo },
                combatBalance: null, developerSettings: null, mathDifficultyTable: table);
        }

        [Test]
        public void GetDifficultyForLevel_TranslatesTheRowIntoACoreDifficultyProfile()
        {
            MathDifficultyTableSO table = CreateValidMathDifficultyTable();
            GameConfigurationRepository repository = CreateRepositoryWithDifficultyTable(table);
            MathDifficultyTableSO.MathDifficultyLevelEntry row = table.GetLevel(8);

            DifficultyProfile profile = repository.GetDifficultyForLevel(8);

            Assert.That(profile.ChallengeLevel, Is.EqualTo(8));
            Assert.That(profile.MathComplexity, Is.EqualTo(row.MathComplexity));
            Assert.That(profile.TimePressure, Is.EqualTo(row.TimePressure));
            Assert.That(profile.CombatDifficulty, Is.EqualTo(row.CombatDifficulty));
            Assert.That(profile.AllowedOperators.Count, Is.EqualTo(row.AllowedOperators.Count));
            Assert.That(profile.MinOperandValue, Is.EqualTo(row.MinOperandValue));
            Assert.That(profile.MaxOperandValue, Is.EqualTo(row.MaxOperandValue));
            Assert.That(profile.MaxResultValue, Is.EqualTo(row.MaxResultValue));
            Assert.That(profile.MinOperandCount, Is.EqualTo(row.MinOperandCount));
            Assert.That(profile.MaxOperandCount, Is.EqualTo(row.MaxOperandCount));
            Assert.That(profile.AllowParentheses, Is.EqualTo(row.AllowParentheses));
            Assert.That(profile.MaxParenthesisDepth, Is.EqualTo(row.MaxParenthesisDepth));
            Assert.That(profile.AllowUnknownOperand, Is.EqualTo(row.AllowUnknownOperand));
            Assert.That(profile.UnknownOperandCount, Is.EqualTo(row.UnknownOperandCount));
            Assert.That(profile.AllowNegativeResults, Is.EqualTo(row.AllowNegativeResults));
            Assert.That(profile.AnswerOptionCount, Is.EqualTo(row.AnswerOptionCount));
            Assert.That(profile.BaseThinkTimeSeconds, Is.EqualTo(row.BaseThinkTimeSeconds));
            Assert.That(profile.MinGateGap, Is.EqualTo(row.MinGateGap));
            Assert.That(profile.AutoAdaptationEnabled, Is.True);
        }

        [Test]
        public void GetDifficultyForLevel_UnlockedSkillsIsTheCumulativeUnionUpToThatLevel()
        {
            MathDifficultyTableSO table = CreateValidMathDifficultyTable();
            GameConfigurationRepository repository = CreateRepositoryWithDifficultyTable(table);

            DifficultyProfile level1 = repository.GetDifficultyForLevel(1);
            DifficultyProfile level3 = repository.GetDifficultyForLevel(3);

            // Level 1's own skills are still there at level 3 (nothing is forgotten) ...
            foreach (SkillTag tag in level1.UnlockedSkills)
            {
                Assert.That(level3.IsSkillUnlocked(tag), Is.True);
            }

            // ... and level 3's newly introduced skills are NOT present at level 1.
            Assert.That(level1.IsSkillUnlocked(SkillTag.Multiplication), Is.False);
            Assert.That(level3.IsSkillUnlocked(SkillTag.Multiplication), Is.True);
            Assert.That(level3.UnlockedSkills.Count, Is.GreaterThan(level1.UnlockedSkills.Count));
        }

        [Test]
        public void GetDifficultyForLevel_EveryLevelOneToTenTranslates()
        {
            MathDifficultyTableSO table = CreateValidMathDifficultyTable();
            GameConfigurationRepository repository = CreateRepositoryWithDifficultyTable(table);

            for (int level = 1; level <= MathDifficultyTableSO.RequiredLevelCount; level++)
            {
                DifficultyProfile profile = repository.GetDifficultyForLevel(level);
                Assert.That(profile.ChallengeLevel, Is.EqualTo(level));
            }
        }

        [Test]
        public void GetDifficultyForLevel_UsesEditedAssetValuesAndSnapshotsThem()
        {
            MathDifficultyTableSO table = CreateValidMathDifficultyTable();
            MathDifficultyTableSO.MathDifficultyLevelEntry row = table.GetLevel(3);
            ConfigTestReflection.SetField(row, "_maxOperandValue", 35);
            ConfigTestReflection.SetField(row, "_maxResultValue", 15);
            ConfigTestReflection.SetField(row, "_mathComplexity", 2);
            ConfigTestReflection.SetField(row, "_timePressure", 1);
            ConfigTestReflection.SetField(row, "_combatDifficulty", 4);
            GameConfigurationRepository repository = CreateRepositoryWithDifficultyTable(table);

            ConfigTestReflection.SetField(row, "_maxOperandValue", 999);
            ConfigTestReflection.SetField(row, "_requiredSkills", null);
            DifficultyProfile profile = repository.GetDifficultyForLevel(3);

            Assert.That(profile.MaxOperandValue, Is.EqualTo(35));
            Assert.That(profile.MaxResultValue, Is.EqualTo(15));
            Assert.That(profile.MathComplexity, Is.EqualTo(2));
            Assert.That(profile.TimePressure, Is.EqualTo(1));
            Assert.That(profile.CombatDifficulty, Is.EqualTo(4));
            Assert.That(profile.IsSkillUnlocked(SkillTag.Multiplication), Is.True);
        }

        [Test]
        public void GetDifficultyForLevel_WithoutATable_ThrowsInvalidOperation()
        {
            GameConfigurationRepository repository = CreateRepositoryWithDifficultyTable(null);

            Assert.That(
                () => repository.GetDifficultyForLevel(1),
                Throws.TypeOf<System.InvalidOperationException>());
        }

        [Test]
        public void GetDifficultyForLevel_WithLevelOutsideTheTable_ThrowsKeyNotFound()
        {
            MathDifficultyTableSO table = CreateValidMathDifficultyTable();
            GameConfigurationRepository repository = CreateRepositoryWithDifficultyTable(table);

            Assert.That(
                () => repository.GetDifficultyForLevel(11),
                Throws.TypeOf<KeyNotFoundException>());
        }

        [Test]
        public void Constructor_InvalidMathDifficultyTable_ThrowsConfigurationValidationException()
        {
            MathDifficultyTableSO table = CreateValidMathDifficultyTable();
            ConfigTestReflection.SetField(
                table, "_levels", new List<MathDifficultyTableSO.MathDifficultyLevelEntry>());

            Assert.That(
                () => CreateRepositoryWithDifficultyTable(table),
                Throws.TypeOf<ConfigurationValidationException>());
        }
    }
}
