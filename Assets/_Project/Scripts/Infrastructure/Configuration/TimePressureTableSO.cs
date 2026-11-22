using System;
using System.Collections.Generic;
using UnityEngine;

namespace MathHorde.Infrastructure.Configuration
{
    [CreateAssetMenu(fileName = "TimePressureTable", menuName = "MathHorde/Time Pressure Table")]
    public sealed class TimePressureTableSO : ScriptableObject, IValidatableConfig
    {
        [Serializable]
        public sealed class TimePressureLevelEntry
        {
            [SerializeField] private int _level;
            [SerializeField] private float _thinkTimeSeconds, _gateApproachSeconds, _answerSpawnDelaySeconds;
            [SerializeField] private float _gateApproachSpeed, _minGapBetweenProblemsSeconds;
            public int Level => _level;
            public float ThinkTimeSeconds => _thinkTimeSeconds;
            public float GateApproachSeconds => _gateApproachSeconds;
            public float AnswerSpawnDelaySeconds => _answerSpawnDelaySeconds;
            public float GateApproachSpeed => _gateApproachSpeed;
            public float MinGapBetweenProblemsSeconds => _minGapBetweenProblemsSeconds;
            internal TimePressureLevelEntry(int level, float think, float approach, float delay, float speed, float gap)
            { _level = level; _thinkTimeSeconds = think; _gateApproachSeconds = approach;
                _answerSpawnDelaySeconds = delay; _gateApproachSpeed = speed; _minGapBetweenProblemsSeconds = gap; }
        }
        [SerializeField] private List<TimePressureLevelEntry> _levels = Defaults();
        public IReadOnlyList<TimePressureLevelEntry> Levels => _levels;
        public TimePressureLevelEntry GetLevel(int level)
        {
            if (_levels != null) foreach (var row in _levels) if (row != null && row.Level == level) return row;
            throw new ConfigurationValidationException(new[] { "TimePressureTable: hiányzó szint." });
        }
        public IReadOnlyList<string> Validate() => Validate(null);
        public IReadOnlyList<string> Validate(MathDifficultyTableSO math)
        {
            var errors = new List<string>();
            if (_levels == null) { errors.Add("TimePressureTable.Levels nem lehet null."); return errors; }
            ConfigValidation.RequireCount(errors, "TimePressureTable.Levels", _levels.Count, 10, 10);
            for (int i = 0; i < _levels.Count; i++)
            {
                var row = _levels[i];
                if (row == null) { errors.Add("TimePressureTable: null sor."); continue; }
                if (row.Level != i + 1) errors.Add("TimePressureTable: a szintek 1..10 sorrendben kötelezőek.");
                ConfigValidation.RequireRange(errors, "ThinkTimeSeconds", row.ThinkTimeSeconds, 4f, 30f);
                ConfigValidation.RequireRange(errors, "GateApproachSeconds", row.GateApproachSeconds, 3f, 30f);
                ConfigValidation.RequireRange(errors, "AnswerSpawnDelaySeconds", row.AnswerSpawnDelaySeconds, 0.5f, 5f);
                ConfigValidation.RequireRange(errors, "GateApproachSpeed", row.GateApproachSpeed, 0.5f, 20f);
                ConfigValidation.RequireRange(errors, "MinGapBetweenProblemsSeconds", row.MinGapBetweenProblemsSeconds, 8f, 60f);
                if (i > 0 && _levels[i - 1] != null && row.ThinkTimeSeconds >= _levels[i - 1].ThinkTimeSeconds)
                    errors.Add("TimePressureTable: a gondolkodási idő szigorúan csökkenő.");
                if (math != null && row.Level >= 1 && row.Level <= 10
                    && row.ThinkTimeSeconds != math.GetLevel(row.Level).BaseThinkTimeSeconds)
                    errors.Add("TimePressureTable: MathDifficultyTable.BaseThinkTimeSeconds eltérés.");
            }
            return errors;
        }
        private static List<TimePressureLevelEntry> Defaults()
        {
            float[] think = { 14, 13, 11.5f, 10, 8, 7.5f, 7, 6.5f, 5.5f, 4.5f };
            float[] approach = { 12, 11, 10, 9, 8, 7.5f, 7, 6.5f, 6, 5 };
            float[] delay = { 2.5f, 2.5f, 2, 2, 1.5f, 1.5f, 1.5f, 1.2f, 1, 1 };
            float[] speed = { 1.2f, 1.3f, 1.5f, 1.7f, 1.9f, 2.1f, 2.3f, 2.6f, 2.9f, 3.3f };
            float[] gap = { 14, 13, 12, 11, 10, 10, 9, 9, 8, 8 };
            var rows = new List<TimePressureLevelEntry>(10);
            for (int i = 0; i < 10; i++) rows.Add(new TimePressureLevelEntry(i + 1, think[i], approach[i], delay[i], speed[i], gap[i]));
            return rows;
        }
    }
}
