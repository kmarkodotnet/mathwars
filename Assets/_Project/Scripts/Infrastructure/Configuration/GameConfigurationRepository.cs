// GameConfigurationRepository.cs
// F1-10 — loads the Configuration/ ScriptableObject assets built in F1-09
// (EnemyDefinitionSO, WeaponConfigSO, LevelDefinitionSO, CombatBalanceSO,
// DeveloperSettingsSO) and translates them into Unity-free DTOs before
// GameLogic ever sees them (ARCHITECTURE.md 8., core-domain.md 8., contract
// scriptable-object-schema.md header note: "A GameConfigurationRepository
// minden SO-t Core DTO-ra fordít, mielőtt a GameLogic megkapja — a GameLogic
// soha nem lát ScriptableObject-et.").
//
// Scope note (read before extending this class): the task header names four
// translation TARGETS — Core.Levels.EnemyDefinition, WeaponDefinition,
// WaveDefinition, LevelDefinition — compiled from five SOURCE SO types.
// CombatBalanceSO does not map onto any of those four; its five multiplier
// fields are the GameLogic.CombatModifiers struct that ICombatService/
// IWaveDirector already consume (game-logic.md 3./4.), so GetCombatModifiers()
// translates it there instead. DeveloperSettingsSO is loaded and VALIDATED
// here (a broken dev-settings asset must fail fast, same as any other config),
// but is deliberately NOT translated to a DTO: no Core/GameLogic contract
// currently defines a target shape for it (no consumer exists yet in Phase 1),
// and inventing one here would be scope creep beyond this task's brief.
//
// CONTRACT GAP (flagged, not silently resolved — see F1-10's closing report):
// this class does not implement the contract's IConfigurationProvider
// interface (game-logic.md 11.). That interface's GetWorld/GetAllWorlds
// return Core.Levels.WorldDefinition and GetDifficultyForLevel returns
// Core.Skills.DifficultyProfile — NEITHER TYPE EXISTS IN CODE YET
// (WorldDefinition is F5 scope; DifficultyProfile needs F2's
// MathDifficultyTableSO). Materializing the full interface is left to
// whichever F2/F5 task introduces those Core types; until then this class
// exposes plain public methods for exactly what F1 needs (and what
// GameSessionService.StartSession already consumes: LevelDefinition,
// WeaponDefinition, an IReadOnlyDictionary&lt;string, EnemyDefinition&gt;
// enemy catalog).
using System;
using System.Collections.Generic;
using System.Linq;
using MathHorde.Core.Common;
using MathHorde.Core.Levels;
using MathHorde.Core.Progress;
using MathHorde.GameLogic;

namespace MathHorde.Infrastructure.Configuration
{
    public sealed class GameConfigurationRepository
    {
        private readonly Dictionary<string, EnemyDefinition> _enemies;
        private readonly Dictionary<string, WeaponDefinition> _weapons;
        private readonly Dictionary<string, LevelDefinition> _levels;
        private readonly CombatModifiers _combatModifiers;
        private readonly bool _hasCombatModifiers;

