using System;
using System.Collections.Generic;
using MathHorde.Core.Expressions;
using MathHorde.Core.Problems;
using MathHorde.Core.Skills;
using MathHorde.MathEngine;
using NUnit.Framework;

namespace MathHorde.Tests.EditMode.MathEngine
{
    // ISkillClassifier/SkillClassifier (contract math-engine.md 6., spec
    // 7.2/7.4/8., task F2-06): Classify's classification table, ClassifyError's
    // heuristics, GetPrerequisites' default graph, IsEligible's gating.
    [TestFixture]
    public class SkillClassifierTests
    {
        private SkillClassifier _classifier;

        [SetUp]
        public void SetUp()
        {
            _classifier = new SkillClassifier();
        }

        private static bool Contains(IReadOnlyCollection<SkillTag> tags, SkillTag tag)
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

        // --- Classify --------------------------------------------------------

        [Test]
        public void Classify_SingleDigitAddition_TagsAdditionAndSingleDigit()
        {
            var expr = new BinaryOperationNode(BinaryOperator.Addition, new ConstantNode(3), new ConstantNode(4));

            var tags = _classifier.Classify(expr, ProblemType.AnswerLaneSelection);

            Assert.That(Contains(tags, SkillTag.Addition), Is.True);
            Assert.That(Contains(tags, SkillTag.SingleDigitAddition), Is.True);
            Assert.That(Contains(tags, SkillTag.TwoDigitAddition), Is.False);
            Assert.That(Contains(tags, SkillTag.CrossingTen), Is.False);
        }

        [Test]
        public void Classify_AdditionCrossingTen_TagsCrossingTen()
        {
            // 37 + 28: ones digits 7 + 8 = 15 >= 10.
            var expr = new BinaryOperationNode(BinaryOperator.Addition, new ConstantNode(37), new ConstantNode(28));

            var tags = _classifier.Classify(expr, ProblemType.AnswerLaneSelection);

            Assert.That(Contains(tags, SkillTag.TwoDigitAddition), Is.True);
            Assert.That(Contains(tags, SkillTag.CrossingTen), Is.True);
        }

        [Test]
        public void Classify_SubtractionWithBorrowing_Detected()
        {
            // 83 - 47: ones digit 3 < 7 => borrowing.
            var expr = new BinaryOperationNode(BinaryOperator.Subtraction, new ConstantNode(83), new ConstantNode(47));

            var tags = _classifier.Classify(expr, ProblemType.AnswerLaneSelection);

            Assert.That(Contains(tags, SkillTag.Subtraction), Is.True);
            Assert.That(Contains(tags, SkillTag.SubtractionWithBorrowing), Is.True);
        }

        [Test]
        public void Classify_SubtractionWithoutBorrowing_NotTagged()
        {
            var expr = new BinaryOperationNode(BinaryOperator.Subtraction, new ConstantNode(48), new ConstantNode(23));

            var tags = _classifier.Classify(expr, ProblemType.AnswerLaneSelection);

            Assert.That(Contains(tags, SkillTag.SubtractionWithBorrowing), Is.False);
        }

        [Test]
        public void Classify_TimesTableMultiplication_TagsBothFactorsTables()
        {
            var expr = new BinaryOperationNode(BinaryOperator.Multiplication, new ConstantNode(7), new ConstantNode(8));

            var tags = _classifier.Classify(expr, ProblemType.AnswerLaneSelection);

            Assert.That(Contains(tags, SkillTag.Multiplication), Is.True);
            Assert.That(Contains(tags, SkillTag.TimesTable7), Is.True);
            Assert.That(Contains(tags, SkillTag.TimesTable8), Is.True);
            Assert.That(Contains(tags, SkillTag.TwoDigitMultiplication), Is.False);
        }

        [Test]
        public void Classify_TwoDigitMultiplication_Detected()
        {
            var expr = new BinaryOperationNode(BinaryOperator.Multiplication, new ConstantNode(12), new ConstantNode(15));

            var tags = _classifier.Classify(expr, ProblemType.AnswerLaneSelection);

            Assert.That(Contains(tags, SkillTag.TwoDigitMultiplication), Is.True);
        }

