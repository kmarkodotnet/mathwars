using System.Collections.Generic;
using MathHorde.Core.Common;
using MathHorde.Core.Problems;
using MathHorde.Core.Progress;
using MathHorde.Core.Resources;
using MathHorde.GameLogic;
using MathHorde.Infrastructure.Common;
using MathHorde.Infrastructure.Configuration;
using MathHorde.MathEngine;
using MathHorde.Presentation.Events;
using MathHorde.Tests.PlayMode.TestSupport;
using NUnit.Framework;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MathHorde.Tests.PlayMode.Configuration
{
    public class MathEventConfigurationTests
    {
        private readonly List<Object> _created = new List<Object>();
        private T Create<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>(); _created.Add(asset); return asset;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var item in _created) Object.DestroyImmediate(item);
            _created.Clear();
        }

        private LevelDefinitionSO Level()
        {
            var level = Create<LevelDefinitionSO>();
            var enemy = Create<EnemyDefinitionSO>();
            var weapon = Create<WeaponConfigSO>();
            var prefab = new GameObject("config-test-prefab"); _created.Add(prefab);
            ConfigTestReflection.SetField(enemy, "_prefab", prefab);
            ConfigTestReflection.SetField(weapon, "_projectilePrefab", prefab);
            var spawn = new LevelDefinitionSO.WaveSpawnEntry();
            ConfigTestReflection.SetField(spawn, "_enemy", enemy);
            var wave = new LevelDefinitionSO.WaveDefinitionEntry();
            ConfigTestReflection.SetField(wave, "_spawns", new List<LevelDefinitionSO.WaveSpawnEntry> { spawn });
            ConfigTestReflection.SetField(level, "_weapon", weapon);
            ConfigTestReflection.SetField(level, "_environmentPrefab", prefab);
            ConfigTestReflection.SetField(level, "_displayNameKey", "level.w1l01.name");
            ConfigTestReflection.SetField(level, "_waves", new List<LevelDefinitionSO.WaveDefinitionEntry> { wave });
            ConfigTestReflection.SetField(level, "_mathEvents", ConfigTestReflection.MathSchedule());
            return level;
        }

        private static Dictionary<int, int> Rewards()
        {
            var map = new Dictionary<int, int>();
            for (int i = 0; i < 10; i++) map.Add(i, 20 + i);
            return map;
        }

        private static MathEventDirectorSettings Settings(MathEventConfigSO config, LevelDefinitionSO level,
            IReadOnlyDictionary<int, int> rewards = null, IReadOnlyDictionary<int, int> targets = null,
            float gap = 14f, float speed = 1.2f)
            => MathEventConfigurationAdapter.CreateDirectorSettings(config, level, speed, gap, rewards ?? Rewards(), targets);

        [Test]
        public void DefaultsAndFullyAssembledLevel_AreValid()
        {
            Assert.That(Create<MathEventConfigSO>().Validate(), Is.Empty);
            Assert.That(Level().Validate(), Is.Empty);
        }

        [TestCase("_mathEventIntervalSeconds", 121f)]
        [TestCase("_mathEventIntervalJitter", -0.1f)]
        [TestCase("_mathEventCooldownSecondsFloor", 1f)]
        [TestCase("_gateSpawnDepth", 61f)]
        [TestCase("_decisionZoneDepth", 0f)]
        [TestCase("_feedbackDisplaySeconds", 6f)]
        [TestCase("_goalIconLeadSeconds", 1.9f)]
        [TestCase("_nonObviousGatePairRatioMin", 1.1f)]
        [TestCase("_gateSpawnDepth", float.NaN)]
        [TestCase("_mathEventIntervalJitter", float.PositiveInfinity)]
        public void InvalidGlobalFloat_IsReported(string field, float value)
        {
            var config = Create<MathEventConfigSO>(); ConfigTestReflection.SetField(config, field, value);
            Assert.That(config.Validate().Count, Is.GreaterThan(0));
        }

        [TestCase("_maxConcurrentMathEvents", 4)]
        [TestCase("_maxConsecutiveSameProblemType", 0)]
        [TestCase("_maxConsecutiveSameGateSide", 4)]
        [TestCase("_incorrectRewardAmount", -1)]
        [TestCase("_resourceGateOperandMin", 0)]
        [TestCase("_resourceGateOperandMax", 201)]
        [TestCase("_resourceGateMultiplierMin", 1)]
        [TestCase("_resourceGateMultiplierMax", 11)]
        public void InvalidGlobalInteger_IsReported(string field, int value)
        {
            var config = Create<MathEventConfigSO>(); ConfigTestReflection.SetField(config, field, value);
            Assert.That(config.Validate().Count, Is.GreaterThan(0));
        }

        [Test]
        public void InvertedGlobalGeometryAndOperandBounds_AreRejected()
        {
            var config = Create<MathEventConfigSO>();
            ConfigTestReflection.SetField(config, "_gateSpawnDepth", 5f);
            ConfigTestReflection.SetField(config, "_decisionZoneDepth", 6f);
            ConfigTestReflection.SetField(config, "_resourceGateOperandMin", 60);
            ConfigTestReflection.SetField(config, "_resourceGateMultiplierMin", 5);
            Assert.That(config.Validate().Count, Is.EqualTo(3));
        }

        [TestCase("_triggerGameTime", -1f)]
        [TestCase("_previewDurationSeconds", 0.4f)]
        [TestCase("_thinkTimeSecondsOverride", 61f)]
        [TestCase("_thinkTimeSecondsOverride", float.NaN)]
        public void InvalidScheduleTime_ContainsOriginalIndex(string field, float value)
        {
            var level = Level(); ConfigTestReflection.SetField(level.MathEvents[3], field, value);
            Assert.That(ConfigTestReflection.AnyContains(level.Validate(), "MathEvents[3]"), Is.True);
        }

        [TestCase("_rewardAmountOverride", 501)]
        [TestCase("_targetValueOverride", 1000)]
        public void InvalidScheduleOverride_IsRejected(string field, int value)
        {
            var level = Level(); ConfigTestReflection.SetField(level.MathEvents[1], field, value);
            Assert.That(level.Validate().Count, Is.GreaterThan(0));
        }

        [Test]
        public void ScheduleEnumAndGoalRestrictions_AreEnforced()
        {
            var level = Level(); var entry = level.MathEvents[1];
            ConfigTestReflection.SetField(entry, "_problemType", (ProblemType)999);
            ConfigTestReflection.SetField(entry, "_rewardResource", (ResourceType)999);
            ConfigTestReflection.SetField(entry, "_goal", (GateGoal)999);
            Assert.That(level.Validate().Count, Is.EqualTo(3));
            ConfigTestReflection.SetField(entry, "_problemType", ProblemType.AnswerLaneSelection);
            ConfigTestReflection.SetField(entry, "_rewardResource", ResourceType.Ammo);
            ConfigTestReflection.SetField(entry, "_goal", GateGoal.ReachTarget);
            Assert.That(ConfigTestReflection.AnyContains(level.Validate(), "ReachTarget"), Is.True);
            ConfigTestReflection.SetField(entry, "_goal", GateGoal.Minimize);
            Assert.That(ConfigTestReflection.AnyContains(level.Validate(), "EnemyArmor"), Is.True);
            ConfigTestReflection.SetField(entry, "_rewardResource", ResourceType.EnemyArmor);
            Assert.That(level.Validate(), Is.Empty);
        }

        [Test]
        public void ScheduleCounts_AllowEightOnlyForTutorial_AndNeverAboveForty()
        {
            var level = Level(); ConfigTestReflection.SetField(level, "_mathEvents", ConfigTestReflection.MathSchedule(8));
            Assert.That(ConfigTestReflection.AnyContains(level.Validate(), "elemszáma"), Is.True);
            ConfigTestReflection.SetField(level, "_templateType", LevelTemplate.Tutorial);
            Assert.That(level.Validate(), Is.Empty);
            ConfigTestReflection.SetField(level, "_mathEvents", ConfigTestReflection.MathSchedule(41, 5f));
            Assert.That(ConfigTestReflection.AnyContains(level.Validate(), "elemszáma"), Is.True);
            ConfigTestReflection.SetField(level, "_mathEvents", null);
            Assert.That(ConfigTestReflection.AnyContains(level.Validate(), "MathEvents"), Is.True);
        }

        [Test]
        public void NullScheduleEntryAndNonFiniteAccuracy_AreReported()
        {
            var level = Level(); var entries = new List<LevelDefinitionSO.MathEventScheduleEntry>(level.MathEvents);
            entries[3] = null; ConfigTestReflection.SetField(level, "_mathEvents", entries);
            ConfigTestReflection.SetField(level, "_requiredMathAccuracy", float.NaN);
            Assert.That(ConfigTestReflection.AnyContains(level.Validate(), "MathEvents[3]"), Is.True);
            Assert.That(ConfigTestReflection.AnyContains(level.Validate(), "RequiredMathAccuracy"), Is.True);
        }

        [Test]
        public void TwoLaneLevel_AllowsBothMandatoryMechanics()
        {
            var level = Level(); ConfigTestReflection.SetField(level, "_laneCount", 2);
            Assert.That(level.Validate(), Is.Empty);
        }

        [Test]
        public void Adapter_UsesGeometryClSpeedAndOverrides_WithoutReindexingOrRetainingMutableMaps()
        {
            var config = Create<MathEventConfigSO>(); var level = Level();
            var entries = new List<LevelDefinitionSO.MathEventScheduleEntry>(level.MathEvents);
            entries.Reverse(); ConfigTestReflection.SetField(level, "_mathEvents", entries);
            ConfigTestReflection.SetField(entries[0], "_rewardAmountOverride", 77);
            ConfigTestReflection.SetField(entries[0], "_thinkTimeSecondsOverride", 19f);
            ConfigTestReflection.SetField(entries[2], "_problemType", ProblemType.TargetValueSelection);
            ConfigTestReflection.SetField(entries[2], "_goal", GateGoal.ReachTarget);
            ConfigTestReflection.SetField(entries[2], "_targetValueOverride", 15);
            ConfigTestReflection.SetField(entries[4], "_problemType", ProblemType.TargetValueSelection);
            var baseRewards = Rewards(); var generatedTargets = new Dictionary<int, int> { [4] = 0 };
            var settings = Settings(config, level, baseRewards, generatedTargets, speed: 2.6f);
            Assert.That(settings.GateApproachSpeed, Is.EqualTo(2.6f));
            Assert.That(settings.GateSpawnDepth, Is.EqualTo(config.GateSpawnDepth));
            Assert.That(settings.DecisionZoneDepth, Is.EqualTo(config.DecisionZoneDepth));
            Assert.That(settings.MaxConcurrentMathEvents, Is.EqualTo(config.MaxConcurrentMathEvents));
            Assert.That(settings.FeedbackDisplaySeconds, Is.EqualTo(config.FeedbackDisplaySeconds));
            Assert.That(settings.RewardsByScheduleIndex[0].FlatAmount, Is.EqualTo(77));
            Assert.That(settings.RewardsByScheduleIndex[1].UseSnapshotDelta, Is.True);
            Assert.That(settings.RewardsByScheduleIndex[1].FlatAmount, Is.EqualTo(0));
            Assert.That(settings.RewardsByScheduleIndex[2].FlatAmount, Is.EqualTo(baseRewards[2]));
            Assert.That(settings.TargetsByScheduleIndex[2], Is.EqualTo(15));
            Assert.That(settings.TargetsByScheduleIndex[4], Is.EqualTo(0));
            baseRewards[2] = 99; generatedTargets[4] = 99;
            ConfigTestReflection.SetField(entries[0], "_rewardAmountOverride", 1);
            Assert.That(settings.RewardsByScheduleIndex[0].FlatAmount, Is.EqualTo(77));
            Assert.That(settings.RewardsByScheduleIndex[2].FlatAmount, Is.EqualTo(22));
            Assert.That(settings.TargetsByScheduleIndex[4], Is.EqualTo(0));
        }

        [Test]
        public void Adapter_MissingRewardOrGeneratedTarget_IsAnError()
        {
            var config = Create<MathEventConfigSO>(); var level = Level();
            Assert.Throws<ConfigurationValidationException>(() => Settings(config, level, new Dictionary<int, int>()));
            ConfigTestReflection.SetField(level.MathEvents[1], "_problemType", ProblemType.TargetValueSelection);
            Assert.Throws<ConfigurationValidationException>(() => Settings(config, level));
        }

        [Test]
        public void Adapter_DoesNotTurnResourceDeltaIntoFlatReward_OrAllowMvpIncorrectReward()
        {
            var config = Create<MathEventConfigSO>(); var level = Level();
            ConfigTestReflection.SetField(level.MathEvents[0], "_rewardAmountOverride", 30);
            Assert.Throws<ConfigurationValidationException>(() => Settings(config, level));
            ConfigTestReflection.SetField(level.MathEvents[0], "_rewardAmountOverride", 0);
            ConfigTestReflection.SetField(config, "_incorrectRewardAmount", 1);
            Assert.Throws<ConfigurationValidationException>(() => Settings(config, level));
        }

        [Test]
        public void Adapter_EnforcesStricterClGapAndGlobalFloorAndRepeatLimit()
        {
            var config = Create<MathEventConfigSO>(); var level = Level();
            ConfigTestReflection.SetField(level, "_mathEvents", ConfigTestReflection.MathSchedule(gap: 12f));
            Assert.Throws<ConfigurationValidationException>(() => Settings(config, level));
            Assert.DoesNotThrow(() => Settings(config, level, gap: 8f));
            ConfigTestReflection.SetField(config, "_mathEventCooldownSecondsFloor", 13f);
            Assert.Throws<ConfigurationValidationException>(() => Settings(config, level, gap: 8f));
            ConfigTestReflection.SetField(config, "_mathEventCooldownSecondsFloor", 6f);
            ConfigTestReflection.SetField(config, "_maxConsecutiveSameProblemType", 1);
            ConfigTestReflection.SetField(level.MathEvents[1], "_problemType", ProblemType.ResourceOperationComparison);
            // Two of the same type are legal locally; the configured limit can be stricter.
            ConfigTestReflection.SetField(level.MathEvents[2], "_problemType", ProblemType.AnswerLaneSelection);
            Assert.That(level.Validate(), Is.Empty);
            Assert.Throws<ConfigurationValidationException>(() => Settings(config, level, gap: 8f));
        }

        [Test]
        public void Schedule_RepetitionUsesChronologicalOrder_AndDoesNotAllowThreeEvenWhenConfigDoes()
        {
            var config = Create<MathEventConfigSO>(); var level = Level();
            ConfigTestReflection.SetField(config, "_maxConsecutiveSameProblemType", 3);
            ConfigTestReflection.SetField(level.MathEvents[1], "_problemType", ProblemType.ResourceOperationComparison);
            Assert.That(ConfigTestReflection.AnyContains(level.Validate(), "legfeljebb 2"), Is.True);
            Assert.Throws<ConfigurationValidationException>(() => Settings(config, level));
        }

        [TestCase(float.NaN, 14f)]
        [TestCase(1.2f, float.NaN)]
        [TestCase(1.2f, 7f)]
        public void Adapter_RejectsInvalidClTiming(float speed, float gap)
        {
            var config = Create<MathEventConfigSO>(); var level = Level();
            Assert.Throws<ConfigurationValidationException>(() => Settings(config, level, gap: gap, speed: speed));
        }

