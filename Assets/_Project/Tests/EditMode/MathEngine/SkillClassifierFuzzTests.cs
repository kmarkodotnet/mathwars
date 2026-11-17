using System;
using System.Collections.Generic;
using MathHorde.Core.Common;
using MathHorde.Core.Expressions;
using MathHorde.Core.Problems;
using MathHorde.Core.Skills;
using MathHorde.MathEngine;
using NUnit.Framework;

namespace MathHorde.Tests.EditMode.MathEngine
{
    // 10 000 seedelt feladat invariáns-ellenőrzése az ISkillClassifier-ra
    // (math-horde.md 17.1, task F2-06 fázis-kapuja: "dotnet test Unity
    // nélkül zöld, 10 000 feladat invariánsa hibátlan"). Minden iteráció egy
    // IExpressionGenerator (F2-05)-generált fán futtatja Classify/
    // ClassifyError/GetPrerequisites/IsEligible-t — ugyanaz a
    // DeterministicRandomSource-mintázat, mint ExpressionGeneratorFuzzTests
    // -ben (ADR-006, nincs System.Random).
    [TestFixture]
    public class SkillClassifierFuzzTests
    {
        private const int IterationCount = 10_000;
        private const int MasterSeed = 260611;
        private const int CategoryCount = 6;

        private IExpressionEvaluator _evaluator;
        private IEquationSolver _solver;
        private ExpressionGenerator _generator;
        private SkillClassifier _classifier;
        private SkillTag[] _allSkillTags;

        [SetUp]
        public void SetUp()
        {
            _evaluator = new ExpressionEvaluator();
            _solver = new EquationSolver(_evaluator);
            _generator = new ExpressionGenerator(_evaluator, _solver);
            _classifier = new SkillClassifier();
            _allSkillTags = (SkillTag[])Enum.GetValues(typeof(SkillTag));
        }

        [Test]
        public void Classify_And_ClassifyError_TenThousandSeededExpressions_SatisfyAllInvariants()
        {
            var master = new DeterministicRandomSource(MasterSeed);

            for (int i = 0; i < IterationCount; i++)
            {
                IRandomSource paramRng = master.Derive("params-" + i);
                int genSeed = master.Derive("gen-" + i).Seed;

                int category = i % CategoryCount;
                var request = BuildRequestForCategory(category, paramRng, genSeed);

                var generated = _generator.Generate(request);
                if (!generated.IsSuccess)
                {
                    Assert.That(generated.ErrorCode, Is.EqualTo("GENERATION_EXHAUSTED"), "iteration " + i);
                    continue;
                }

                ExpressionNode tree = generated.Value.Root;
                ProblemType type = request.IncludeUnknown ? ProblemType.EquationSolving : ProblemType.AnswerLaneSelection;
                string ctx = "category " + category + ", iteration " + i;

                // --- Classify invariants -------------------------------------------
                IReadOnlyList<SkillTag> tags = _classifier.Classify(tree, type);

                Assert.That(tags, Is.Not.Null, ctx);
                Assert.That(tags.Count, Is.GreaterThan(0), ctx);

                var usedOperators = new List<BinaryOperator>();
                CollectOperators(tree, usedOperators);
                foreach (var op in usedOperators)
                {
                    // "tükrözi a fában ténylegesen előforduló műveleteket" (core-domain.md
                    // I5) — every operator actually present maps to at least its coarse tag.
                    SkillTag coarse = CoarseTagFor(op);
                    Assert.That(Contains(tags, coarse), Is.True, ctx + " missing coarse tag for " + op);
                }

                // Determinism (P2): classifying the identical tree twice must
                // produce the identical tag set (same elements and order).
                var tagsAgain = _classifier.Classify(tree, type);
                Assert.That(tagsAgain.Count, Is.EqualTo(tags.Count), ctx);
                for (int t = 0; t < tags.Count; t++)
                {
                    Assert.That(tagsAgain[t], Is.EqualTo(tags[t]), ctx);
                }

                // No duplicate tags (HashSet-backed by construction, verified here too).
                var seen = new HashSet<SkillTag>();
                foreach (var tag in tags)
                {
                    Assert.That(seen.Add(tag), Is.True, ctx + " duplicate tag " + tag);
                }

                // --- ClassifyError invariants ---------------------------------------
                // For EquationSolving the thing a student actually answers is the
                // solved UNKNOWN, not the equation's known-side constant.
                var correct = request.IncludeUnknown
                    ? new EvaluationResult(generated.Value.UnknownSolution.Value)
                    : new EvaluationResult(generated.Value.Value.Value, generated.Value.Value.Remainder);

                // Never throws, and the exact-match case is always None.
                Assert.That(_classifier.ClassifyError(tree, correct, correct), Is.EqualTo(ErrorCategory.None), ctx);

                // A handful of deterministic perturbations around the correct
                // value must classify into SOME defined category without throwing.
                foreach (int delta in new[] { 1, -1, 10, -10 })
                {
                    var selected = new EvaluationResult(correct.Value + delta);
                    ErrorCategory category2 = _classifier.ClassifyError(tree, correct, selected);
                    Assert.That(Enum.IsDefined(typeof(ErrorCategory), category2), Is.True, ctx + " delta " + delta);

                    if (delta != 0)
                    {
                        Assert.That(category2, Is.Not.EqualTo(ErrorCategory.None), ctx + " delta " + delta);
                    }
                }

                // --- GetPrerequisites / IsEligible invariants -----------------------
                foreach (var tag in tags)
                {
                    var prereqs = _classifier.GetPrerequisites(tag);
                    Assert.That(prereqs, Is.Not.Null, ctx);

                    // IsEligible must never throw for any skill/empty-profile combination.
                    var emptyProfile = new SkillProfile(new Dictionary<SkillTag, SkillStatistics>(), DateTimeOffset.UtcNow);
                    bool eligible = _classifier.IsEligible(tag, emptyProfile);

                    // A skill with NO prerequisites is always eligible, regardless of profile state.
                    if (prereqs.Count == 0)
                    {
                        Assert.That(eligible, Is.True, ctx + " " + tag);
                    }
                }
            }
        }

