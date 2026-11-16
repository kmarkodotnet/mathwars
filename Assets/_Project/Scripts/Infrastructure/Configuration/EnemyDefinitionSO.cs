// EnemyDefinitionSO.cs
// F1-09 — contract scriptable-object-schema.md 1. EnemyDefinitionSO (F1 scope).
//
// Scope note: the contract's field table also lists ArmorPoints,
// FrontalDamageReduction, SplitOnDeathEnemy and SplitOnDeathCount, each tagged
// **F5** in the Fázis column, and ADR-008's "Következmények" section says this
// explicitly: "Az Armored páncél-mezői (ArmorPoints, FrontalDamageReduction)
// F5-ösek: F1–F4-ben az Armored a megnövelt HP-val és a lassú sebességgel
// differenciált." Those fields are intentionally NOT added here — F5-02 (boss
// system) and F5-11 (armor/splitter variants) own introducing them. Section 1.1's
// BossPhaseEntry list is explicitly "az F5-ben bevezetendő" (to be introduced in
// F5) and is likewise out of scope.
//
// GameConfigurationRepository (the SO -> Core.Levels.EnemyDefinition translator,
// game-logic.md) is a separate, not-yet-built component — this task only covers
// the editor-facing ScriptableObject + its own validation.
using System.Collections.Generic;
using MathHorde.Core.Levels;
using UnityEngine;

namespace MathHorde.Infrastructure.Configuration
{
    [CreateAssetMenu(fileName = "EnemyDefinition", menuName = "MathHorde/Enemy Definition")]
    public sealed class EnemyDefinitionSO : ScriptableObject, IValidatableConfig
    {
        [SerializeField] private string _id = "basic_bot";
        [SerializeField] private string _displayNameKey = "enemy.basic";
        [SerializeField] private EnemyType _type = EnemyType.Basic;

        [Range(1, 10000)]
        [SerializeField] private int _maxHealth = 10;

        [Range(0.1f, 20.0f)]
        [SerializeField] private float _movementSpeed = 2.0f;

        [Range(0, 1000)]
        [SerializeField] private int _contactDamage = 10;

        [Range(0.0f, 10.0f)]
        [SerializeField] private float _attackCooldownSeconds = 1.0f;

        [Tooltip("Lőszer-visszatérítés kill-enként (ApplyCombatReward(Ammo, ..., \"KILL_REFUND\")), nem pontszám.")]
        [Range(0, 1000)]
        [SerializeField] private int _rewardValue = 1;

        [SerializeField] private GameObject _prefab;

        [SerializeField] private Color _tintColor = Color.white;

        [Range(0.2f, 5.0f)]
        [SerializeField] private float _scaleMultiplier = 1.0f;

        [Tooltip("Kötelék-spawn mérete (Swarmlet: 4, GAME_DESIGN 5.).")]
        [Range(1, 8)]
        [SerializeField] private int _spawnGroupSize = 1;

        public string Id => _id;
        public string DisplayNameKey => _displayNameKey;
        public EnemyType Type => _type;
        public int MaxHealth => _maxHealth;
        public float MovementSpeed => _movementSpeed;
        public int ContactDamage => _contactDamage;
        public float AttackCooldownSeconds => _attackCooldownSeconds;
        public int RewardValue => _rewardValue;
        public GameObject Prefab => _prefab;
        public Color TintColor => _tintColor;
        public float ScaleMultiplier => _scaleMultiplier;
        public int SpawnGroupSize => _spawnGroupSize;

        public IReadOnlyList<string> Validate()
        {
            var errors = new List<string>();

            ConfigValidation.RequireNonEmpty(errors, nameof(Id), _id);
            ConfigValidation.RequireNonEmpty(errors, nameof(DisplayNameKey), _displayNameKey);
            ConfigValidation.RequireNotNull(errors, nameof(Prefab), _prefab);
            ConfigValidation.RequireRange(errors, nameof(MaxHealth), _maxHealth, 1, 10000);
            ConfigValidation.RequireRange(errors, nameof(MovementSpeed), _movementSpeed, 0.1f, 20.0f);
            ConfigValidation.RequireRange(errors, nameof(ContactDamage), _contactDamage, 0, 1000);
            ConfigValidation.RequireRange(errors, nameof(AttackCooldownSeconds), _attackCooldownSeconds, 0.0f, 10.0f);
            ConfigValidation.RequireRange(errors, nameof(RewardValue), _rewardValue, 0, 1000);
            ConfigValidation.RequireRange(errors, nameof(ScaleMultiplier), _scaleMultiplier, 0.2f, 5.0f);
            ConfigValidation.RequireRange(errors, nameof(SpawnGroupSize), _spawnGroupSize, 1, 8);

            if (_type == EnemyType.Boss && _maxHealth < 100)
            {
                errors.Add("Type == Boss esetén MaxHealth >= 100 kötelező.");
            }

            if (_type == EnemyType.Swarmlet)
            {
                if (_maxHealth > 2)
                {
                    errors.Add("Type == Swarmlet esetén MaxHealth <= 2 kötelező.");
                }

                if (_spawnGroupSize < 2)
                {
                    errors.Add("Type == Swarmlet esetén SpawnGroupSize >= 2 kötelező.");
                }
            }

            return errors;
        }

        private void OnValidate()
        {
            foreach (string error in Validate())
            {
                Debug.LogError($"{name} ({nameof(EnemyDefinitionSO)}): {error}", this);
            }
        }
    }
}
