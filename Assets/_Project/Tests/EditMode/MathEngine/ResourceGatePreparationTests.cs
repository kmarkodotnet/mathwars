using System;
using System.Collections.Generic;
using System.Linq;
using MathHorde.Core.Common;
using MathHorde.Core.Expressions;
using MathHorde.Core.Problems;
using MathHorde.Core.Resources;
using MathHorde.Core.Skills;
using MathHorde.MathEngine;
using NUnit.Framework;

namespace MathHorde.Tests.EditMode.MathEngine
{
    [TestFixture]
    public class ResourceGatePreparationTests
    {
        internal static DifficultyProfile Profile(int count = 2, int capacity = 30, int maxResult = 1000,
            int gap = 1, bool negative = false, IReadOnlyCollection<BinaryOperator> operators = null,
            IReadOnlyCollection<SkillTag> skills = null)
            => ProblemValidatorTests.Profile(count: count, maxOperand: capacity, maxResult: maxResult, gap: gap,
                depth: 0, allowNegative: negative, operators: operators ?? new[] { BinaryOperator.Addition, BinaryOperator.Modulo }, skills: skills);

        internal static ResourceGatePreparationRequest Request(int seed = 17, int capacity = 30, DifficultyProfile profile = null,
            GateGoal goal = GateGoal.Maximize, int? target = null, ResourceType resource = ResourceType.Ammo)
            => new ResourceGatePreparationRequest
            {
                Seed = seed, Difficulty = profile ?? Profile(capacity: Math.Max(1, capacity)), LaneCount = 4,
                ThinkTimeSeconds = 6f, RewardResource = resource, Reward = new RewardSpec(resource, 0, true),
                Goal = goal, TargetValue = target, ResourceCapacity = capacity,
            };

        [Test]
        public void Prepare_TenThousandSeededGates_AllSnapshotsPassOfflineOracle_AndLanesStayBalanced()
        {
            var generator = new MathProblemGenerator();
            var validator = new ProblemValidator();
            var profile = Profile(capacity: 10);
            int[] lanes = new int[2];
            int largerOperandWins = 0;
            for (int seed = 0; seed < 10000; seed++)
            {
                var prepared = generator.PrepareResourceGate(Request(seed, 10, profile));
                Assert.That(prepared.IsSuccess, Is.True, "seed=" + seed + " " + prepared.ErrorCode);
                for (int snapshot = 0; snapshot <= 10; snapshot++)
                {
                    var binding = generator.BindResourceGate(prepared.Value, snapshot, 10);
                    Assert.That(binding.IsSuccess, Is.True);
                    Assert.That(validator.IsValid(Materialize(binding.Value), profile), Is.True);
                    if (snapshot == 0)
                    {
                        var b = binding.Value;
                        lanes[b.Template.Options[b.CorrectOptionIndex].Lane.Value]++;
                        int left = ((ConstantNode)((BinaryOperationNode)b.Template.Options[0].GateOperation).Right).Value;
                        int right = ((ConstantNode)((BinaryOperationNode)b.Template.Options[1].GateOperation).Right).Value;
                        if ((left >= right ? 0 : 1) == b.CorrectOptionIndex) largerOperandWins++;
                    }
                }
            }
            double chiSquare = Math.Pow(lanes[0] - 5000, 2) / 5000 + Math.Pow(lanes[1] - 5000, 2) / 5000;
            Assert.That(chiSquare, Is.LessThan(6.634896601)); // df=1, p > 0.01
            Assert.That(largerOperandWins / 10000.0, Is.LessThanOrEqualTo(0.70));
            TestContext.WriteLine("Resource Prepare/Bind: 10000 seeds, 110000 oracle bindings, lanes="
                + lanes[0] + "/" + lanes[1] + ", chiSquare=" + chiSquare + ", largerOperandWins=" + largerOperandWins);
        }