        [Test]
        public void Classify_IntegerDivisionWithRemainder_TagsDivisionRemainderAndDivisionWithRemainder()
        {
            var expr = new BinaryOperationNode(BinaryOperator.IntegerDivisionWithRemainder, new ConstantNode(29), new ConstantNode(4));

            var tags = _classifier.Classify(expr, ProblemType.AnswerLaneSelection);

            Assert.That(Contains(tags, SkillTag.Division), Is.True);
            Assert.That(Contains(tags, SkillTag.Remainder), Is.True);
            Assert.That(Contains(tags, SkillTag.DivisionWithRemainder), Is.True);
        }

        [Test]
        public void Classify_MultiStepWithTwoPrecedenceTiers_TagsOperatorPrecedence()
        {
            // 8 + 4 x 6 - 7 (hand-built tree; see math-horde.md 17.1 canonical example).
            var inner = new BinaryOperationNode(BinaryOperator.Multiplication, new ConstantNode(4), new ConstantNode(6));
            var addPart = new BinaryOperationNode(BinaryOperator.Addition, new ConstantNode(8), inner);
            var expr = new BinaryOperationNode(BinaryOperator.Subtraction, addPart, new ConstantNode(7));

            var tags = _classifier.Classify(expr, ProblemType.AnswerLaneSelection);

            Assert.That(Contains(tags, SkillTag.OperatorPrecedence), Is.True);
            Assert.That(Contains(tags, SkillTag.MultiStepExpressions), Is.True);
        }

        [Test]
        public void Classify_SinglePrecedenceTier_DoesNotTagOperatorPrecedence()
        {
            var expr = new BinaryOperationNode(BinaryOperator.Addition,
                new BinaryOperationNode(BinaryOperator.Addition, new ConstantNode(1), new ConstantNode(2)),
                new ConstantNode(3));

            var tags = _classifier.Classify(expr, ProblemType.AnswerLaneSelection);

            Assert.That(Contains(tags, SkillTag.OperatorPrecedence), Is.False);
            Assert.That(Contains(tags, SkillTag.MultiStepExpressions), Is.False);
        }

        [Test]
        public void Classify_ForcedParentheses_TagsParentheses()
        {
            var group = new BinaryOperationNode(BinaryOperator.Addition, new ConstantNode(6), new ConstantNode(4), forceParentheses: true);
            var expr = new BinaryOperationNode(BinaryOperator.Multiplication, group, new ConstantNode(3));

            var tags = _classifier.Classify(expr, ProblemType.AnswerLaneSelection);

            Assert.That(Contains(tags, SkillTag.Parentheses), Is.True);
        }

        [Test]
        public void Classify_MissingOperandType_TagsMissingOperand()
        {
            var expr = new BinaryOperationNode(BinaryOperator.Addition, new ConstantNode(7), new UnknownNode());

            var tags = _classifier.Classify(expr, ProblemType.MissingOperand);

            Assert.That(Contains(tags, SkillTag.MissingOperand), Is.True);
        }

        [Test]
        public void Classify_EquationSolving_AlwaysTagsMissingOperandAndMultiStep_EvenForSingleOperator()
        {
            // "7 + ? = 15" — a single Addition, no real precedence mix, but
            // the contract's EquationSolving row always adds both tags.
            var equation = new EqualityNode(
                new BinaryOperationNode(BinaryOperator.Addition, new ConstantNode(7), new UnknownNode()),
                new ConstantNode(15));

            var tags = _classifier.Classify(equation, ProblemType.EquationSolving);

            Assert.That(Contains(tags, SkillTag.MissingOperand), Is.True);
            Assert.That(Contains(tags, SkillTag.MultiStepExpressions), Is.True);
        }

        [Test]
        public void Classify_ResourceOperationComparison_TagsResourceComparison_NotMissingOperand()
        {
            // GateOperation shape per math-engine.md 7.1: BinaryOp(op, Unknown, Constant).
            var gateOperation = new BinaryOperationNode(BinaryOperator.Multiplication, new UnknownNode(), new ConstantNode(2));

            var tags = _classifier.Classify(gateOperation, ProblemType.ResourceOperationComparison);

            Assert.That(Contains(tags, SkillTag.ResourceComparison), Is.True);
            Assert.That(Contains(tags, SkillTag.MissingOperand), Is.False);
        }