        public GameConfigurationRepository(
            IReadOnlyList<EnemyDefinitionSO> enemyAssets,
            IReadOnlyList<WeaponConfigSO> weaponAssets,
            IReadOnlyList<LevelDefinitionSO> levelAssets,
            CombatBalanceSO combatBalance = null,
            DeveloperSettingsSO developerSettings = null)
        {
            if (enemyAssets == null)
            {
                throw new ArgumentNullException(nameof(enemyAssets));
            }

            if (weaponAssets == null)
            {
                throw new ArgumentNullException(nameof(weaponAssets));
            }

            if (levelAssets == null)
            {
                throw new ArgumentNullException(nameof(levelAssets));
            }

            var errors = new List<string>();

            Dictionary<string, EnemyDefinition> enemies = BuildEnemyCatalog(enemyAssets, errors);
            Dictionary<string, WeaponDefinition> weapons = BuildWeaponCatalog(weaponAssets, errors);
            Dictionary<string, LevelDefinition> levels = BuildLevelCatalog(levelAssets, enemies, weapons, errors);

            if (combatBalance != null)
            {
                CollectAssetErrors(errors, combatBalance, combatBalance.name, "CombatBalanceSO");
            }

            if (developerSettings != null)
            {
                CollectAssetErrors(errors, developerSettings, developerSettings.name, "DeveloperSettingsSO");
            }

            if (errors.Count > 0)
            {
                throw new ConfigurationValidationException(errors);
            }

            _enemies = enemies;
            _weapons = weapons;
            _levels = levels;

            if (combatBalance != null)
            {
                _combatModifiers = new CombatModifiers(
                    combatBalance.EnemySpeedMultiplier,
                    combatBalance.EnemyHealthMultiplier,
                    combatBalance.EnemyDamageMultiplier,
                    combatBalance.SpawnRateMultiplier,
                    combatBalance.AmmoConsumptionMultiplier);
                _hasCombatModifiers = true;
            }
        }

        /// <summary>Contract game-logic.md 11. IConfigurationProvider.GetEnemy(string).</summary>
        public EnemyDefinition GetEnemy(string enemyId)
        {
            if (_enemies.TryGetValue(enemyId ?? string.Empty, out EnemyDefinition definition))
            {
                return definition;
            }

            throw new KeyNotFoundException(
                $"Enemy configuration not found: '{enemyId}'. Known ids: {string.Join(", ", _enemies.Keys)}.");
        }

        /// <summary>
        /// The full enemy catalog, in the exact shape GameSessionService.StartSession /
        /// ICombatService.Initialize already expect (game-logic.md 3./8.).
        /// </summary>
        public IReadOnlyDictionary<string, EnemyDefinition> GetAllEnemies() => _enemies;

        /// <summary>Contract game-logic.md 11. IConfigurationProvider.GetWeapon(string).</summary>
        public WeaponDefinition GetWeapon(string weaponId)
        {
            if (_weapons.TryGetValue(weaponId ?? string.Empty, out WeaponDefinition definition))
            {
                return definition;
            }

            throw new KeyNotFoundException(
                $"Weapon configuration not found: '{weaponId}'. Known ids: {string.Join(", ", _weapons.Keys)}.");
        }

        /// <summary>Contract game-logic.md 11. IConfigurationProvider.GetLevel(LevelId).</summary>
        public LevelDefinition GetLevel(LevelId id)
        {
            if (_levels.TryGetValue(id.Value ?? string.Empty, out LevelDefinition definition))
            {
                return definition;
            }

            throw new KeyNotFoundException(
                $"Level configuration not found: '{id.Value}'. Known ids: {string.Join(", ", _levels.Keys)}.");
        }

        /// <summary>
        /// CombatBalanceSO's five multipliers, translated to the GameLogic.CombatModifiers
        /// struct that IWaveDirector.ApplyCombatModifiers/ICombatService already consume
        /// (see this file's header scope note — CombatBalanceSO has no Core.Levels DTO).
        /// </summary>
        public CombatModifiers GetCombatModifiers()
        {
            if (!_hasCombatModifiers)
            {
                throw new InvalidOperationException(
                    "No CombatBalanceSO was supplied to this GameConfigurationRepository; GetCombatModifiers() is unavailable without one.");
            }

            return _combatModifiers;
        }

