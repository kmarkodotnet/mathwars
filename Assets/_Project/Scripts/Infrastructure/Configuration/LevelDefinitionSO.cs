// LevelDefinitionSO.cs
// SO schema 3., 3.1–3.3. F3 adds the math schedule and accuracy threshold.
// World/BossEnemy/BossTriggerGameTime remain F5 fields.
using System.Collections.Generic;
using MathHorde.Core.Problems;
using MathHorde.Core.Resources;
using UnityEngine;

namespace MathHorde.Infrastructure.Configuration
{
    [CreateAssetMenu(fileName = "LevelDefinition", menuName = "MathHorde/Level Definition")]
    public sealed class LevelDefinitionSO : ScriptableObject, IValidatableConfig
    {
        [System.Serializable]
        public sealed class MathEventScheduleEntry
        {
            [SerializeField, Range(0f, 600f)] private float _triggerGameTime = 10f;
            [SerializeField] private ProblemType _problemType = ProblemType.AnswerLaneSelection;
            [SerializeField, Range(0.5f, 10f)] private float _previewDurationSeconds = 2f;
            [SerializeField, Range(0f, 60f)] private float _thinkTimeSecondsOverride;
            [SerializeField] private ResourceType _rewardResource = ResourceType.Ammo;
            [SerializeField] private GateGoal _goal = GateGoal.Maximize;
            [SerializeField, Range(0, 500)] private int _rewardAmountOverride;
            [SerializeField, Range(0, 999)] private int _targetValueOverride;

            public float TriggerGameTime => _triggerGameTime;
            public ProblemType ProblemType => _problemType;
            public float PreviewDurationSeconds => _previewDurationSeconds;
            public float ThinkTimeSecondsOverride => _thinkTimeSecondsOverride;
            public ResourceType RewardResource => _rewardResource;
            public GateGoal Goal => _goal;
            public int RewardAmountOverride => _rewardAmountOverride;
            public int TargetValueOverride => _targetValueOverride;

            internal void CollectErrors(List<string> errors, string path)
            {
                ConfigValidation.RequireRange(errors, $"{path}.TriggerGameTime", _triggerGameTime, 0f, 600f);
                ConfigValidation.RequireRange(errors, $"{path}.PreviewDurationSeconds", _previewDurationSeconds, 0.5f, 10f);
                ConfigValidation.RequireRange(errors, $"{path}.ThinkTimeSecondsOverride", _thinkTimeSecondsOverride, 0f, 60f);
                ConfigValidation.RequireRange(errors, $"{path}.RewardAmountOverride", _rewardAmountOverride, 0, 500);
                ConfigValidation.RequireRange(errors, $"{path}.TargetValueOverride", _targetValueOverride, 0, 999);
                if (!System.Enum.IsDefined(typeof(ProblemType), _problemType)) errors.Add($"{path}.ProblemType érvénytelen.");
                if (!System.Enum.IsDefined(typeof(ResourceType), _rewardResource)) errors.Add($"{path}.RewardResource érvénytelen.");
                if (!System.Enum.IsDefined(typeof(GateGoal), _goal)) errors.Add($"{path}.Goal érvénytelen.");
                if (_goal == GateGoal.ReachTarget && _problemType != ProblemType.TargetValueSelection)
                    errors.Add($"{path}.Goal ReachTarget csak TargetValueSelection esetén engedett.");
                if (_goal == GateGoal.Minimize && _rewardResource != ResourceType.EnemyArmor)
                    errors.Add($"{path}.Goal Minimize csak EnemyArmor esetén engedett.");
                if (_problemType == ProblemType.ResourceOperationComparison && _rewardAmountOverride != 0)
                    errors.Add($"{path}.RewardAmountOverride resource gate esetén 0: a jutalom snapshot-delta.");
                if (_problemType != ProblemType.TargetValueSelection && _targetValueOverride != 0)
                    errors.Add($"{path}.TargetValueOverride csak TargetValueSelection esetén alkalmazható.");
            }

            internal MathHorde.Core.Levels.MathEventScheduleEntry ToDomain() =>
                new MathHorde.Core.Levels.MathEventScheduleEntry(_triggerGameTime, _problemType,
                    _previewDurationSeconds, _thinkTimeSecondsOverride, _rewardResource, _goal);
        }

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

        [SerializeField] private List<MathEventScheduleEntry> _mathEvents = new List<MathEventScheduleEntry>();
        [SerializeField, Range(0f, 1f)] private float _requiredMathAccuracy;

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
        public IReadOnlyList<MathEventScheduleEntry> MathEvents => _mathEvents;
        public float RequiredMathAccuracy => _requiredMathAccuracy;
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
            ConfigValidation.RequireRange(errors, nameof(RequiredMathAccuracy), _requiredMathAccuracy, 0f, 1f);
            if (!System.Enum.IsDefined(typeof(LevelTemplate), _templateType)) errors.Add("TemplateType érvénytelen.");
            CollectMathEventErrors(errors, 0f, 2);

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

        // Sorting only a copy preserves the original schedule indexes used by IDs/reward/target maps.
        internal void CollectMathEventErrors(List<string> errors, float minGap, int maxSameType)
        {
            if (_mathEvents == null) { errors.Add("MathEvents nem lehet null."); return; }
            ConfigValidation.RequireCount(errors, nameof(MathEvents), _mathEvents.Count,
                _templateType == LevelTemplate.Tutorial ? 8 : 10, 40);
            var order = new List<int>(_mathEvents.Count);
            for (int i = 0; i < _mathEvents.Count; i++)
            {
                var entry = _mathEvents[i];
                if (entry == null) { errors.Add($"MathEvents[{i}] nem lehet null."); continue; }
                entry.CollectErrors(errors, $"MathEvents[{i}]");
                if (entry.TriggerGameTime >= _targetDurationSeconds)
                    errors.Add($"MathEvents[{i}].TriggerGameTime a pálya vége előtt kötelező.");
                order.Add(i);
            }
            order.Sort((a, b) =>
            {
                int time = _mathEvents[a].TriggerGameTime.CompareTo(_mathEvents[b].TriggerGameTime);
                return time != 0 ? time : a.CompareTo(b);
            });
            int consecutive = 1;
            for (int i = 1; i < order.Count; i++)
            {
                var previous = _mathEvents[order[i - 1]];
                var current = _mathEvents[order[i]];
                if (current.TriggerGameTime - previous.TriggerGameTime < minGap || current.TriggerGameTime == previous.TriggerGameTime)
                    errors.Add($"MathEvents[{order[i]}].TriggerGameTime: legalább {minGap} s szünet kötelező.");
                consecutive = current.ProblemType == previous.ProblemType ? consecutive + 1 : 1;
                if (consecutive > maxSameType) errors.Add($"MathEvents[{order[i]}].ProblemType: legfeljebb {maxSameType} egymást követő azonos típus engedett.");
            }
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
