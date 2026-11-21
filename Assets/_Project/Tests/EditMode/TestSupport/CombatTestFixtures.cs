using System.Collections.Generic;
using MathHorde.Core.Levels;
using MathHorde.Core.Progress;

namespace MathHorde.Tests.EditMode.TestSupport
{
    /// <summary>
    /// Shared builders for F1-06/07/08 tests (CombatService, WaveDirector,
    /// GameSessionService) — keeps each test's arrange step to the one or
    /// two parameters it actually cares about.
    /// </summary>
    public static class CombatTestFixtures
    {
        public static LevelDefinition BuildLevel(
            int laneCount = 3,
            int startingHealth = 100,
            int maxHealth = 100,
            int startingShield = 0,
            int maxShield = 0,
            float requiredMathAccuracy = 0f,
            IReadOnlyList<WaveDefinition> waves = null,
            string bossEnemyId = null,
            string weaponId = "basic_weapon",
            IReadOnlyList<MathEventScheduleEntry> mathEvents = null,
            IReadOnlyList<string> availableWeaponIds = null)
        {
            return new LevelDefinition(
                id: new LevelId("w1-l01"),
                worldId: new WorldId("w1"),
                displayNameKey: "level.w1l01.name",
                laneCount: laneCount,
                targetDurationSeconds: 200,
                waves: waves,
                mathEvents: mathEvents,
                bossEnemyId: bossEnemyId,
                startingHealth: startingHealth,
                maxHealth: maxHealth,
                startingShield: startingShield,
                maxShield: maxShield,
                requiredMathAccuracy: requiredMathAccuracy,
                weaponId: weaponId, availableWeaponIds: availableWeaponIds ?? new[] { weaponId });
        }

        public static WeaponDefinition BuildWeapon(
            string weaponId = "basic_weapon",
            int damage = 10,
            float fireRate = 10f,
            float range = 20f,
            float projectileSpeed = 0f,
            TargetingMode targetingMode = TargetingMode.OwnLaneThenAdjacent)
        {
            return new WeaponDefinition(weaponId, damage, fireRate, range, projectileSpeed, targetingMode, "fx_basic", displayNameKey: "weapon.test");
        }

        public static EnemyDefinition BuildEnemy(
            string enemyId = "basic_bot",
            EnemyType type = EnemyType.Basic,
            int maxHealth = 10,
            float movementSpeed = 1f,
            int contactDamage = 5,
            float attackCooldownSeconds = 1f)
        {
            return new EnemyDefinition(enemyId, type, maxHealth, movementSpeed, contactDamage, attackCooldownSeconds, "prefab_" + enemyId);
        }

        public static IReadOnlyDictionary<string, EnemyDefinition> BuildCatalog(params EnemyDefinition[] enemies)
        {
            var dict = new Dictionary<string, EnemyDefinition>();
            foreach (var enemy in enemies)
            {
                dict[enemy.EnemyId] = enemy;
            }

            return dict;
        }
    }
}
