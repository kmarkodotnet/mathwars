using System;
using System.Collections.Generic;
using MathHorde.Core.Levels;

namespace MathHorde.GameLogic
{
    internal static class WeaponCatalog
    {
        // Startup only. Validate the exact ordered level catalog before mutating a session.
        internal static IReadOnlyList<WeaponDefinition> Copy(LevelDefinition level, IReadOnlyList<WeaponDefinition> weapons)
        {
            if (level == null) throw new ArgumentNullException(nameof(level));
            if (weapons == null || weapons.Count != level.AvailableWeaponIds.Count)
                throw new ArgumentException("Weapon catalog does not match the level.", nameof(weapons));
            var copy = new WeaponDefinition[weapons.Count];
            for (int i = 0; i < copy.Length; i++)
            {
                var weapon = weapons[i];
                if (weapon == null || weapon.WeaponId != level.AvailableWeaponIds[i])
                    throw new ArgumentException("Missing, duplicate, unordered or foreign weapon.", nameof(weapons));
                copy[i] = weapon;
            }
            return Array.AsReadOnly(copy);
        }
    }
}