        [TestCase(GateGoal.Minimize)]
        [TestCase(GateGoal.ReachTarget)]
        public void Prepare_ArmorMinimizeAndTargetGoalsKeepTheSameOracleRules(GateGoal goal)
        {
            var generator = new MathProblemGenerator();
            var profile = goal == GateGoal.Minimize ? Profile(negative: true, operators: new[] { BinaryOperator.Subtraction }) : Profile();
            var resource = goal == GateGoal.Minimize ? ResourceType.EnemyArmor : ResourceType.Ammo;
            var prepared = generator.PrepareResourceGate(Request(profile: profile, goal: goal,
                target: goal == GateGoal.ReachTarget ? 0 : (int?)null, resource: resource));
            Assert.That(prepared.IsSuccess, Is.True, prepared.ErrorCode);
            for (int snapshot = 0; snapshot <= 30; snapshot++)
            {
                var b = generator.BindResourceGate(prepared.Value, snapshot, 30).Value;
                Assert.That(new ProblemValidator().IsValid(Materialize(b), profile), Is.True);
                if (goal == GateGoal.Minimize) Assert.That(b.GetValue(b.CorrectOptionIndex).Value - snapshot, Is.LessThan(0));
            }
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void Prepare_ReplayIsIdentical_AndSupportsOptionCounts(int count)
        {
            var generator = new MathProblemGenerator();
            var profile = Profile(count: count, negative: true, operators: new[] { BinaryOperator.Subtraction });
            var request = Request(profile: profile);
            var a = generator.PrepareResourceGate(request);
            var b = generator.PrepareResourceGate(request);
            Assert.That(a.IsSuccess, Is.True, a.ErrorCode);
            Assert.That(b.IsSuccess, Is.True, b.ErrorCode);
            Assert.That(a.Value.Id, Is.EqualTo(b.Value.Id));
            for (int s = 0; s <= 30; s++)
            {
                var x = generator.BindResourceGate(a.Value, s, 30).Value;
                var y = generator.BindResourceGate(b.Value, s, 30).Value;
                Assert.That(x.OptionCount, Is.EqualTo(count));
                Assert.That(x.CandidateIndex, Is.EqualTo(y.CandidateIndex));
                Assert.That(x.CorrectOptionIndex, Is.EqualTo(y.CorrectOptionIndex));
                for (int i = 0; i < x.OptionCount; i++)
                {
                    Assert.That(x.GetValue(i), Is.EqualTo(y.GetValue(i)));
                    Assert.That(x.Template.Options[i].DisplayLabel, Is.EqualTo(y.Template.Options[i].DisplayLabel));
                }
            }
        }

        [Test]
        public void Bind_ReferenceCaseUsesFreshSnapshot_AndPreservesRawDeltaAndCapturedCapacity()
        {
            var generator = new MathProblemGenerator();
            var prepared = ReferencePrepared(60, GateGoal.Maximize, null);
            var at20 = generator.BindResourceGate(prepared, 20, 60).Value;
            var at30 = generator.BindResourceGate(prepared, 30, 60).Value;
            Assert.That(at20.CorrectOptionIndex, Is.EqualTo(1));
            Assert.That(at20.GetValue(1).Value - at20.SnapshotValue, Is.EqualTo(25));
            Assert.That(at30.CorrectOptionIndex, Is.EqualTo(0));
            Assert.That(at30.GetValue(0).Value, Is.EqualTo(60));
            Assert.That(at20.GetValue(0).Value, Is.EqualTo(40));
            Assert.That(at20.ResourceCapacity, Is.EqualTo(60));
        }

        [TestCase(GateGoal.Maximize, null)]
        [TestCase(GateGoal.ReachTarget, 60)]
        public void Bind_NumericAcceptanceAgreesWithOfflineValidator_IncludingCapTiesAndTargetInversions(GateGoal goal, int? target)
        {
            var generator = new MathProblemGenerator();
            var validator = new ProblemValidator();
            var prepared = ReferencePrepared(60, goal, target);
            for (int snapshot = 0; snapshot <= 60; snapshot++)
            {
                var result = generator.BindResourceGate(prepared, snapshot, 60);
                // Independently choose the raw optimum and materialize the old API,
                // including invalid numeric pairs. The oracle never runs in Bind.
                var values = new[] { new EvaluationResult(snapshot * 2), new EvaluationResult(snapshot + 25) };
                long score0 = goal == GateGoal.Maximize ? -(long)values[0].Value : Math.Abs((long)values[0].Value - target.Value);
                long score1 = goal == GateGoal.Maximize ? -(long)values[1].Value : Math.Abs((long)values[1].Value - target.Value);
                int winner = score0 <= score1 ? 0 : 1;
                var candidate = new ResourceGateBinding(prepared, 0, snapshot, 60, winner, values[0], values[1]);
                Assert.That(result.IsSuccess, Is.EqualTo(validator.IsValid(Materialize(candidate), prepared.Difficulty)), "snapshot=" + snapshot);
            }
        }

        [TestCase(20, 100, GateGoal.Maximize, null, 4, null)]
        [TestCase(20, 44, GateGoal.Maximize, null, 4, null)]
        [TestCase(20, 43, GateGoal.Maximize, null, 1, null)]
        [TestCase(20, 43, GateGoal.Maximize, null, 4, "GATE_GAP_TOO_SMALL")]
        [TestCase(20, 40, GateGoal.Maximize, null, 4, "GATE_TIE")]
        [TestCase(25, 100, GateGoal.Maximize, null, 4, "GATE_TIE")]
        [TestCase(20, 44, GateGoal.ReachTarget, 42, 4, "GATE_TIE")]
        [TestCase(40, 72, GateGoal.ReachTarget, 70, 4, "GATE_ORDER_INVERTED_BY_CAP")]
        public void Bind_CapacityBoundaryCasesAgreeWithOfflineErrors_AndRetainRawValues(
            int snapshot, int capacity, GateGoal goal, int? target, int gap, string error)
        {
            var prepared = ReferencePrepared(capacity, goal, target, gap);
            int a = snapshot * 2, b = snapshot + 25;
            long scoreA = goal == GateGoal.Maximize ? -(long)a : Math.Abs((long)a - target.Value);
            long scoreB = goal == GateGoal.Maximize ? -(long)b : Math.Abs((long)b - target.Value);
            int winner = scoreA <= scoreB ? 0 : 1;
            var candidate = new ResourceGateBinding(prepared, 0, snapshot, capacity, winner,
                new EvaluationResult(a), new EvaluationResult(b));
            var report = new ProblemValidator().Validate(Materialize(candidate), prepared.Difficulty);
            var bound = new MathProblemGenerator().BindResourceGate(prepared, snapshot, capacity);
            Assert.That(report.IsValid, Is.EqualTo(error == null));
            Assert.That(bound.IsSuccess, Is.EqualTo(report.IsValid));
            if (error != null)
            {
                Assert.That(ProblemValidatorTests.Codes(report), Does.Contain(error));
                Assert.That(bound.ErrorCode, Is.EqualTo("GENERATION_EXHAUSTED"));
                return;
            }
            Assert.That(bound.Value.CorrectOptionIndex, Is.EqualTo(winner));
            Assert.That(bound.Value.GetValue(0).Value, Is.EqualTo(a));
            Assert.That(bound.Value.GetValue(1).Value, Is.EqualTo(b));
            Assert.That(bound.Value.GetValue(winner).Value - bound.Value.SnapshotValue, Is.EqualTo(winner == 0 ? snapshot : 25));
            Assert.That(bound.Value.ResourceCapacity, Is.EqualTo(capacity));
        }

        [Test]
        public void Bind_BothSuccessAndEveryBusinessFailure_AllocateZeroBytes()
        {
            var generator = new MathProblemGenerator();
            var prepared = ReferencePrepared(60, GateGoal.Maximize, null);
            for (int i = 0; i < 50; i++) ExerciseBindings(generator, prepared);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10000; i++) ExerciseBindings(generator, prepared);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.EqualTo(0));
        }