        [Test]
        public void IsEligible_NeverThrows_ForEverySkillTagAgainstEveryMasteryLevelCombination()
        {
            // Not tree-dependent — a direct, exhaustive sweep over every
            // SkillTag paired with a profile where ALL its direct
            // prerequisites sit at each possible SkillMasteryLevel in turn.
            foreach (var tag in _allSkillTags)
            {
                var prereqs = _classifier.GetPrerequisites(tag);
                foreach (SkillMasteryLevel level in Enum.GetValues(typeof(SkillMasteryLevel)))
                {
                    var skills = new Dictionary<SkillTag, SkillStatistics>();
                    foreach (var prereq in prereqs)
                    {
                        skills[prereq] = StatsForLevel(prereq, level);
                    }

                    var profile = new SkillProfile(skills, DateTimeOffset.UtcNow);
                    Assert.That(() => _classifier.IsEligible(tag, profile), Throws.Nothing, tag + "/" + level);
                }
            }
        }

        private static SkillStatistics StatsForLevel(SkillTag tag, SkillMasteryLevel level)
        {
            switch (level)
            {
                case SkillMasteryLevel.Locked:
                case SkillMasteryLevel.Introduced:
                    return new SkillStatistics(tag, 1, attempts: 0, correctAnswers: 0, 0f, 0f, null, 0f, 0f, null);

                case SkillMasteryLevel.Practiced:
                    return new SkillStatistics(tag, 1, attempts: 6, correctAnswers: 5, 1f, 1f, null, 0.6f, 0.1f, null);

                case SkillMasteryLevel.Mastered:
                    return new SkillStatistics(tag, 1, attempts: 10, correctAnswers: 9, 1f, 1f, null, 0.9f, 0.8f, null);

                default:
                    throw new ArgumentOutOfRangeException(nameof(level), level, "Ismeretlen SkillMasteryLevel.");
            }
        }

        private static SkillTag CoarseTagFor(BinaryOperator op)
        {
            switch (op)
            {
                case BinaryOperator.Addition: return SkillTag.Addition;
                case BinaryOperator.Subtraction: return SkillTag.Subtraction;
                case BinaryOperator.Multiplication: return SkillTag.Multiplication;
                case BinaryOperator.Division:
                case BinaryOperator.Modulo:
                case BinaryOperator.IntegerDivisionWithRemainder:
                    return SkillTag.Division;
                default:
                    throw new ArgumentOutOfRangeException(nameof(op), op, "Ismeretlen BinaryOperator.");
            }
        }

