using System;
using System.Collections.Generic;
using MathHorde.Core.Progress;

namespace MathHorde.Core.Levels
{
    /// <summary>
    /// Contract core-domain.md 7. / spec 10., 16.
    /// Revision r2 (ADR-009): MaxShield added as its own field, mirroring the
    /// StartingAmmo/MaxAmmo and StartingHealth/MaxHealth pairs — it is never
    /// derived from StartingShield. MaxShield == 0 means the level has no
    /// shield mechanic at all (ResourceService.Initialize then creates no
    /// Shield pool — see core-domain.md's note after invariant L4/L5).
    /// </summary>
    public sealed class LevelDefinition
    {
        public LevelId Id { get; }
        public WorldId WorldId { get; }
        public string DisplayNameKey { get; }
        public int LaneCount { get; }
        public double TargetDurationSeconds { get; }
        public IReadOnlyList<WaveDefinition> Waves { get; }
        public IReadOnlyList<MathEventScheduleEntry> MathEvents { get; }
        public string BossEnemyId { get; }
        public int StartingAmmo { get; }
        public int MaxAmmo { get; }
        public int StartingHealth { get; }
        public int MaxHealth { get; }
        public int StartingShield { get; }

        /// <summary>
        /// Shield pool capacity (contract core-domain.md 7., ADR-009). 0 means
        /// the level has no shield mechanic — ResourceService.Initialize then
        /// creates no Shield pool at all (never a Max=1 workaround).
        /// </summary>
        public int MaxShield { get; }

        public float RequiredMathAccuracy { get; }
        public string WeaponId { get; }

        public LevelDefinition(
            LevelId id,
            WorldId worldId,
            string displayNameKey,
            int laneCount,
            double targetDurationSeconds,
            IReadOnlyList<WaveDefinition> waves,
            IReadOnlyList<MathEventScheduleEntry> mathEvents,
            string bossEnemyId,
            int startingAmmo,
            int maxAmmo,
            int startingHealth,
            int maxHealth,
            int startingShield,
            int maxShield,
            float requiredMathAccuracy,
            string weaponId)
        {
            if (laneCount < 2 || laneCount > 4)
            {
                throw new ArgumentOutOfRangeException(nameof(laneCount), laneCount, "LaneCount 2..4 kötelező.");
            }

            if (targetDurationSeconds <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(targetDurationSeconds), targetDurationSeconds, "TargetDurationSeconds > 0 kötelező.");
            }

            if (maxAmmo <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxAmmo), maxAmmo, "MaxAmmo > 0 kötelező.");
            }

            if (maxHealth <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxHealth), maxHealth, "MaxHealth > 0 kötelező.");
            }

            if (startingAmmo < 0 || startingAmmo > maxAmmo)
            {
                throw new ArgumentOutOfRangeException(nameof(startingAmmo), startingAmmo, "StartingAmmo-nak 0..MaxAmmo között kell lennie.");
            }

            if (startingHealth < 0 || startingHealth > maxHealth)
            {
                throw new ArgumentOutOfRangeException(nameof(startingHealth), startingHealth, "StartingHealth-nek 0..MaxHealth között kell lennie.");
            }

            if (maxShield < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxShield), maxShield, "MaxShield nem lehet negatív (L4).");
            }

            if (startingShield < 0 || startingShield > maxShield)
            {
                throw new ArgumentOutOfRangeException(nameof(startingShield), startingShield, "StartingShield-nek 0..MaxShield között kell lennie (L5).");
            }

            if (requiredMathAccuracy < 0 || requiredMathAccuracy > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(requiredMathAccuracy), requiredMathAccuracy, "RequiredMathAccuracy 0..1 kötelező (0 = nincs minimum).");
            }

            Id = id;
            WorldId = worldId;
            DisplayNameKey = displayNameKey;
            LaneCount = laneCount;
            TargetDurationSeconds = targetDurationSeconds;
            Waves = waves ?? Array.Empty<WaveDefinition>();
            MathEvents = mathEvents ?? Array.Empty<MathEventScheduleEntry>();
            BossEnemyId = bossEnemyId;
            StartingAmmo = startingAmmo;
            MaxAmmo = maxAmmo;
            StartingHealth = startingHealth;
            MaxHealth = maxHealth;
            StartingShield = startingShield;
            MaxShield = maxShield;
            RequiredMathAccuracy = requiredMathAccuracy;
            WeaponId = weaponId;
        }
    }
}