#if UNITY_EDITOR
        [Test]
        public void ShippedAssets_ImportAndTranslate_WhileCl1MaxAmmoCoverageRemainsAnHonestFailure()
        {
            var config = AssetDatabase.LoadAssetAtPath<MathEventConfigSO>("Assets/_Project/Configuration/MathEventConfigDefault.asset");
            var level = AssetDatabase.LoadAssetAtPath<LevelDefinitionSO>("Assets/_Project/Configuration/LevelW1L01.asset");
            var table = AssetDatabase.LoadAssetAtPath<MathDifficultyTableSO>("Assets/_Project/Configuration/MathDifficultyTable.asset");
            Assert.That(config, Is.Not.Null); Assert.That(level, Is.Not.Null); Assert.That(table, Is.Not.Null);
            Assert.That(JsonUtility.ToJson(config), Is.EqualTo(JsonUtility.ToJson(Create<MathEventConfigSO>())));
            Assert.That(level.Validate(), Is.Empty);
            var enemies = new List<EnemyDefinitionSO>();
            foreach (var guid in AssetDatabase.FindAssets("t:EnemyDefinitionSO", new[] { "Assets/_Project/Configuration/Enemies" }))
                enemies.Add(AssetDatabase.LoadAssetAtPath<EnemyDefinitionSO>(AssetDatabase.GUIDToAssetPath(guid)));
            var repository = new GameConfigurationRepository(enemies, new[] { level.Weapon }, new[] { level }, mathDifficultyTable: table);
            var dto = repository.GetLevel(new LevelId(level.Id));
            Assert.That(dto.MathEvents.Count, Is.EqualTo(10));
            var settings = Settings(config, level);
            var bus = new GameEventBus(); var resources = new ResourceService(bus, new UnityGameClock());
            resources.Initialize(dto);
            var director = new MathEventDirector(resources, bus, settings);
            director.Initialize(dto, repository.GetDifficultyForLevel(1), new MathProblemGenerator(), new DeterministicRandomSource(7));
            var preparation = director.PrepareUpcomingEvents(0);
            Assert.That(preparation.IsSuccess, Is.False);
            Assert.That(preparation.ErrorCode, Is.EqualTo("GENERATION_EXHAUSTED"));
            Assert.That(dto.MaxAmmo, Is.EqualTo(200));
            Assert.That(repository.GetDifficultyForLevel(1).MaxResultValue, Is.EqualTo(10));
            Assert.That(director.PendingResolutions.Count, Is.EqualTo(0));
        }
#endif
    }
}
