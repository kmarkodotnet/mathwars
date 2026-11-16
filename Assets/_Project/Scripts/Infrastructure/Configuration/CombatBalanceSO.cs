// CombatBalanceSO.cs
// F1-09 — contract scriptable-object-schema.md 7. CombatBalanceSO (F1).
//
// Scope note: the paragraph below the table says Validate() should warn when
// both this asset's PerChallengeLevel*Step fields AND CombatDifficultyTableSO's
// per-level multipliers are simultaneously non-neutral (double-scaling risk).
// CombatDifficultyTableSO (contract 7.1) is one of the seven new assets from the
// architect's r2 revision and is explicitly NOT part of this task's header
// (EnemyDefinitionSO, WeaponConfigSO, CombatBalanceSO, LevelDefinitionSO,
// DeveloperSettingsSO + IValidatableConfig) — no such asset/class exists yet, so
// that cross-asset check cannot be implemented here. It is deferred to whichever
// task introduces CombatDifficultyTableSO.
using System.Collections.Generic;
using UnityEngine;

namespace MathHorde.Infrastructure.Configuration
{
    [CreateAssetMenu(fileName = "CombatBalance", menuName = "MathHorde/Combat Balance")]
    public sealed class CombatBalanceSO : ScriptableObject, IValidatableConfig
    {
        [Range(0.5f, 2.0f)]
        [SerializeField] private float _enemySpeedMultiplier = 1.0f;

        [Range(0.5f, 2.0f)]
        [SerializeField] private float _enemyHealthMultiplier = 1.0f;

        [Range(0.5f, 2.0f)]
        [SerializeField] private float _enemyDamageMultiplier = 1.0f;

        [Range(0.5f, 3.0f)]
        [SerializeField] private float _waveSizeMultiplier = 1.0f;

        [Range(0.5f, 3.0f)]
        [SerializeField] private float _spawnRateMultiplier = 1.0f;

        [Range(0.5f, 2.0f)]
        [SerializeField] private float _ammoConsumptionMultiplier = 1.0f;

        [Range(0.0f, 10.0f)]
        [SerializeField] private float _maxWaveGapSeconds = 2.0f;

        [Range(1.0f, 30.0f)]
        [SerializeField] private float _playerLaneChangeSpeed = 10.0f;

        [Range(-20.0f, 0.0f)]
        [SerializeField] private float _enemyDespawnDepth = -2.0f;

        [Range(10, 300)]
        [SerializeField] private int _maxActiveEnemies = 60;

        [Range(10, 400)]
        [SerializeField] private int _enemyPoolSize = 80;

        [Range(20, 500)]
        [SerializeField] private int _projectilePoolSize = 120;

        [Tooltip("Alapérték 0 — a CL-skálázás kanonikus helye a (jövőbeli) CombatDifficultyTableSO.")]
        [Range(0.0f, 0.2f)]
        [SerializeField] private float _perChallengeLevelSpeedStep;

        [Range(0.0f, 0.3f)]
        [SerializeField] private float _perChallengeLevelHealthStep;

        [Range(0.0f, 0.3f)]
        [SerializeField] private float _perChallengeLevelWaveSizeStep;

        [SerializeField] private bool _shieldAbsorbsBeforeHealth = true;

        public float EnemySpeedMultiplier => _enemySpeedMultiplier;
        public float EnemyHealthMultiplier => _enemyHealthMultiplier;
        public float EnemyDamageMultiplier => _enemyDamageMultiplier;
        public float WaveSizeMultiplier => _waveSizeMultiplier;
        public float SpawnRateMultiplier => _spawnRateMultiplier;
        public float AmmoConsumptionMultiplier => _ammoConsumptionMultiplier;
        public float MaxWaveGapSeconds => _maxWaveGapSeconds;
        public float PlayerLaneChangeSpeed => _playerLaneChangeSpeed;
        public float EnemyDespawnDepth => _enemyDespawnDepth;
        public int MaxActiveEnemies => _maxActiveEnemies;
        public int EnemyPoolSize => _enemyPoolSize;
        public int ProjectilePoolSize => _projectilePoolSize;
        public float PerChallengeLevelSpeedStep => _perChallengeLevelSpeedStep;
        public float PerChallengeLevelHealthStep => _perChallengeLevelHealthStep;
        public float PerChallengeLevelWaveSizeStep => _perChallengeLevelWaveSizeStep;
        public bool ShieldAbsorbsBeforeHealth => _shieldAbsorbsBeforeHealth;

        public IReadOnlyList<string> Validate()
        {
            var errors = new List<string>();

            ConfigValidation.RequireRange(errors, nameof(EnemySpeedMultiplier), _enemySpeedMultiplier, 0.5f, 2.0f);
            ConfigValidation.RequireRange(errors, nameof(EnemyHealthMultiplier), _enemyHealthMultiplier, 0.5f, 2.0f);
            ConfigValidation.RequireRange(errors, nameof(EnemyDamageMultiplier), _enemyDamageMultiplier, 0.5f, 2.0f);
            ConfigValidation.RequireRange(errors, nameof(WaveSizeMultiplier), _waveSizeMultiplier, 0.5f, 3.0f);
            ConfigValidation.RequireRange(errors, nameof(SpawnRateMultiplier), _spawnRateMultiplier, 0.5f, 3.0f);
            ConfigValidation.RequireRange(errors, nameof(AmmoConsumptionMultiplier), _ammoConsumptionMultiplier, 0.5f, 2.0f);
            ConfigValidation.RequireRange(errors, nameof(MaxWaveGapSeconds), _maxWaveGapSeconds, 0.0f, 10.0f);
            ConfigValidation.RequireRange(errors, nameof(PlayerLaneChangeSpeed), _playerLaneChangeSpeed, 1.0f, 30.0f);
            ConfigValidation.RequireRange(errors, nameof(EnemyDespawnDepth), _enemyDespawnDepth, -20.0f, 0.0f);
            ConfigValidation.RequireRange(errors, nameof(MaxActiveEnemies), _maxActiveEnemies, 10, 300);
            ConfigValidation.RequireRange(errors, nameof(EnemyPoolSize), _enemyPoolSize, 10, 400);
            ConfigValidation.RequireRange(errors, nameof(ProjectilePoolSize), _projectilePoolSize, 20, 500);
            ConfigValidation.RequireRange(errors, nameof(PerChallengeLevelSpeedStep), _perChallengeLevelSpeedStep, 0.0f, 0.2f);
            ConfigValidation.RequireRange(errors, nameof(PerChallengeLevelHealthStep), _perChallengeLevelHealthStep, 0.0f, 0.3f);
            ConfigValidation.RequireRange(errors, nameof(PerChallengeLevelWaveSizeStep), _perChallengeLevelWaveSizeStep, 0.0f, 0.3f);

            return errors;
        }

        private void OnValidate()
        {
            foreach (string error in Validate())
            {
                Debug.LogError($"{name} ({nameof(CombatBalanceSO)}): {error}", this);
            }
        }
    }
}