        [Test]
        public void Classify_NullExpression_Throws()
        {
            Assert.That(() => _classifier.Classify(null, ProblemType.AnswerLaneSelection), Throws.TypeOf<ArgumentNullException>());
        }

        // --- ClassifyError -----------------------------------------------------

        [Test]
        public void ClassifyError_EqualValues_IsNone()
        {
            var expr = new BinaryOperationNode(BinaryOperator.Addition, new ConstantNode(3), new ConstantNode(4));
            var result = _classifier.ClassifyError(expr, new EvaluationResult(7), new EvaluationResult(7));

            Assert.That(result, Is.EqualTo(ErrorCategory.None));
        }

        [Test]
        public void ClassifyError_RemainderMismatchSameQuotient_IsRemainderMisread()
        {
            var expr = new BinaryOperationNode(BinaryOperator.IntegerDivisionWithRemainder, new ConstantNode(29), new ConstantNode(4));
            var result = _classifier.ClassifyError(expr, new EvaluationResult(7, 1), new EvaluationResult(7, 2));

            Assert.That(result, Is.EqualTo(ErrorCategory.RemainderMisread));
        }

        [Test]
        public void ClassifyError_SignFlipped_IsSignError()
        {
            var expr = new BinaryOperationNode(BinaryOperator.Subtraction, new ConstantNode(3), new ConstantNode(10));
            var result = _classifier.ClassifyError(expr, new EvaluationResult(-7), new EvaluationResult(7));

            Assert.That(result, Is.EqualTo(ErrorCategory.SignError));
        }

        [Test]
        public void ClassifyError_ReverseSubtractionAfterAddingUnknown_IsReverseCalculationError()
        {
            // "7 + ? = 15": correct ? = 15 - 7 = 8. A student who re-applies
            // the equation's own operator (Addition) instead of inverting it
            // computes 15 + 7 = 22 — ReverseCalculationError (see
            // TryReverseCalculationValue's doc comment).
            var equation = new EqualityNode(
                new BinaryOperationNode(BinaryOperator.Addition, new ConstantNode(7), new UnknownNode()),
                new ConstantNode(15));

            var result = _classifier.ClassifyError(equation, new EvaluationResult(8), new EvaluationResult(22));

            Assert.That(result, Is.EqualTo(ErrorCategory.ReverseCalculationError));
        }

        [Test]
        public void ClassifyError_ReverseDivisionAfterUnknownDivisor_IsReverseCalculationError()
        {
            // "20 / ? = 5": correct ? = 20 / 5 = 4. Re-applying Division
            // instead of inverting gives 20 / 4... no — re-applying the
            // SAME operator to (knownSideValue, knownOperand) = (5, 20)
            // gives 5 / 20 = 0 (integer division) — still a well-defined,
            // non-dead mistake value, distinct from the correct answer.
            var equation = new EqualityNode(
                new BinaryOperationNode(BinaryOperator.Division, new ConstantNode(20), new UnknownNode()),
                new ConstantNode(5));

            var result = _classifier.ClassifyError(equation, new EvaluationResult(4), new EvaluationResult(0));

            Assert.That(result, Is.EqualTo(ErrorCategory.ReverseCalculationError));
        }

        [Test]
        public void ClassifyError_SubtractionOperandsSwapped_IsSignErrorNotReverseCalculation()
        {
            // For plain (non-equation) Subtraction, b-a is always -(a-b) —
            // mathematically identical to a sign flip, so SignError (not
            // ReverseCalculationError) is the correct, documented reading.
            var expr = new BinaryOperationNode(BinaryOperator.Subtraction, new ConstantNode(10), new ConstantNode(3));

            var result = _classifier.ClassifyError(expr, new EvaluationResult(7), new EvaluationResult(-7));

            Assert.That(result, Is.EqualTo(ErrorCategory.SignError));
        }

