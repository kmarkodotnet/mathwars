using System;
using MathHorde.Core.Common;
using NUnit.Framework;

namespace MathHorde.Tests.EditMode.Common
{
    // Exercises Result<T> (MathHorde.Core.Common) only — zero UnityEngine
    // dependency, compiles and runs both under the Unity Test Runner and
    // the headless tests/MathHorde.Tests.sln mirror (ADR-003).
    [TestFixture]
    public class ResultTests
    {
        [Test]
        public void Ok_ExposesSuccessAndValue_NoErrorCode()
        {
            var result = Result<int>.Ok(42);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo(42));
            Assert.That(result.ErrorCode, Is.Null);
        }

        [Test]
        public void Fail_ExposesFailureAndErrorCode()
        {
            var result = Result<int>.Fail("DIVISION_BY_ZERO");

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("DIVISION_BY_ZERO"));
        }

        [Test]
        public void Fail_ReadingValue_ThrowsInsteadOfReturningADefaultValue()
        {
            var result = Result<int>.Fail("DIVISION_BY_ZERO");

            // Reading Value on a failed result must never silently hand
            // back default(T) (0 for int) — that could be mistaken for a
            // real math answer. It must be impossible to read a "valid"
            // value out of a failure.
            Assert.That(() => _ = result.Value, Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void Fail_NullOrEmptyErrorCode_Throws()
        {
            Assert.That(() => Result<int>.Fail(null), Throws.TypeOf<ArgumentException>());
            Assert.That(() => Result<int>.Fail(string.Empty), Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void Ok_WithReferenceType_ReturnsSameInstance()
        {
            var payload = "answer";
            var result = Result<string>.Ok(payload);

            Assert.That(result.Value, Is.SameAs(payload));
        }
    }
}
