// LevelDefinitionSO.cs
// F1-09 — contract scriptable-object-schema.md 3. LevelDefinitionSO (F1 scope),
// plus the nested 3.1 WaveDefinitionEntry and 3.2 WaveSpawnEntry types.
//
// Scope note: the contract's field table also lists World (WorldDefinitionSO,
// F5), MathEvents (List<MathEventScheduleEntry>, F3), BossEnemy/
// BossTriggerGameTime (F5) and RequiredMathAccuracy (F3) — all four are tagged
// with a LATER phase in the Fázis column. World in particular cannot be added
// without creating WorldDefinitionSO itself (contract section 4, F5), which is
// not part of this task's header (EnemyDefinitionSO, WeaponConfigSO,
// CombatBalanceSO, LevelDefinitionSO, DeveloperSettingsSO + IValidatableConfig).
// Those four fields are left for the F3/F5 tasks that own them, matching how
// EnemyDefinitionSO (same contract revision) also omits its own F5-tagged
// fields (ArmorPoints, FrontalDamageReduction, SplitOnDeathEnemy/Count).
using System.Collections.Generic;
using UnityEngine;

namespace MathHorde.Infrastructure.Configuration
{
    [CreateAssetMenu(fileName = "LevelDefinition", menuName = "MathHorde/Level Definition")]
    public sealed class LevelDefinitionSO : ScriptableObject, IValidatableConfig
    {
        [System.Serializable]
        public sealed class WaveSpawnEntry
        {
            [SerializeField] private EnemyDefinitionSO _enemy;

            [Range(1, 200)]
            [SerializeField] private int _count = 5;

            [Range(0.05f, 10.0f)]
            [SerializeField] private float _spawnIntervalSeconds = 0.8f;

            [Tooltip("Elemek 0..3; üres = bármelyik sáv.")]
            [SerializeField] private List<int> _allowedLanes = new List<int>();

            public EnemyDefinitionSO Enemy => _enemy;
            public int Count => _count;
            public float SpawnIntervalSeconds => _spawnIntervalSeconds;
            public IReadOnlyList<int> AllowedLanes => _allowedLanes;

            public void CollectErrors(List<string> errors, string pathPrefix)
            {
                ConfigValidation.RequireNotNull(errors, $"{pathPrefix}.Enemy", _enemy);
                ConfigValidation.RequireRange(errors, $"{pathPrefix}.Count", _count, 1, 200);
                ConfigValidation.RequireRange(errors, $"{pathPrefix}.SpawnIntervalSeconds", _spawnIntervalSeconds, 0.05f, 10.0f);

                for (int i = 0; i < _allowedLanes.Count; i++)
                {
                    ConfigValidation.RequireRange(errors, $"{pathPrefix}.AllowedLanes[{i}]", _allowedLanes[i], 0, 3);
                }
            }
        }

        [System.Serializable]
        public sealed class WaveDefinitionEntry
        {
            [Range(0, 49)]
            [SerializeField] private int _waveIndex;

            [Range(0.0f, 600.0f)]
            [SerializeField] private float _startGameTime;

            [Range(1.0f, 300.0f)]
            [SerializeField] private float _durationSeconds = 20.0f;

            [SerializeField] private List<WaveSpawnEntry> _spawns = new List<WaveSpawnEntry>();

            public int WaveIndex => _waveIndex;
            public float StartGameTime => _startGameTime;
            public float DurationSeconds => _durationSeconds;
            public IReadOnlyList<WaveSpawnEntry> Spawns => _spawns;

            public void CollectErrors(List<string> errors, string pathPrefix)
            {
                ConfigValidation.RequireRange(errors, $"{pathPrefix}.WaveIndex", _waveIndex, 0, 49);
                ConfigValidation.RequireRange(errors, $"{pathPrefix}.StartGameTime", _startGameTime, 0.0f, 600.0f);
                ConfigValidation.RequireRange(errors, $"{pathPrefix}.DurationSeconds", _durationSeconds, 1.0f, 300.0f);
                ConfigValidation.RequireCount(errors, $"{pathPrefix}.Spawns", _spawns.Count, 1, 20);

                for (int i = 0; i < _spawns.Count; i++)
                {
                    _spawns[i]?.CollectErrors(errors, $"{pathPrefix}.Spawns[{i}]");
                }
            }
        }