        private static bool Contains(IReadOnlyList<SkillTag> tags, SkillTag tag)
        {
            foreach (var item in tags)
            {
                if (item == tag)
                {
                    return true;
                }
            }

            return false;
        }

        private static void CollectOperators(ExpressionNode node, List<BinaryOperator> into)
        {
            switch (node)
            {
                case ConstantNode:
                case UnknownNode:
                    return;

                case BinaryOperationNode binary:
                    into.Add(binary.Operator);
                    CollectOperators(binary.Left, into);
                    CollectOperators(binary.Right, into);
                    return;

                case EqualityNode equality:
                    CollectOperators(equality.Left, into);
                    CollectOperators(equality.Right, into);
                    return;
            }
        }

        private static ExpressionGenerationRequest BuildRequestForCategory(int category, IRandomSource paramRng, int genSeed)
        {
            var random = new DeterministicRandomSource(genSeed);

            switch (category)
            {
                case 0: // Flat Addition/Subtraction chain
                    return new ExpressionGenerationRequest
                    {
                        Random = random,
                        AllowedOperators = new[] { BinaryOperator.Addition, BinaryOperator.Subtraction },
                        MinOperandValue = 1,
                        MaxOperandValue = paramRng.Next(5, 90),
                        MaxResultValue = 1_000_000,
                        MinOperandCount = 2,
                        MaxOperandCount = paramRng.Next(2, 6),
                        MaxAttempts = 200,
                    };

                case 1: // Multiplication only
                    return new ExpressionGenerationRequest
                    {
                        Random = random,
                        AllowedOperators = new[] { BinaryOperator.Multiplication },
                        MinOperandValue = 1,
                        MaxOperandValue = paramRng.Next(2, 20),
                        MaxResultValue = 1_000_000,
                        MinOperandCount = 2,
                        MaxOperandCount = 2,
                        MaxAttempts = 200,
                    };

                case 2: // Division only
                    return new ExpressionGenerationRequest
                    {
                        Random = random,
                        AllowedOperators = new[] { BinaryOperator.Division },
                        MinOperandValue = 1,
                        MaxOperandValue = paramRng.Next(5, 50),
                        MaxResultValue = 1_000_000,
                        MinOperandCount = 2,
                        MaxOperandCount = 2,
                        MaxAttempts = 200,
                    };

                case 3: // IntegerDivisionWithRemainder only
                    return new ExpressionGenerationRequest
                    {
                        Random = random,
                        AllowedOperators = new[] { BinaryOperator.IntegerDivisionWithRemainder },
                        MinOperandValue = 1,
                        MaxOperandValue = paramRng.Next(5, 50),
                        MaxResultValue = 1_000_000,
                        MinOperandCount = 2,
                        MaxOperandCount = 2,
                        MaxAttempts = 200,
                    };

                case 4: // Addition + Multiplication precedence mix, optional parentheses
                    return new ExpressionGenerationRequest
                    {
                        Random = random,
                        AllowedOperators = new[] { BinaryOperator.Addition, BinaryOperator.Multiplication },
                        MinOperandValue = 1,
                        MaxOperandValue = paramRng.Next(5, 20),
                        MaxResultValue = 1_000_000,
                        MinOperandCount = 3,
                        MaxOperandCount = 5,
                        AllowParentheses = true,
                        MaxParenthesisDepth = 2,
                        MaxAttempts = 300,
                    };

                case 5: // IncludeUnknown (EquationSolving)
                    return new ExpressionGenerationRequest
                    {
                        Random = random,
                        AllowedOperators = new[] { BinaryOperator.Addition, BinaryOperator.Subtraction },
                        MinOperandValue = 1,
                        MaxOperandValue = paramRng.Next(5, 30),
                        MaxResultValue = 1_000_000,
                        MinOperandCount = 2,
                        MaxOperandCount = paramRng.Next(2, 4),
                        IncludeUnknown = true,
                        UnknownPlacement = UnknownPlacement.EitherSide,
                        MaxAttempts = 300,
                    };

                default:
                    throw new ArgumentOutOfRangeException(nameof(category), category, "Ismeretlen fuzz-kategória.");
            }
        }
    }
}