        private static void ExerciseBindings(MathProblemGenerator generator, PreparedResourceGate prepared)
        {
            generator.BindResourceGate(prepared, 20, 60); // success
            generator.BindResourceGate(prepared, 25, 60); // tied raw values
            generator.BindResourceGate(prepared, -1, 60);
            generator.BindResourceGate(prepared, 61, 60);
            generator.BindResourceGate(prepared, 20, 0);
            generator.BindResourceGate(prepared, 20, 61);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(100)]
        public void Prepare_CapacityEndpointsAreCoveredWithoutChangingTheProfile(int capacity)
        {
            var generator = new MathProblemGenerator();
            var profile = Profile(capacity: Math.Max(2, capacity));
            var prepared = generator.PrepareResourceGate(Request(capacity: capacity, profile: profile));
            Assert.That(prepared.IsSuccess, Is.True, prepared.ErrorCode);
            Assert.That(prepared.Value.Difficulty, Is.SameAs(profile));
            Assert.That(generator.BindResourceGate(prepared.Value, 0, capacity).IsSuccess, Is.True);
            Assert.That(generator.BindResourceGate(prepared.Value, capacity, capacity).IsSuccess, Is.True);
            Assert.That(generator.BindResourceGate(prepared.Value, capacity, int.MaxValue).ErrorCode, Is.EqualTo("GATE_CONTEXT_CHANGED"));
        }

