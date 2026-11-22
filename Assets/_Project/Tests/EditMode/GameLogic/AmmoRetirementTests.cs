using System;
using System.Collections.Generic;
using System.Linq;
using MathHorde.Core.Common;
using MathHorde.Core.Levels;
using MathHorde.Core.Problems;
using MathHorde.Core.Progress;
using MathHorde.Core.Resources;
using MathHorde.GameLogic;
using MathHorde.MathEngine;
using MathHorde.Tests.EditMode.MathEngine;
using MathHorde.Tests.EditMode.TestSupport;
using NUnit.Framework;

namespace MathHorde.Tests.EditMode.GameLogic
{
    public class AmmoRetirementTests
    {
        [Test]
        public void PersistedOrdinalsRemainStable_AndLivePoolsRejectTheTombstone()
        {
            Assert.That((int)ResourceType.Ammo, Is.EqualTo(0));
            Assert.That((int)ResourceType.Health, Is.EqualTo(1));
            Assert.That((int)ResourceType.Shield, Is.EqualTo(2));
            Assert.That((int)ResourceType.EnemyArmor, Is.EqualTo(3));
            Assert.That((int)ResourceType.Energy, Is.EqualTo(4));
            Assert.That((int)LossDiagnosis.AmmoStarvation, Is.EqualTo(1));
            Assert.That(() => new ResourcePool(ResourceType.Ammo, 20, 200), Throws.TypeOf<ArgumentException>());
            Assert.That(() => new ResourceState(new Dictionary<ResourceType, ResourcePool>
                { [ResourceType.Ammo] = new ResourcePool(ResourceType.Health, 20, 200) }), Throws.TypeOf<ArgumentException>());
            var state = new ResourceState(null);
            Assert.That(() => state.GetCurrent(ResourceType.Ammo), Throws.TypeOf<ArgumentException>());
            Assert.That(() => state.GetMax(ResourceType.Ammo), Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void EveryLiveResourceApiRejectsAmmoBeforeMutationOrEvents()
        {
            var sink = new RecordingGameEventSink();
            var resources = new MathHorde.GameLogic.ResourceService(sink, new GameClock());
            resources.Initialize(CombatTestFixtures.BuildLevel());
            int before = sink.PublishedCount;
            var state = resources.State;
            Assert.That(state.Pools.ContainsKey(ResourceType.Ammo), Is.False);
            Assert.That(() => resources.GetCurrent(ResourceType.Ammo), Throws.TypeOf<ArgumentException>());
            Assert.That(() => resources.GetMax(ResourceType.Ammo), Throws.TypeOf<ArgumentException>());
            Assert.That(() => resources.ApplyDelta(ResourceType.Ammo, 1, "INVALID"), Throws.TypeOf<ArgumentException>());
            Assert.That(() => resources.SetMax(ResourceType.Ammo, 200, "INVALID"), Throws.TypeOf<ArgumentException>());
            Assert.That(() => resources.TryConsume(ResourceType.Ammo, 0, "INVALID", out _), Throws.TypeOf<ArgumentException>());
            Assert.That(resources.State, Is.SameAs(state));
            Assert.That(sink.PublishedCount, Is.EqualTo(before));
            Assert.That(() => new MathEventScheduleEntry(0, ProblemType.AnswerLaneSelection, 1, 6,
                ResourceType.Ammo, GateGoal.Maximize), Throws.TypeOf<ArgumentException>());
        }

        [TestCase(ProblemType.ResourceOperationComparison)]
        [TestCase(ProblemType.AnswerLaneSelection)]
        [TestCase(ProblemType.MissingOperand)]
        [TestCase(ProblemType.EquationSolving)]
        [TestCase(ProblemType.TargetValueSelection)]
        public void GenerateAndBatch_CheckBothRewardFieldsBeforeEveryGoal(ProblemType type)
        {
            var generator = new MathProblemGenerator();
            foreach (GateGoal goal in Enum.GetValues(typeof(GateGoal)))
            foreach (var pair in Pairs())
            {
                var r = MathProblemGeneratorTests.Request(type: type, goal: goal);
                var invalid = new ProblemGenerationRequest
                {
                    Seed = r.Seed, Type = r.Type, Difficulty = r.Difficulty, SkillProfile = r.SkillProfile,
                    LaneCount = r.LaneCount, ThinkTimeSeconds = r.ThinkTimeSeconds, Goal = r.Goal,
                    TargetValue = r.TargetValue,
                    GateLimits = r.GateLimits, RewardResource = pair.Item1,
                    Reward = new RewardSpec(pair.Item2, r.Reward.FlatAmount)
                };
                string error = pair.Item1 == ResourceType.Ammo || pair.Item2 == ResourceType.Ammo
                    ? "AMMO_NOT_ALLOWED" : "REWARD_RESOURCE_NOT_ALLOWED";
                Assert.That(generator.Generate(invalid).ErrorCode, Is.EqualTo(error));
                foreach (var result in generator.GenerateBatch(invalid, 3))
                    Assert.That(result.ErrorCode, Is.EqualTo(error));
            }
        }



        [TestCase(ProblemType.ResourceOperationComparison)]
        [TestCase(ProblemType.AnswerLaneSelection)]
        [TestCase(ProblemType.MissingOperand)]
        [TestCase(ProblemType.EquationSolving)]
        [TestCase(ProblemType.TargetValueSelection)]
        public void BothValidatorEntriesRejectAmmoRegardlessOfTypeOrGoal(ProblemType type)
        {
            var validator = new ProblemValidator();
            foreach (GateGoal goal in Enum.GetValues(typeof(GateGoal)))
            {
                var r = MathProblemGeneratorTests.Request(type: type);
                var good = new MathProblemGenerator().Generate(r);
                Assert.That(good.IsSuccess, Is.True, good.ErrorCode);
                var p = good.Value;
                var invalid = new MathProblem(p.Id, p.Type, p.Expression, p.DisplayExpression,
                    p.CorrectAnswer, p.Options, p.DifficultyLevel, p.SkillTags, p.ThinkTimeSeconds,
                    new RewardSpec(ResourceType.Ammo, p.Reward.FlatAmount),
                    p.Seed, goal, p.TargetValue, p.Explanation, p.GateBaseValue);
                Assert.That(validator.Validate(invalid, r.Difficulty).Issues.Any(i => i.Code == "AMMO_NOT_ALLOWED"), Is.True);
                Assert.That(validator.IsValid(invalid, r.Difficulty), Is.False);
            }
        }

        [Test]
        public void RewardRejectionPrecedesPoolAndJournalChanges_EvenForReplaysOrSkippedOutcomes()
        {
            var sink = new RecordingGameEventSink(); var clock = new GameClock();
            var resources = new MathHorde.GameLogic.ResourceService(sink, clock);
            resources.Initialize(CombatTestFixtures.BuildLevel(startingHealth: 20));
            var rewards = new MathHorde.GameLogic.RewardService(resources, sink, clock, 1);
            var id = MathEventId.Create(7, 0); int before = sink.PublishedCount;
            foreach (MathHorde.GameLogic.GateResolutionOutcome outcome in Enum.GetValues(typeof(MathHorde.GameLogic.GateResolutionOutcome)))
            {
                var invalid = new MathHorde.GameLogic.GateResolution(id, outcome, true, 0, 45, 20, 25, ResourceType.Ammo, default);
                Assert.That(() => rewards.ApplyReward(invalid), Throws.TypeOf<ArgumentException>());
                Assert.That(rewards.AppliedRewardEventIds.Count, Is.EqualTo(0));
            }
            Assert.That(() => rewards.ApplyCombatReward(ResourceType.Ammo, 25, "INVALID"), Throws.TypeOf<ArgumentException>());
            Assert.That(sink.PublishedCount, Is.EqualTo(before));
            var valid = new MathHorde.GameLogic.GateResolution(id, MathHorde.GameLogic.GateResolutionOutcome.CorrectRewarded,
                true, 0, 45, 20, 25, ResourceType.Health, default);
            Assert.That(rewards.ApplyReward(valid).AppliedDelta, Is.EqualTo(25));
            var replay = new MathHorde.GameLogic.GateResolution(id, valid.Outcome, true, 0, 45, 20, 25, ResourceType.Ammo, default);
            Assert.That(() => rewards.ApplyReward(replay), Throws.TypeOf<ArgumentException>());
            Assert.That(rewards.AppliedRewardEventIds.Count, Is.EqualTo(1));
            Assert.That(resources.GetCurrent(ResourceType.Health), Is.EqualTo(45));
        }

        private static Tuple<ResourceType, ResourceType>[] Pairs() => new[]
        {
            Tuple.Create(ResourceType.Ammo, ResourceType.Health),
            Tuple.Create(ResourceType.Health, ResourceType.Ammo),
            Tuple.Create(ResourceType.Ammo, ResourceType.Ammo),
            Tuple.Create(ResourceType.Health, ResourceType.Shield)
        };
    }
}
