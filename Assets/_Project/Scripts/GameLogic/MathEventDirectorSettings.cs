using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using MathHorde.Core.Problems;
using MathHorde.MathEngine;

namespace MathHorde.GameLogic
{
    public sealed class MathEventDirectorSettings
    {
        public GateGenerationLimits GateLimits { get; }
        public int MaxConcurrentMathEvents { get; }
        public float GateSpawnDepth { get; }
        public float DecisionZoneDepth { get; }
        public float GateApproachSpeed { get; }
        public float FeedbackDisplaySeconds { get; }
        public IReadOnlyDictionary<int, RewardSpec> RewardsByScheduleIndex { get; }
        public IReadOnlyDictionary<int, int> TargetsByScheduleIndex { get; }

        public MathEventDirectorSettings(int maxConcurrentMathEvents, float gateSpawnDepth,
            float decisionZoneDepth, float gateApproachSpeed, float feedbackDisplaySeconds,
            GateGenerationLimits gateLimits,
            IReadOnlyDictionary<int, RewardSpec> rewardsByScheduleIndex,
            IReadOnlyDictionary<int, int> targetsByScheduleIndex = null)
        {
            if (maxConcurrentMathEvents < 1 || maxConcurrentMathEvents > 3) throw new ArgumentOutOfRangeException(nameof(maxConcurrentMathEvents));
            Range(gateSpawnDepth, 5f, 60f, nameof(gateSpawnDepth));
            Range(decisionZoneDepth, 0.5f, 10f, nameof(decisionZoneDepth));
            Range(gateApproachSpeed, 0.5f, 20f, nameof(gateApproachSpeed));
            Range(feedbackDisplaySeconds, 0.5f, 5f, nameof(feedbackDisplaySeconds));
            if (gateSpawnDepth <= decisionZoneDepth) throw new ArgumentException("Spawn must be beyond the decision zone.", nameof(gateSpawnDepth));
            if (rewardsByScheduleIndex == null) throw new ArgumentNullException(nameof(rewardsByScheduleIndex));
            GateLimits = gateLimits;
            MaxConcurrentMathEvents = maxConcurrentMathEvents; GateSpawnDepth = gateSpawnDepth;
            DecisionZoneDepth = decisionZoneDepth; GateApproachSpeed = gateApproachSpeed; FeedbackDisplaySeconds = feedbackDisplaySeconds;
            var rewards = new Dictionary<int, RewardSpec>();
            foreach (var pair in rewardsByScheduleIndex) rewards.Add(pair.Key, pair.Value);
            RewardsByScheduleIndex = new ReadOnlyDictionary<int, RewardSpec>(rewards);
            var targets = new Dictionary<int, int>();
            if (targetsByScheduleIndex != null) foreach (var pair in targetsByScheduleIndex) targets.Add(pair.Key, pair.Value);
            TargetsByScheduleIndex = new ReadOnlyDictionary<int, int>(targets);
        }

        private static void Range(float value, float min, float max, string name)
        {
            if (float.IsNaN(value) || value < min || value > max) throw new ArgumentOutOfRangeException(name);
        }
    }
}