        [TestCase(BinaryOperator.Multiplication, int.MaxValue)]
        [TestCase(BinaryOperator.Division, 0)]
        [TestCase(BinaryOperator.Division, 3)]
        [TestCase(BinaryOperator.Modulo, 0)]
        public void Bind_InvalidArithmeticIsRejectedWithoutAllocatingAnException(BinaryOperator op, int operand)
        {
            var options = new[]
            {
                new ResourceGateOptionTemplate(0, new LaneIndex(0), new BinaryOperationNode(op, new UnknownNode(), new ConstantNode(operand)), "invalid"),
                new ResourceGateOptionTemplate(1, new LaneIndex(1), new BinaryOperationNode(BinaryOperator.Subtraction, new UnknownNode(), new ConstantNode(1)), "small"),
            };
            var tags = new[] { SkillTag.ResourceComparison, SkillTag.Multiplication, SkillTag.Subtraction };
            var template = new ResourceGateTemplate(options, tags);
            var metadata = new IReadOnlyList<SkillTag>[] { tags, tags, tags };
            var prepared = new PreparedResourceGate(new MathProblemId(Guid.Empty), 1,
                Profile(capacity: int.MaxValue, maxResult: int.MaxValue), 1f, new RewardSpec(ResourceType.Ammo, 0, true),
                GateGoal.Maximize, null, 2, new[] { template }, new[] { metadata });
            var generator = new MathProblemGenerator();
            generator.BindResourceGate(prepared, 2, 2);
            long before = GC.GetAllocatedBytesForCurrentThread();
            var result = generator.BindResourceGate(prepared, 2, 2);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(result.ErrorCode, Is.EqualTo("GENERATION_EXHAUSTED")); Assert.That(allocated, Is.EqualTo(0));
        }

        [Test]
        public void PreparedCopiesEveryInputCollectionIncludingNestedSkillMetadata()
        {
            var operation = new BinaryOperationNode(BinaryOperator.Addition, new UnknownNode(), new ConstantNode(1));
            var options = new[] { new ResourceGateOptionTemplate(0, new LaneIndex(0), operation, "+1"),
                new ResourceGateOptionTemplate(1, new LaneIndex(1), operation, "+1") };
            var tags = new List<SkillTag> { SkillTag.Addition };
            var template = new ResourceGateTemplate(options, tags);
            var candidates = new List<ResourceGateTemplate> { template };
            var metadata = new IReadOnlyList<SkillTag>[] { tags, tags };
            var nested = new List<IReadOnlyList<SkillTag>[]> { metadata };
            var prepared = new PreparedResourceGate(new MathProblemId(Guid.Empty), 7, Profile(), 6f,
                new RewardSpec(ResourceType.Ammo, 0, true), GateGoal.Maximize, null, 1, candidates, nested);
            options[0] = default; tags.Clear(); candidates.Clear(); metadata[0] = null; nested.Clear();
            Assert.That(prepared.Candidates.Count, Is.EqualTo(1));
            Assert.That(template.Options[0].DisplayLabel, Is.EqualTo("+1")); Assert.That(template.SkillTags.Count, Is.EqualTo(1));
            Assert.That(prepared.GetSkillTags(0, 0).Count, Is.EqualTo(1));
        }