        private static Dictionary<string, EnemyDefinition> BuildEnemyCatalog(
            IReadOnlyList<EnemyDefinitionSO> assets, List<string> errors)
        {
            var catalog = new Dictionary<string, EnemyDefinition>(StringComparer.Ordinal);

            foreach (EnemyDefinitionSO asset in assets)
            {
                if (asset == null)
                {
                    errors.Add("Enemy catalog contains a null EnemyDefinitionSO reference.");
                    continue;
                }

                if (!CollectAssetErrors(errors, asset, asset.name, "EnemyDefinitionSO"))
                {
                    continue;
                }

                if (catalog.ContainsKey(asset.Id))
                {
                    errors.Add($"Duplicate EnemyDefinitionSO.Id '{asset.Id}' (asset '{asset.name}').");
                    continue;
                }

                catalog[asset.Id] = new EnemyDefinition(
                    enemyId: asset.Id,
                    type: asset.Type,
                    maxHealth: asset.MaxHealth,
                    movementSpeed: asset.MovementSpeed,
                    contactDamage: asset.ContactDamage,
                    attackCooldownSeconds: asset.AttackCooldownSeconds,
                    rewardValue: asset.RewardValue,
                    // No PrefabKey field exists on EnemyDefinitionSO (it carries a direct
                    // Prefab reference for the editor instead) — EnemyId already IS the
                    // catalog key Presentation resolves against (EnemyVisualCatalogSO,
                    // F1-14), so it doubles as the PrefabKey.
                    prefabKey: asset.Id);
            }

            return catalog;
        }

        private static Dictionary<string, WeaponDefinition> BuildWeaponCatalog(
            IReadOnlyList<WeaponConfigSO> assets, List<string> errors)
        {
            var catalog = new Dictionary<string, WeaponDefinition>(StringComparer.Ordinal);

            foreach (WeaponConfigSO asset in assets)
            {
                if (asset == null)
                {
                    errors.Add("Weapon catalog contains a null WeaponConfigSO reference.");
                    continue;
                }

                if (!CollectAssetErrors(errors, asset, asset.name, "WeaponConfigSO"))
                {
                    continue;
                }

                if (catalog.ContainsKey(asset.Id))
                {
                    errors.Add($"Duplicate WeaponConfigSO.Id '{asset.Id}' (asset '{asset.name}').");
                    continue;
                }

                // CONTRACT GAP (flagged): WeaponDefinition (core-domain.md 7.) has a single
                // VisualEffectKey field, but WeaponConfigSO (scriptable-object-schema.md 2.)
                // has TWO — MuzzleEffectKey and HitEffectKey — plus AdjacentLanePenalty, none
                // of which the Core DTO has room for. MuzzleEffectKey is mapped here (the
                // "weapon fires" cue is the closer match to a generic VisualEffectKey);
                // HitEffectKey and AdjacentLanePenalty have no destination and are dropped.
                // Not fixed here — would mean changing the Core contract type, which only the
                // architect agent may do.
                catalog[asset.Id] = new WeaponDefinition(
                    weaponId: asset.Id,
                    damage: asset.Damage,
                    fireRate: asset.FireRate,
                    range: asset.Range,
                    ammoPerShot: asset.AmmoPerShot,
                    projectileSpeed: asset.ProjectileSpeed,
                    targetingMode: asset.TargetingMode,
                    visualEffectKey: asset.MuzzleEffectKey);
            }

            return catalog;
        }

