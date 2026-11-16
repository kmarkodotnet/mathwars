// WeaponConfigSO.cs
// F1-09 — contract scriptable-object-schema.md 2. WeaponConfigSO (F1).
//
// Scope note: the contract's balance rule under this section checks the
// weapon's DPS (Damage x FireRate) against GameBalanceSO.ReferenceDps and warns
// on >5% deviation. GameBalanceSO is one of the seven new assets introduced by
// the architect's r2 revision and is NOT part of this task's header
// (EnemyDefinitionSO, WeaponConfigSO, CombatBalanceSO, LevelDefinitionSO,
// DeveloperSettingsSO + IValidatableConfig) — it has no backing asset yet, so
// that cross-asset DPS check is deliberately deferred to whichever task
// introduces GameBalanceSO, rather than guessed at here.
using System.Collections.Generic;
using MathHorde.Core.Levels;
using UnityEngine;

namespace MathHorde.Infrastructure.Configuration
{
    [CreateAssetMenu(fileName = "WeaponConfig", menuName = "MathHorde/Weapon Config")]
    public sealed class WeaponConfigSO : ScriptableObject, IValidatableConfig
    {
        [SerializeField] private string _id = "starter_blaster";
        [SerializeField] private string _displayNameKey = "weapon.starter";

        [Range(1, 1000)]
        [SerializeField] private int _damage = 5;

        [Tooltip("Lövés/mp.")]
        [Range(0.1f, 30.0f)]
        [SerializeField] private float _fireRate = 3.0f;

        [Range(1.0f, 100.0f)]
        [SerializeField] private float _range = 25.0f;

        [Range(1, 20)]
        [SerializeField] private int _ammoPerShot = 1;

        [Tooltip("0 = hitscan.")]
        [Range(0.0f, 200.0f)]
        [SerializeField] private float _projectileSpeed = 40.0f;

        [SerializeField] private TargetingMode _targetingMode = TargetingMode.OwnLaneThenAdjacent;

        [Tooltip("Sebzés-szorzó szomszédos sávra.")]
        [Range(0.0f, 1.0f)]
        [SerializeField] private float _adjacentLanePenalty = 0.0f;

        [SerializeField] private GameObject _projectilePrefab;

        [SerializeField] private string _muzzleEffectKey = "vfx.muzzle.default";
        [SerializeField] private string _hitEffectKey = "vfx.hit.default";

        public string Id => _id;
        public string DisplayNameKey => _displayNameKey;
        public int Damage => _damage;
        public float FireRate => _fireRate;
        public float Range => _range;
        public int AmmoPerShot => _ammoPerShot;
        public float ProjectileSpeed => _projectileSpeed;
        public TargetingMode TargetingMode => _targetingMode;
        public float AdjacentLanePenalty => _adjacentLanePenalty;
        public GameObject ProjectilePrefab => _projectilePrefab;
        public string MuzzleEffectKey => _muzzleEffectKey;
        public string HitEffectKey => _hitEffectKey;

        public IReadOnlyList<string> Validate()
        {
            var errors = new List<string>();

            ConfigValidation.RequireNonEmpty(errors, nameof(Id), _id);
            ConfigValidation.RequireNonEmpty(errors, nameof(DisplayNameKey), _displayNameKey);
            ConfigValidation.RequireRange(errors, nameof(Damage), _damage, 1, 1000);
            ConfigValidation.RequireRange(errors, nameof(FireRate), _fireRate, 0.1f, 30.0f);
            ConfigValidation.RequireRange(errors, nameof(Range), _range, 1.0f, 100.0f);
            ConfigValidation.RequireRange(errors, nameof(AmmoPerShot), _ammoPerShot, 1, 20);
            ConfigValidation.RequireRange(errors, nameof(ProjectileSpeed), _projectileSpeed, 0.0f, 200.0f);
            ConfigValidation.RequireRange(errors, nameof(AdjacentLanePenalty), _adjacentLanePenalty, 0.0f, 1.0f);

            if (_projectileSpeed > 0 && _projectilePrefab == null)
            {
                errors.Add("ProjectilePrefab nem lehet null, ha ProjectileSpeed > 0.");
            }

            return errors;
        }

        private void OnValidate()
        {
            foreach (string error in Validate())
            {
                Debug.LogError($"{name} ({nameof(WeaponConfigSO)}): {error}", this);
            }
        }
    }
}