        [Test]
        public void ClassifyError_TimesTableNeighbour_IsNeighbourInTimesTable()
        {
            // 7 x 8 = 56 (correct); 7 x 9 = 63 (neighbour distractor).
            var expr = new BinaryOperationNode(BinaryOperator.Multiplication, new ConstantNode(7), new ConstantNode(8));
            var result = _classifier.ClassifyError(expr, new EvaluationResult(56), new EvaluationResult(63));

            Assert.That(result, Is.EqualTo(ErrorCategory.NeighbourInTimesTable));
        }

        [Test]
        public void ClassifyError_NaiveLeftToRightIgnoringPrecedence_IsOperatorPrecedenceError()
        {
            // 8 + 4 x 6 - 7: correct = 8 + 24 - 7 = 25; naive L2R = ((8+4)x6)-7 = 65.
            var inner = new BinaryOperationNode(BinaryOperator.Multiplication, new ConstantNode(4), new ConstantNode(6));
            var addPart = new BinaryOperationNode(BinaryOperator.Addition, new ConstantNode(8), inner);
            var expr = new BinaryOperationNode(BinaryOperator.Subtraction, addPart, new ConstantNode(7));

            var result = _classifier.ClassifyError(expr, new EvaluationResult(25), new EvaluationResult(65));

            Assert.That(result, Is.EqualTo(ErrorCategory.OperatorPrecedenceError));
        }

        [Test]
        public void ClassifyError_NaiveIgnoringExplicitParens_IsIgnoredParentheses()
        {
            // 3 x (6 + 4): correct = 30; naive L2R ignoring the grouping = (3x6)+4 = 22.
            var group = new BinaryOperationNode(BinaryOperator.Addition, new ConstantNode(6), new ConstantNode(4), forceParentheses: true);
            var expr = new BinaryOperationNode(BinaryOperator.Multiplication, new ConstantNode(3), group);

            var result = _classifier.ClassifyError(expr, new EvaluationResult(30), new EvaluationResult(22));

            Assert.That(result, Is.EqualTo(ErrorCategory.IgnoredParentheses));
        }

        [Test]
        public void ClassifyError_TensDigitMatchOnesDiffer_IsOnesDigitError()
        {
            var expr = new BinaryOperationNode(BinaryOperator.Addition, new ConstantNode(20), new ConstantNode(27));
            var result = _classifier.ClassifyError(expr, new EvaluationResult(47), new EvaluationResult(43));

            Assert.That(result, Is.EqualTo(ErrorCategory.OnesDigitError));
        }

        [Test]
        public void ClassifyError_OnesDigitMatchTensDiffer_IsTensDigitError()
        {
            var expr = new BinaryOperationNode(BinaryOperator.Addition, new ConstantNode(20), new ConstantNode(27));
            var result = _classifier.ClassifyError(expr, new EvaluationResult(47), new EvaluationResult(57));

            Assert.That(result, Is.EqualTo(ErrorCategory.TensDigitError));
        }

        [Test]
        public void ClassifyError_DifferenceIsMultipleOfTen_IsCarryError()
        {
            var expr = new BinaryOperationNode(BinaryOperator.Addition, new ConstantNode(27), new ConstantNode(15));
            var result = _classifier.ClassifyError(expr, new EvaluationResult(42), new EvaluationResult(32));

            Assert.That(result, Is.EqualTo(ErrorCategory.CarryError));
        }

        [Test]
        public void ClassifyError_NullExpression_Throws()
        {
            Assert.That(
                () => _classifier.ClassifyError(null, new EvaluationResult(1), new EvaluationResult(2)),
                Throws.TypeOf<ArgumentNullException>());
        }

        // --- GetPrerequisites --------------------------------------------------

        [Test]
        public void GetPrerequisites_Addition_HasNone()
        {
            Assert.That(_classifier.GetPrerequisites(SkillTag.Addition).Count, Is.EqualTo(0));
        }

