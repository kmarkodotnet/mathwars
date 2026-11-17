using System;
using MathHorde.Core.Common;
using MathHorde.Core.Expressions;
using MathHorde.Core.Problems;
using NUnit.Framework;

namespace MathHorde.Tests.EditMode.Problems
{
    // AnswerOption (MathHorde.Core.Problems, contract core-domain.md 3.).
    // Only the per-option invariants documented directly on this type are
    // exercised here — the cross-option invariants (I2-I4: exactly one
    // correct, distinct values, 2..4 unique lanes) are the ProblemValidator's
    // job (F2-08), not this type's.

    [TestFixture]
    public class AnswerOptionTests
    {
        [Test]
        public void Constructor_ValidCorrectOption_SetsAllProperties()
        {
            var option = new AnswerOption(
                optionIndex: 0,
                lane: new LaneIndex(1),
                value: new EvaluationResult(56),
                gateOperation: null,
                isCorrect: true,
                distractorErrorModel: null,
                displayLabel: "56");

            Assert.That(option.OptionIndex, Is.EqualTo(0));
            Assert.That(option.Lane, Is.EqualTo(new LaneIndex(1)));
            Assert.That(option.Value, Is.EqualTo(new EvaluationResult(56)));
            Assert.That(option.GateOperation, Is.Null);
            Assert.That(option.IsCorrect, Is.True);
            Assert.That(option.DistractorErrorModel, Is.Null);
            Assert.That(option.DisplayLabel, Is.EqualTo("56"));
        }

        [Test]
        public void Constructor_IncorrectOption_CarriesDistractorErrorModel()
        {
            var option = new AnswerOption(
                optionIndex: 1,
                lane: new LaneIndex(0),
                value: new EvaluationResult(63),
                gateOperation: null,
                isCorrect: false,
                distractorErrorModel: ErrorCategory.NeighbourInTimesTable,
                displayLabel: "63");

            Assert.That(option.IsCorrect, Is.False);
            Assert.That(option.DistractorErrorModel, Is.EqualTo(ErrorCategory.NeighbourInTimesTable));
        }

        [Test]
        public void Constructor_GateOperationOption_StoresExpression()
        {
            var gate = new BinaryOperationNode(BinaryOperator.Multiplication, new UnknownNode(), new ConstantNode(2));

            var option = new AnswerOption(
                optionIndex: 0,
                lane: new LaneIndex(0),
                value: default,
                gateOperation: gate,
                isCorrect: true,
                distractorErrorModel: null,
                displayLabel: "×2");

            Assert.That(option.GateOperation, Is.SameAs(gate));
        }

        [Test]
        public void Constructor_NegativeOptionIndex_Throws()
        {
            Assert.That(
                () => new AnswerOption(-1, new LaneIndex(0), new EvaluationResult(1), null, true, null, "1"),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void Constructor_CorrectOptionWithDistractorErrorModel_Throws()
        {
            Assert.That(
                () => new AnswerOption(0, new LaneIndex(0), new EvaluationResult(1), null, true, ErrorCategory.CarryError, "1"),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void Constructor_NullDisplayLabel_Throws()
        {
            Assert.That(
                () => new AnswerOption(0, new LaneIndex(0), new EvaluationResult(1), null, true, null, null),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void Constructor_EmptyDisplayLabel_Throws()
        {
            Assert.That(
                () => new AnswerOption(0, new LaneIndex(0), new EvaluationResult(1), null, true, null, string.Empty),
                Throws.TypeOf<ArgumentException>());
        }
    }
}