        private static Dictionary<string, LevelDefinition> BuildLevelCatalog(
            IReadOnlyList<LevelDefinitionSO> assets,
            Dictionary<string, EnemyDefinition> enemies,
            Dictionary<string, WeaponDefinition> weapons,
            List<string> errors)
        {
            var catalog = new Dictionary<string, LevelDefinition>(StringComparer.Ordinal);

            foreach (LevelDefinitionSO asset in assets)
            {
                if (asset == null)
                {
                    errors.Add("Level catalog contains a null LevelDefinitionSO reference.");
                    continue;
                }

                if (!CollectAssetErrors(errors, asset, asset.name, "LevelDefinitionSO"))
                {
                    continue;
                }

                if (catalog.ContainsKey(asset.Id))
                {
                    errors.Add($"Duplicate LevelDefinitionSO.Id '{asset.Id}' (asset '{asset.name}').");
                    continue;
                }

                string weaponId = asset.Weapon.Id;
                if (!weapons.ContainsKey(weaponId))
                {
                    errors.Add(
                        $"Level '{asset.Id}' references weapon '{weaponId}', which is missing or invalid in the supplied weapon catalog.");
                    continue;
                }

                var waves = new List<WaveDefinition>(asset.Waves.Count);
                bool waveReferenceFailed = false;

                foreach (LevelDefinitionSO.WaveDefinitionEntry waveEntry in asset.Waves)
                {
                    var spawns = new List<WaveSpawnEntry>(waveEntry.Spawns.Count);

                    foreach (LevelDefinitionSO.WaveSpawnEntry spawnEntry in waveEntry.Spawns)
                    {
                        string enemyId = spawnEntry.Enemy.Id;
                        if (!enemies.ContainsKey(enemyId))
                        {
                            errors.Add(
                                $"Level '{asset.Id}' wave {waveEntry.WaveIndex} references enemy '{enemyId}', which is missing or invalid in the supplied enemy catalog.");
                            waveReferenceFailed = true;
                            continue;
                        }

                        IReadOnlyList<LaneIndex> allowedLanes = spawnEntry.AllowedLanes
                            .Select(lane => new LaneIndex(lane))
                            .ToArray();

                        spawns.Add(new WaveSpawnEntry(
                            enemyId: enemyId,
                            count: spawnEntry.Count,
                            spawnIntervalSeconds: spawnEntry.SpawnIntervalSeconds,
                            allowedLanes: allowedLanes));
                    }

                    waves.Add(new WaveDefinition(
                        waveIndex: waveEntry.WaveIndex,
                        startGameTime: waveEntry.StartGameTime,
                        durationSeconds: waveEntry.DurationSeconds,
                        spawns: spawns));
                }

                if (waveReferenceFailed)
                {
                    continue;
                }

                // WorldId/MathEvents/BossEnemyId/RequiredMathAccuracy have no backing field on
                // LevelDefinitionSO yet — F1-09 deliberately left them out as F3/F5-tagged
                // columns (see that file's own header comment). Using the documented
                // "not present yet" defaults: WorldId empty (worlds.md: "null ⇒ kezdettől
                // nyitva"), no math events, no boss, no math-accuracy minimum.
                catalog[asset.Id] = new LevelDefinition(
                    id: new LevelId(asset.Id),
                    // NOT `default(WorldId)` — that bypasses the constructor and leaves
                    // Value == null instead of "" (WorldId's ctor normalizes null -> "").
                    worldId: new WorldId(null),
                    displayNameKey: asset.DisplayNameKey,
                    laneCount: asset.LaneCount,
                    targetDurationSeconds: asset.TargetDurationSeconds,
                    waves: waves,
                    mathEvents: Array.Empty<MathEventScheduleEntry>(),
                    bossEnemyId: null,
                    startingAmmo: asset.StartingAmmo,
                    maxAmmo: asset.MaxAmmo,
                    startingHealth: asset.StartingHealth,
                    maxHealth: asset.MaxHealth,
                    startingShield: asset.StartingShield,
                    maxShield: asset.MaxShield,
                    requiredMathAccuracy: 0f,
                    weaponId: weaponId);
            }

            return catalog;
        }

        /// <summary>Runs an IValidatableConfig asset's own Validate() and appends any errors
        /// (prefixed with the asset's type and name) to the shared error list. Returns false
        /// if the asset had any errors (so the caller can skip translating/cataloguing it).</summary>
        private static bool CollectAssetErrors(
            List<string> errors, IValidatableConfig asset, string assetName, string assetTypeName)
        {
            IReadOnlyList<string> assetErrors = asset.Validate();
            if (assetErrors.Count == 0)
            {
                return true;
            }

            foreach (string error in assetErrors)
            {
                errors.Add($"{assetTypeName} '{assetName}': {error}");
            }

            return false;
        }
    }
}
