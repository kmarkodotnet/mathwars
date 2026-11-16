using System;

namespace MathHorde.Core.Levels
{
    /// <summary>
    /// Engine-free weapon descriptor (contract core-domain.md 7., spec 4.2).
    /// ProjectileSpeed == 0 means hitscan (game-logic.md 3.3).
    /// </summary>
    public sealed class WeaponDefinition
    {
        public string WeaponId { get; }
        public int Damage { get; }
        public float FireRate { get; }
        public float Range { get; }
        public int AmmoPerShot { get; }
        public float ProjectileSpeed { get; }
        public TargetingMode TargetingMode { get; }
        public string VisualEffectKey { get; }

        public WeaponDefinition(
            string weaponId,
            int damage,
            float fireRate,
            float range,
            int ammoPerShot,
            float projectileSpeed,
            TargetingMode targetingMode,
            string visualEffectKey)
        {
            if (string.IsNullOrEmpty(weaponId))
            {
                throw new ArgumentException("WeaponId nem lehet üres.", nameof(weaponId));
            }

            if (damage <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(damage), damage, "Damage > 0 kötelező.");
            }

            if (fireRate <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(fireRate), fireRate, "FireRate > 0 kötelező.");
            }

            if (range <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(range), range, "Range > 0 kötelező.");
            }

            if (ammoPerShot < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(ammoPerShot), ammoPerShot, "AmmoPerShot >= 1 kötelező.");
            }

            if (projectileSpeed < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(projectileSpeed), projectileSpeed, "ProjectileSpeed nem lehet negatív (0 = hitscan).");
            }

            WeaponId = weaponId;
            Damage = damage;
            FireRate = fireRate;
            Range = range;
            AmmoPerShot = ammoPerShot;
            ProjectileSpeed = projectileSpeed;
            TargetingMode = targetingMode;
            VisualEffectKey = visualEffectKey;
        }
    }
}
