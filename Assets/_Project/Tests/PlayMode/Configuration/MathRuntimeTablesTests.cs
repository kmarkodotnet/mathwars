using System;
using System.Linq;
using MathHorde.Core.Problems;
using MathHorde.Core.Resources;
using MathHorde.Infrastructure.Configuration;
using MathHorde.MathEngine;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MathHorde.Tests.PlayMode.Configuration
{
    public class MathRuntimeTablesTests
    {
        [Test]
        public void ShippedTablesValidateAndEveryProfileCanGenerateConcreteGates()
        {
            var math = AssetDatabase.LoadAssetAtPath<MathDifficultyTableSO>("Assets/_Project/Configuration/MathDifficultyTable.asset");
            var time = AssetDatabase.LoadAssetAtPath<TimePressureTableSO>("Assets/_Project/Configuration/TimePressureTable.asset");
            var rewards = AssetDatabase.LoadAssetAtPath<RewardTableSO>("Assets/_Project/Configuration/RewardTable.asset");
            var config = AssetDatabase.LoadAssetAtPath<MathEventConfigSO>("Assets/_Project/Configuration/MathEventConfigDefault.asset");
            var level = AssetDatabase.LoadAssetAtPath<LevelDefinitionSO>("Assets/_Project/Configuration/LevelW1L01.asset");
            Assert.That(math.Validate(), Is.Empty); Assert.That(time.Validate(math), Is.Empty); Assert.That(rewards.Validate(), Is.Empty);
            var repository = new GameConfigurationRepository(Array.Empty<EnemyDefinitionSO>(), Array.Empty<WeaponConfigSO>(), Array.Empty<LevelDefinitionSO>(), mathDifficultyTable: math);
            var settings = MathEventConfigurationAdapter.CreateDirectorSettings(config, level, math, time, rewards, 1);
            Assert.That(settings.RewardsByScheduleIndex.Values.All(r => r.Resource == ResourceType.Health), Is.True);
            Assert.That(settings.RewardsByScheduleIndex[0].FlatAmount, Is.EqualTo(30));
            Assert.That(settings.RewardsByScheduleIndex[1].FlatAmount, Is.EqualTo(15));
            Assert.That(settings.GateLimits.OperandMin, Is.EqualTo(config.ResourceGateOperandMin));
            Assert.That(settings.GateLimits.MultiplierMax, Is.EqualTo(config.ResourceGateMultiplierMax));
            for (int cl = 1; cl <= 10; cl++)
            {
                var request = new ProblemGenerationRequest { Seed = 191000 + cl, Type = ProblemType.ResourceOperationComparison,
                    Difficulty = repository.GetDifficultyForLevel(cl), LaneCount = 4, ThinkTimeSeconds = time.GetLevel(cl).ThinkTimeSeconds,
                    RewardResource = ResourceType.Health, Reward = new RewardSpec(ResourceType.Health, 30), GateLimits = settings.GateLimits };
                foreach (var result in new MathProblemGenerator().GenerateBatch(request, 20))
                    Assert.That(result.IsSuccess, Is.True, "CL" + cl + ": " + result.ErrorCode);
            }
        }

        [Test]
        public void TimeMirrorMismatchAndInvalidRewardMultiplierAreVisibleConfigurationErrors()
        {
            var math = ScriptableObject.CreateInstance<MathDifficultyTableSO>();
            var time = ScriptableObject.CreateInstance<TimePressureTableSO>();
            var rewards = ScriptableObject.CreateInstance<RewardTableSO>();
            try
            {
                TestSupport.ConfigTestReflection.SetField(time.Levels[0], "_thinkTimeSeconds", 15f);
                Assert.That(time.Validate(math).Any(s => s.Contains("eltérés")), Is.True);
                TestSupport.ConfigTestReflection.SetField(rewards, "_gateRewardHealthMultiplier", float.NaN);
                Assert.That(rewards.Validate(), Is.Not.Empty);
            }
            finally { UnityEngine.Object.DestroyImmediate(math); UnityEngine.Object.DestroyImmediate(time); UnityEngine.Object.DestroyImmediate(rewards); }
        }
    }
}
