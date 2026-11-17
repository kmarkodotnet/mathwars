using System;
using MathHorde.Core.Problems;
using NUnit.Framework;

namespace MathHorde.Tests.EditMode.Problems
{
    // EvaluationResult (MathHorde.Core.Problems, contract core-domain.md 3.).

    [TestFixture]
    public class EvaluationResultTests
    {
        [Test]
        public void ToDisplayString_NoRemainder_ReturnsPlainNumber()
        {
            var result = new EvaluationResult(7);

            Assert.That(result.ToDisplayString(), Is.EqualTo("7"));
        }

        [Test]
        public void ToDisplayString_NegativeValue_NoRemainder_IncludesSign()
        {
            var result = new EvaluationResult(-3);

            Assert.That(result.ToDisplayString(), Is.EqualTo("-3"));
        }

        [Test]
        public void ToDisplayString_WithRemainder_UsesContractFormat()
        {
            // spec 17.1: 29 ÷r 4 = 7 maradék 1
            var result = new EvaluationResult(7, 1);

            Assert.That(result.ToDisplayString(), Is.EqualTo("7 maradék 1"));
        }

        [Test]
        public void Constructor_NegativeRemainder_Throws()
        {
            Assert.That(() => new EvaluationResult(7, -1), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void Equals_SameValueAndRemainder_AreEqual()
        {
            var a = new EvaluationResult(7, 1);
            var b = new EvaluationResult(7, 1);

            Assert.That(a, Is.EqualTo(b));
            Assert.That(a == b, Is.True);
        }

        [Test]
        public void Equals_SameValueDifferentRemainder_AreNotEqual()
        {
            var a = new EvaluationResult(7, 1);
            var b = new EvaluationResult(7);

            Assert.That(a.Equals(b), Is.False);
            Assert.That(a != b, Is.True);
        }

        [Test]
        public void Equals_DifferentValue_AreNotEqual()
        {
            var a = new EvaluationResult(7);
            var b = new EvaluationResult(8);

            Assert.That(a.Equals(b), Is.False);
        }

        [Test]
        public void GetHashCode_EqualInstances_ProduceSameHash()
        {
            var a = new EvaluationResult(29, 1);
            var b = new EvaluationResult(29, 1);

            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
        }
    }
}