        [SerializeField] private string _id = "w1-l01";
        [SerializeField] private string _displayNameKey;
        [SerializeField] private LevelTemplate _templateType = LevelTemplate.Standard;

        [Range(2, 4)]
        [SerializeField] private int _laneCount = 3;

        [Range(60.0f, 600.0f)]
        [SerializeField] private float _targetDurationSeconds = 240.0f;

        [SerializeField] private List<WaveDefinitionEntry> _waves = new List<WaveDefinitionEntry>();

        [Range(0, 999)]
        [SerializeField] private int _startingAmmo = 60;

        [Range(1, 999)]
        [SerializeField] private int _maxAmmo = 200;

        [Range(1, 999)]
        [SerializeField] private int _startingHealth = 100;

        [Range(1, 999)]
        [SerializeField] private int _maxHealth = 100;

        [Range(0, 999)]
        [SerializeField] private int _startingShield;

        [Tooltip("0 megengedett: a pályán nincs pajzs-mechanika, a ResourceService nem hoz létre Shield poolt.")]
        [Range(0, 999)]
        [SerializeField] private int _maxShield = 50;

        [SerializeField] private WeaponConfigSO _weapon;
        [SerializeField] private GameObject _environmentPrefab;

        public string Id => _id;
        public string DisplayNameKey => _displayNameKey;
        public LevelTemplate TemplateType => _templateType;
        public int LaneCount => _laneCount;
        public float TargetDurationSeconds => _targetDurationSeconds;
        public IReadOnlyList<WaveDefinitionEntry> Waves => _waves;
        public int StartingAmmo => _startingAmmo;
        public int MaxAmmo => _maxAmmo;
        public int StartingHealth => _startingHealth;
        public int MaxHealth => _maxHealth;
        public int StartingShield => _startingShield;
        public int MaxShield => _maxShield;
        public WeaponConfigSO Weapon => _weapon;
        public GameObject EnvironmentPrefab => _environmentPrefab;

        public IReadOnlyList<string> Validate()
        {
            var errors = new List<string>();

            ConfigValidation.RequireNonEmpty(errors, nameof(Id), _id);
            ConfigValidation.RequireNonEmpty(errors, nameof(DisplayNameKey), _displayNameKey);
            ConfigValidation.RequireNotNull(errors, nameof(Weapon), _weapon);
            ConfigValidation.RequireNotNull(errors, nameof(EnvironmentPrefab), _environmentPrefab);

            ConfigValidation.RequireRange(errors, nameof(LaneCount), _laneCount, 2, 4);
            ConfigValidation.RequireRange(errors, nameof(TargetDurationSeconds), _targetDurationSeconds, 60.0f, 600.0f);
            ConfigValidation.RequireRange(errors, nameof(StartingAmmo), _startingAmmo, 0, 999);
            ConfigValidation.RequireRange(errors, nameof(MaxAmmo), _maxAmmo, 1, 999);
            ConfigValidation.RequireRange(errors, nameof(StartingHealth), _startingHealth, 1, 999);
            ConfigValidation.RequireRange(errors, nameof(MaxHealth), _maxHealth, 1, 999);
            ConfigValidation.RequireRange(errors, nameof(StartingShield), _startingShield, 0, 999);
            ConfigValidation.RequireRange(errors, nameof(MaxShield), _maxShield, 0, 999);
            ConfigValidation.RequireCount(errors, nameof(Waves), _waves.Count, 1, 50);

            if (_startingAmmo > _maxAmmo)
            {
                errors.Add("StartingAmmo nem lehet nagyobb MaxAmmo-nál.");
            }

            if (_startingHealth > _maxHealth)
            {
                errors.Add("StartingHealth nem lehet nagyobb MaxHealth-nél.");
            }

            if (_startingShield > _maxShield)
            {
                errors.Add("StartingShield nem lehet nagyobb MaxShield-nél.");
            }

            for (int i = 0; i < _waves.Count; i++)
            {
                _waves[i]?.CollectErrors(errors, $"{nameof(Waves)}[{i}]");
            }

            return errors;
        }

        private void OnValidate()
        {
            foreach (string error in Validate())
            {
                Debug.LogError($"{name} ({nameof(LevelDefinitionSO)}): {error}", this);
            }
        }
    }
}