        [Test]
        public void Prepare_InvalidAndUnsupportedContextsAreHonestFailures()
        {
            var generator = new MathProblemGenerator();
            Assert.That(generator.PrepareResourceGate(Request(capacity: 0)).ErrorCode, Is.EqualTo("GATE_CAPACITY_INVALID"));
            Assert.That(generator.PrepareResourceGate(Request(goal: GateGoal.ReachTarget)).ErrorCode, Is.EqualTo("GATE_TARGET_MISSING"));
            Assert.That(generator.PrepareResourceGate(Request(capacity: 200, profile: Profile(capacity: 10, maxResult: 10))).ErrorCode,
                Is.EqualTo("GENERATION_EXHAUSTED"));
            Assert.That(generator.PrepareResourceGate(Request(profile: Profile(operators: new[] { BinaryOperator.Addition }))).ErrorCode,
                Is.EqualTo("GENERATION_EXHAUSTED"));
            Assert.That(generator.PrepareResourceGate(Request(goal: GateGoal.Minimize, resource: ResourceType.EnemyArmor)).ErrorCode,
                Is.EqualTo("GENERATION_EXHAUSTED")); // snapshot=0 cannot decrease without negative results
            Assert.That(() => generator.PrepareResourceGate(null), Throws.TypeOf<ArgumentNullException>());
            Assert.That(() => generator.BindResourceGate(null, 0, 30), Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void PreparedCollectionsAreCopiedAndReadOnly_AndSkillsMatchConcreteSnapshot()
        {
            var generator = new MathProblemGenerator();
            var p = generator.PrepareResourceGate(Request()).Value;
            var b = generator.BindResourceGate(p, 9, 30).Value;
            Assert.That(b.SkillTags, Is.EquivalentTo(Materialize(b).SkillTags));
            Assert.That(() => ((IList<ResourceGateTemplate>)p.Candidates).Clear(), Throws.TypeOf<NotSupportedException>());
            Assert.That(() => ((IList<ResourceGateOptionTemplate>)b.Template.Options).Clear(), Throws.TypeOf<NotSupportedException>());
            Assert.That(() => ((IList<SkillTag>)b.SkillTags).Clear(), Throws.TypeOf<NotSupportedException>());
            Assert.That(() => b.GetValue(-1), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => b.GetValue(2), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(default(ResourceGateBinding).Prepared, Is.Null);
        }

        internal static MathProblem Materialize(ResourceGateBinding binding)
        {
            var p = binding.Prepared;
            var options = new AnswerOption[binding.OptionCount];
            for (int i = 0; i < options.Length; i++)
            {
                var o = binding.Template.Options[i];
                options[i] = new AnswerOption(o.OptionIndex, o.Lane, binding.GetValue(i), o.GateOperation,
                    i == binding.CorrectOptionIndex, null, o.DisplayLabel);
            }
            var operation = (BinaryOperationNode)binding.Template.Options[binding.CorrectOptionIndex].GateOperation;
            var expression = new BinaryOperationNode(operation.Operator, new ConstantNode(binding.SnapshotValue), operation.Right);
            var tags = new SortedSet<SkillTag>(binding.Template.SkillTags);
            tags.UnionWith(new SkillClassifier().Classify(expression, ProblemType.ResourceOperationComparison));
            return new MathProblem(p.Id, ProblemType.ResourceOperationComparison, expression, expression.ToDisplayString(),
                binding.GetValue(binding.CorrectOptionIndex), options, p.Difficulty.ChallengeLevel, tags.ToList().AsReadOnly(),
                p.ThinkTimeSeconds, p.Reward, p.Seed, p.Goal, p.TargetValue, null, binding.ResourceCapacity);
        }

        internal static PreparedResourceGate ReferencePrepared(int capacity, GateGoal goal, int? target, int gap = 1)
        {
            var options = new[]
            {
                new ResourceGateOptionTemplate(0, new LaneIndex(0), new BinaryOperationNode(BinaryOperator.Multiplication, new UnknownNode(), new ConstantNode(2)), "×2"),
                new ResourceGateOptionTemplate(1, new LaneIndex(1), new BinaryOperationNode(BinaryOperator.Addition, new UnknownNode(), new ConstantNode(25)), "+25"),
            };
            var classifier = new SkillClassifier();
            var symbolic = new SortedSet<SkillTag>();
            foreach (var o in options) symbolic.UnionWith(classifier.Classify(o.GateOperation, ProblemType.ResourceOperationComparison));
            var template = new ResourceGateTemplate(options, symbolic.ToList());
            var metadata = new IReadOnlyList<SkillTag>[capacity + 1];
            for (int s = 0; s <= capacity; s++)
            {
                long a = goal == GateGoal.Maximize ? -s * 2 : Math.Abs((long)s * 2 - target.Value);
                long b = goal == GateGoal.Maximize ? -s - 25 : Math.Abs((long)s + 25 - target.Value);
                var op = (BinaryOperationNode)options[a <= b ? 0 : 1].GateOperation;
                var tags = new SortedSet<SkillTag>(symbolic);
                tags.UnionWith(classifier.Classify(new BinaryOperationNode(op.Operator, new ConstantNode(s), op.Right), ProblemType.ResourceOperationComparison));
                metadata[s] = tags.ToList();
            }
            return new PreparedResourceGate(new MathProblemId(Guid.Empty), 1, Profile(capacity: Math.Max(100, capacity), gap: gap,
                    operators: new[] { BinaryOperator.Multiplication, BinaryOperator.Addition }), 6f,
                new RewardSpec(ResourceType.Ammo, 0, true), goal, target, capacity, new[] { template }, new[] { metadata });
        }
    }
}
