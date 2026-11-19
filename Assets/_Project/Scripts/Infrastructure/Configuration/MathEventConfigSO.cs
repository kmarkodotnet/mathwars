using System.Collections.Generic;
using UnityEngine;

namespace MathHorde.Infrastructure.Configuration
{
    /// <summary>Global limits and geometry; CL timing/rewards come from their own tables (SO schema 6.).</summary>
    [CreateAssetMenu(fileName = "MathEventConfig", menuName = "MathHorde/Math Event Config")]
    public sealed class MathEventConfigSO : ScriptableObject, IValidatableConfig
    {
        [SerializeField, Range(5f, 120f)] private float _mathEventIntervalSeconds = 20f;
        [SerializeField, Range(0f, 0.5f)] private float _mathEventIntervalJitter = 0.15f;
        [SerializeField, Range(2f, 60f)] private float _mathEventCooldownSecondsFloor = 6f;
        [SerializeField, Range(1, 3)] private int _maxConcurrentMathEvents = 1;
        [SerializeField, Range(5f, 60f)] private float _gateSpawnDepth = 30f;
        [SerializeField, Range(0.5f, 10f)] private float _decisionZoneDepth = 3f;
        [SerializeField, Range(0.5f, 5f)] private float _feedbackDisplaySeconds = 1.5f;
        [SerializeField, Range(1f, 10f)] private float _goalIconLeadSeconds = 2f;
        [SerializeField, Range(1, 3)] private int _maxConsecutiveSameProblemType = 2;
        [SerializeField, Range(1, 3)] private int _maxConsecutiveSameGateSide = 2;
        [SerializeField, Range(0f, 1f)] private float _nonObviousGatePairRatioMin = 0.30f;
        [SerializeField, Range(0, 100)] private int _incorrectRewardAmount;
        [SerializeField, Range(1, 100)] private int _resourceGateOperandMin = 2;
        [SerializeField, Range(2, 200)] private int _resourceGateOperandMax = 50;
        [SerializeField, Range(2, 5)] private int _resourceGateMultiplierMin = 2;
        [SerializeField, Range(2, 10)] private int _resourceGateMultiplierMax = 4;

        public float MathEventIntervalSeconds => _mathEventIntervalSeconds;
        public float MathEventIntervalJitter => _mathEventIntervalJitter;
        public float MathEventCooldownSecondsFloor => _mathEventCooldownSecondsFloor;
        public int MaxConcurrentMathEvents => _maxConcurrentMathEvents;
        public float GateSpawnDepth => _gateSpawnDepth;
        public float DecisionZoneDepth => _decisionZoneDepth;
        public float FeedbackDisplaySeconds => _feedbackDisplaySeconds;
        public float GoalIconLeadSeconds => _goalIconLeadSeconds;
        public int MaxConsecutiveSameProblemType => _maxConsecutiveSameProblemType;
        public int MaxConsecutiveSameGateSide => _maxConsecutiveSameGateSide;
        public float NonObviousGatePairRatioMin => _nonObviousGatePairRatioMin;
        public int IncorrectRewardAmount => _incorrectRewardAmount;
        public int ResourceGateOperandMin => _resourceGateOperandMin;
        public int ResourceGateOperandMax => _resourceGateOperandMax;
        public int ResourceGateMultiplierMin => _resourceGateMultiplierMin;
        public int ResourceGateMultiplierMax => _resourceGateMultiplierMax;

        public IReadOnlyList<string> Validate()
        {
            var errors = new List<string>();
            ConfigValidation.RequireRange(errors, nameof(MathEventIntervalSeconds), _mathEventIntervalSeconds, 5f, 120f);
            ConfigValidation.RequireRange(errors, nameof(MathEventIntervalJitter), _mathEventIntervalJitter, 0f, 0.5f);
            ConfigValidation.RequireRange(errors, nameof(MathEventCooldownSecondsFloor), _mathEventCooldownSecondsFloor, 2f, 60f);
            ConfigValidation.RequireRange(errors, nameof(MaxConcurrentMathEvents), _maxConcurrentMathEvents, 1, 3);
            ConfigValidation.RequireRange(errors, nameof(GateSpawnDepth), _gateSpawnDepth, 5f, 60f);
            ConfigValidation.RequireRange(errors, nameof(DecisionZoneDepth), _decisionZoneDepth, 0.5f, 10f);
            ConfigValidation.RequireRange(errors, nameof(FeedbackDisplaySeconds), _feedbackDisplaySeconds, 0.5f, 5f);
            // GAME_DESIGN 3.4 is stricter than the schema's raw Inspector range.
            ConfigValidation.RequireRange(errors, nameof(GoalIconLeadSeconds), _goalIconLeadSeconds, 2f, 10f);
            ConfigValidation.RequireRange(errors, nameof(MaxConsecutiveSameProblemType), _maxConsecutiveSameProblemType, 1, 3);
            ConfigValidation.RequireRange(errors, nameof(MaxConsecutiveSameGateSide), _maxConsecutiveSameGateSide, 1, 3);
            ConfigValidation.RequireRange(errors, nameof(NonObviousGatePairRatioMin), _nonObviousGatePairRatioMin, 0f, 1f);
            ConfigValidation.RequireRange(errors, nameof(IncorrectRewardAmount), _incorrectRewardAmount, 0, 100);
            ConfigValidation.RequireRange(errors, nameof(ResourceGateOperandMin), _resourceGateOperandMin, 1, 100);
            ConfigValidation.RequireRange(errors, nameof(ResourceGateOperandMax), _resourceGateOperandMax, 2, 200);
            ConfigValidation.RequireRange(errors, nameof(ResourceGateMultiplierMin), _resourceGateMultiplierMin, 2, 5);
            ConfigValidation.RequireRange(errors, nameof(ResourceGateMultiplierMax), _resourceGateMultiplierMax, 2, 10);
            if (_gateSpawnDepth <= _decisionZoneDepth) errors.Add("GateSpawnDepth > DecisionZoneDepth kötelező.");
            if (_resourceGateOperandMin > _resourceGateOperandMax) errors.Add("ResourceGateOperandMin > ResourceGateOperandMax.");
            if (_resourceGateMultiplierMin > _resourceGateMultiplierMax) errors.Add("ResourceGateMultiplierMin > ResourceGateMultiplierMax.");
            return errors;
        }

        private void OnValidate()
        {
            foreach (string error in Validate()) Debug.LogError($"{name} ({nameof(MathEventConfigSO)}): {error}", this);
        }
    }
}
