using System;
using System.Collections.Generic;
using UnityEngine;

namespace MathHorde.Infrastructure.Configuration
{
    [CreateAssetMenu(fileName = "RewardTable", menuName = "MathHorde/Reward Table")]
    public sealed class RewardTableSO : ScriptableObject, IValidatableConfig
    {
        [Serializable]
        public sealed class RewardLevelEntry
        {
            [SerializeField] private int _level, _gateRewardBase, _laneRewardBase, _mathDecisionCount;
            [SerializeField] private int _healthRewardBase = 15, _shieldRewardBase = 10, _enemyArmorRewardBase = 10;
            public int Level => _level;
            public int GateRewardBase => _gateRewardBase;
            public int LaneRewardBase => _laneRewardBase;
            public int MathDecisionCount => _mathDecisionCount;
            public int HealthRewardBase => _healthRewardBase;
            public int ShieldRewardBase => _shieldRewardBase;
            public int EnemyArmorRewardBase => _enemyArmorRewardBase;
            internal RewardLevelEntry(int level, int gate, int lane, int count)
            { _level = level; _gateRewardBase = gate; _laneRewardBase = lane; _mathDecisionCount = count; }
        }
        [SerializeField] private List<RewardLevelEntry> _levels = Defaults();
        [SerializeField] private float _rewardScalePerChallengeLevel;
        [SerializeField] private float _rescueWaveRewardMultiplier = 2f;
        [SerializeField] private float _gateRewardHealthMultiplier = 2f;
        public IReadOnlyList<RewardLevelEntry> Levels => _levels;
        public float RewardScalePerChallengeLevel => _rewardScalePerChallengeLevel;
        public float RescueWaveRewardMultiplier => _rescueWaveRewardMultiplier;
        public float GateRewardHealthMultiplier => _gateRewardHealthMultiplier;
        public RewardLevelEntry GetLevel(int level)
        {
            if (_levels != null) foreach (var row in _levels) if (row != null && row.Level == level) return row;
            throw new ConfigurationValidationException(new[] { "RewardTable: hiányzó szint." });
        }
        public IReadOnlyList<string> Validate()
        {
            var errors = new List<string>();
            if (_levels == null) { errors.Add("RewardTable.Levels nem lehet null."); return errors; }
            ConfigValidation.RequireCount(errors, "RewardTable.Levels", _levels.Count, 10, 10);
            ConfigValidation.RequireRange(errors, "RewardScalePerChallengeLevel", _rewardScalePerChallengeLevel, 0f, 0.3f);
            ConfigValidation.RequireRange(errors, "RescueWaveRewardMultiplier", _rescueWaveRewardMultiplier, 1f, 3f);
            ConfigValidation.RequireRange(errors, "GateRewardHealthMultiplier", _gateRewardHealthMultiplier, 1f, 3f);
            for (int i = 0; i < _levels.Count; i++)
            {
                var row = _levels[i];
                if (row == null) { errors.Add("RewardTable: null sor."); continue; }
                if (row.Level != i + 1) errors.Add("RewardTable: a szintek 1..10 sorrendben kötelezőek.");
                ConfigValidation.RequireRange(errors, "GateRewardBase", row.GateRewardBase, 1, 500);
                ConfigValidation.RequireRange(errors, "LaneRewardBase", row.LaneRewardBase, 1, 500);
                ConfigValidation.RequireRange(errors, "MathDecisionCount", row.MathDecisionCount, i == 0 ? 8 : 10, 24);
                ConfigValidation.RequireRange(errors, "HealthRewardBase", row.HealthRewardBase, 1, 200);
                ConfigValidation.RequireRange(errors, "ShieldRewardBase", row.ShieldRewardBase, 0, 200);
                ConfigValidation.RequireRange(errors, "EnemyArmorRewardBase", row.EnemyArmorRewardBase, 0, 200);
            }
            return errors;
        }
        private static List<RewardLevelEntry> Defaults()
        {
            int[] gate = { 30, 34, 40, 50, 60, 70, 82, 96, 112, 130 };
            int[] lane = { 20, 22, 26, 32, 38, 44, 52, 60, 70, 82 };
            int[] count = { 10, 11, 12, 13, 14, 14, 15, 15, 16, 16 };
            var rows = new List<RewardLevelEntry>(10);
            for (int i = 0; i < 10; i++) rows.Add(new RewardLevelEntry(i + 1, gate[i], lane[i], count[i]));
            return rows;
        }
    }
}