        [Test]
        public void GetPrerequisites_TwoDigitAddition_RequiresSingleDigitAddition()
        {
            var prereqs = _classifier.GetPrerequisites(SkillTag.TwoDigitAddition);
            Assert.That(Contains(prereqs, SkillTag.SingleDigitAddition), Is.True);
        }

        [Test]
        public void GetPrerequisites_TwoDigitMultiplication_RequiresAllTimesTables()
        {
            var prereqs = _classifier.GetPrerequisites(SkillTag.TwoDigitMultiplication);
            Assert.That(prereqs.Count, Is.EqualTo(9));
            Assert.That(Contains(prereqs, SkillTag.TimesTable2), Is.True);
            Assert.That(Contains(prereqs, SkillTag.TimesTable10), Is.True);
        }

        [Test]
        public void GetPrerequisites_MissingOperand_RequiresAdditionAndSubtraction()
        {
            var prereqs = _classifier.GetPrerequisites(SkillTag.MissingOperand);
            Assert.That(Contains(prereqs, SkillTag.Addition), Is.True);
            Assert.That(Contains(prereqs, SkillTag.Subtraction), Is.True);
        }

        [Test]
        public void PrerequisiteGraph_IsAcyclic_ForEverySkillTag()
        {
            foreach (SkillTag tag in Enum.GetValues(typeof(SkillTag)))
            {
                Assert.That(IsAcyclicFrom(tag, new HashSet<SkillTag>()), Is.True, "cycle reachable from " + tag);
            }
        }

        private bool IsAcyclicFrom(SkillTag tag, HashSet<SkillTag> visiting)
        {
            if (visiting.Contains(tag))
            {
                return false;
            }

            visiting.Add(tag);
            foreach (var prereq in _classifier.GetPrerequisites(tag))
            {
                if (!IsAcyclicFrom(prereq, visiting))
                {
                    return false;
                }
            }

            visiting.Remove(tag);
            return true;
        }

        // --- IsEligible ----------------------------------------------------

        [Test]
        public void IsEligible_NoPrerequisites_AlwaysTrue()
        {
            var profile = new SkillProfile(new Dictionary<SkillTag, SkillStatistics>(), DateTimeOffset.UtcNow);
            Assert.That(_classifier.IsEligible(SkillTag.Addition, profile), Is.True);
        }

        [Test]
        public void IsEligible_PrerequisiteOnlyIntroduced_DefaultPracticedMinLevel_IsFalse()
        {
            // Addition recorded with Attempts == 0 -> Introduced, below the
            // default PrerequisiteMinLevel (Practiced).
            var profile = new SkillProfile(new Dictionary<SkillTag, SkillStatistics>(), DateTimeOffset.UtcNow);
            Assert.That(_classifier.IsEligible(SkillTag.Subtraction, profile), Is.False);
        }

        [Test]
        public void IsEligible_PrerequisitePracticed_IsTrue()
        {
            var skills = new Dictionary<SkillTag, SkillStatistics>
            {
                [SkillTag.Addition] = new SkillStatistics(
                    SkillTag.Addition, 1, attempts: 6, correctAnswers: 5,
                    averageResponseTimeSeconds: 1f, medianResponseTimeSeconds: 1f,
                    lastPracticedAt: null, masteryEstimate: 0.6f, confidence: 0.1f, typicalErrors: null)
            };
            var profile = new SkillProfile(skills, DateTimeOffset.UtcNow);

            Assert.That(_classifier.IsEligible(SkillTag.Subtraction, profile), Is.True);
        }

        [Test]
        public void IsEligible_CustomPrerequisiteMinLevel_IntroducedIsEnough()
        {
            var lenientClassifier = new SkillClassifier(SkillMasteryLevel.Introduced);
            var profile = new SkillProfile(new Dictionary<SkillTag, SkillStatistics>(), DateTimeOffset.UtcNow);

            Assert.That(lenientClassifier.IsEligible(SkillTag.Subtraction, profile), Is.True);
        }

        [Test]
        public void IsEligible_NullProfile_Throws()
        {
            Assert.That(() => _classifier.IsEligible(SkillTag.Addition, null), Throws.TypeOf<ArgumentNullException>());
        }
    }
}
