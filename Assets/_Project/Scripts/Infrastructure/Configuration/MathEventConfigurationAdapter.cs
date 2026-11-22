using System;
using System.Collections.Generic;
using MathHorde.Core.Problems;
using MathHorde.Core.Resources;
using MathHorde.GameLogic;
using MathHorde.GameLogic.Events;
using MathHorde.MathEngine;

namespace MathHorde.Infrastructure.Configuration
{
    /// <summary>
    /// Composition-time translation only. Speed/gap are the selected TimePressureTableSO row;
    /// base rewards are resolved from RewardTableSO, targets by the host's seeded generator.
    /// No fallback table, RNG, Unity object or mutable source list enters the director settings.
    /// </summary>
    public static class MathEventConfigurationAdapter
    {
        public static MathEventDirector CreateDirector(IResourceService resources, IGameEventSink events,
            MathEventDirectorSettings settings, Core.Levels.LevelDefinition level, Core.Skills.DifficultyProfile difficulty, int seed)
        {
            var director = new MathEventDirector(resources, events, settings);
            director.Initialize(level, difficulty, new MathProblemGenerator(), new Core.Common.DeterministicRandomSource(seed));
            return director;
        }

        public static MathEventDirectorSettings CreateDirectorSettings(MathEventConfigSO config,
            LevelDefinitionSO level, MathDifficultyTableSO math, TimePressureTableSO time, RewardTableSO rewards, int challengeLevel)
        {
            if (math == null || time == null || rewards == null)
                throw new ConfigurationValidationException(new[] { "Hiányzó matematikai konfigurációs tábla." });
            var errors = new List<string>(math.Validate());
            errors.AddRange(time.Validate(math)); errors.AddRange(rewards.Validate());
            if (errors.Count > 0) throw new ConfigurationValidationException(errors);
            var timing = time.GetLevel(challengeLevel);
            var row = rewards.GetLevel(challengeLevel);
            var amounts = new Dictionary<int, int>();
            for (int i = 0; i < level.MathEvents.Count; i++)
                amounts.Add(i, level.MathEvents[i].ProblemType == ProblemType.ResourceOperationComparison
                    ? Math.Max(1, (int)Math.Round(row.HealthRewardBase * rewards.GateRewardHealthMultiplier, MidpointRounding.AwayFromZero))
                    : Math.Max(1, row.HealthRewardBase));
            return CreateDirectorSettings(config, level, timing.GateApproachSpeed, timing.MinGapBetweenProblemsSeconds, amounts);
        }

        public static MathEventDirectorSettings CreateDirectorSettings(MathEventConfigSO config,
            LevelDefinitionSO level, float gateApproachSpeed, float minGapBetweenProblemsSeconds,
            IReadOnlyDictionary<int, int> rewardAmountsByScheduleIndex,
            IReadOnlyDictionary<int, int> generatedTargetsByScheduleIndex = null)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (level == null) throw new ArgumentNullException(nameof(level));
            var errors = new List<string>(config.Validate());
            errors.AddRange(level.Validate());
            ConfigValidation.RequireRange(errors, nameof(gateApproachSpeed), gateApproachSpeed, 0.5f, 20f);
            ConfigValidation.RequireRange(errors, nameof(minGapBetweenProblemsSeconds), minGapBetweenProblemsSeconds, 8f, 60f);
            if (config.IncorrectRewardAmount != 0) errors.Add("IncorrectRewardAmount az MVP-ben 0 kötelező.");
            level.CollectMathEventErrors(errors,
                Math.Max(config.MathEventCooldownSecondsFloor, minGapBetweenProblemsSeconds),
                Math.Min(2, config.MaxConsecutiveSameProblemType));

            var rewards = new Dictionary<int, RewardSpec>();
            var targets = new Dictionary<int, int>();
            if (level.MathEvents != null)
            {
                for (int index = 0; index < level.MathEvents.Count; index++)
                {
                    var entry = level.MathEvents[index];
                    if (entry == null) continue;
                    if (entry.RewardResource == ResourceType.Ammo)
                        errors.Add($"MathEvents[{index}].RewardResource: AMMO_NOT_ALLOWED.");
                    else if (entry.RewardResource != ResourceType.Health || level.MaxHealth <= 0)
                        errors.Add($"MathEvents[{index}].RewardResource: REWARD_RESOURCE_NOT_ALLOWED.");
                    bool gate = entry.ProblemType == ProblemType.ResourceOperationComparison;
                    int amount = entry.RewardAmountOverride;
                    if (amount == 0 && (rewardAmountsByScheduleIndex == null || !rewardAmountsByScheduleIndex.TryGetValue(index, out amount)))
                        errors.Add($"MathEvents[{index}].RewardAmount: hiányzó RewardTable érték.");
                    ConfigValidation.RequireRange(errors, $"MathEvents[{index}].RewardAmount", amount, 1, int.MaxValue);
                    rewards.Add(index, new RewardSpec(entry.RewardResource, amount, config.IncorrectRewardAmount));

                    if (entry.ProblemType == ProblemType.TargetValueSelection || (gate && entry.Goal == GateGoal.ReachTarget))
                    {
                        int target = entry.TargetValueOverride;
                        if (target == 0 && (generatedTargetsByScheduleIndex == null || !generatedTargetsByScheduleIndex.TryGetValue(index, out target)))
                            errors.Add($"MathEvents[{index}].TargetValue: hiányzó generált cél.");
                        ConfigValidation.RequireRange(errors, $"MathEvents[{index}].TargetValue", target, 0, 999);
                        targets.Add(index, target);
                    }
                    else if (entry.TargetValueOverride != 0)
                        errors.Add($"MathEvents[{index}].TargetValueOverride csak TargetValueSelection esetén alkalmazható.");
                }
            }
            if (errors.Count > 0) throw new ConfigurationValidationException(errors);
            return new MathEventDirectorSettings(config.MaxConcurrentMathEvents, config.GateSpawnDepth,
                config.DecisionZoneDepth, gateApproachSpeed, config.FeedbackDisplaySeconds,
                new GateGenerationLimits(config.ResourceGateOperandMin, config.ResourceGateOperandMax,
                    config.ResourceGateMultiplierMin, config.ResourceGateMultiplierMax), rewards, targets);
        }
    }
}
