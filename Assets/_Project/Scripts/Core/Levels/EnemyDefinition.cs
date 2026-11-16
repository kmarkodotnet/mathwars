using System;

namespace MathHorde.Core.Levels
{
    /// <summary>
    /// Engine-free enemy descriptor compiled from a ScriptableObject (contract
    /// core-domain.md 7., spec 4.1). This is static per-enemy-type balance
    /// data only — the live, mutable per-instance combat state (current
    /// health, position, ...) is CombatService's EnemySnapshot
    /// (game-logic.md 3.), not this type. There is deliberately no
    /// CurrentHealth field here even though the task brief's paraphrase of
    /// spec 4.1 mentions one — the contract's EnemyDefinition has none, and
    /// EnemySnapshot already owns that runtime field.
    /// </summary>
    public sealed class EnemyDefinition
    {
        public string EnemyId { get; }
        public EnemyType Type { get; }
        public int MaxHealth { get; }
        public float MovementSpeed { get; }
        public int ContactDamage { get; }
        public float AttackCooldownSeconds { get; }

        /// <summary>
        /// Kill refund: this many AMMO are credited back via
        /// ApplyCombatReward(Ammo, RewardValue, "KILL_REFUND") when the enemy
        /// dies (game-logic.md 3./7., difficulty-model.md 5. "killRefund").
        /// This is NOT score — score is a separate, F5 ScoringService concern.
        /// </summary>
        public int RewardValue { get; }

        public string PrefabKey { get; }

        public EnemyDefinition(
            string enemyId,
            EnemyType type,
            int maxHealth,
            float movementSpeed,
            int contactDamage,
            float attackCooldownSeconds,
            int rewardValue,
            string prefabKey)
        {
            if (string.IsNullOrEmpty(enemyId))
            {
                throw new ArgumentException("EnemyId nem lehet üres.", nameof(enemyId));
            }

            if (maxHealth <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxHealth), maxHealth, "MaxHealth > 0 kötelező.");
            }

            if (movementSpeed <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(movementSpeed), movementSpeed, "MovementSpeed > 0 kötelező.");
            }

            if (contactDamage < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(contactDamage), contactDamage, "ContactDamage nem lehet negatív.");
            }

            if (attackCooldownSeconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(attackCooldownSeconds), attackCooldownSeconds, "AttackCooldownSeconds nem lehet negatív.");
            }

            if (rewardValue < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(rewardValue), rewardValue, "RewardValue nem lehet negatív.");
            }

            EnemyId = enemyId;
            Type = type;
            MaxHealth = maxHealth;
            MovementSpeed = movementSpeed;
            ContactDamage = contactDamage;
            AttackCooldownSeconds = attackCooldownSeconds;
            RewardValue = rewardValue;
            PrefabKey = prefabKey;
        }
    }
}
