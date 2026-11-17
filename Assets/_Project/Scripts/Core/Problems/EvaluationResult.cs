using System;
using System.Globalization;

namespace MathHorde.Core.Problems
{
    /// <summary>
    /// The result of evaluating a math expression tree (contract
    /// core-domain.md 3., spec 6.2). <see cref="Remainder"/> is set only for
    /// <see cref="MathHorde.Core.Expressions.BinaryOperator.IntegerDivisionWithRemainder"/>
    /// — every other operator chain yields a plain scalar.
    ///
    /// CONTRACT/ADR-007 NOTE: <see cref="ToDisplayString"/> literally returns
    /// "7 maradék 1" per core-domain.md 3.'s own documented example — a
    /// Hungarian word baked directly into a Core type, which on its face
    /// conflicts with ADR-007 ("magyar szöveg soha nincs C# kódba égetve").
    /// This is the same situation F2-01 already accepted for
    /// ExpressionNode.ToDisplayString's Unicode operator glyphs: a Core
    /// domain type has no access to ILocalizationService (that is an
    /// Infrastructure/Presentation concept), and the contract spells out the
    /// exact literal string, so it is followed as written rather than
    /// silently re-interpreted. Flagged here for the architect/ADR-007 owner
    /// rather than resolved unilaterally — see the F2-02 closing report.
    /// </summary>
    public readonly struct EvaluationResult : IEquatable<EvaluationResult>
    {
        public int Value { get; }

        /// <summary>Non-null only for an IntegerDivisionWithRemainder result; always &gt;= 0 when present.</summary>
        public int? Remainder { get; }

        public EvaluationResult(int value, int? remainder = null)
        {
            if (remainder.HasValue && remainder.Value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(remainder), remainder.Value, "A maradék nem lehet negatív.");
            }

            Value = value;
            Remainder = remainder;
        }

        /// <summary>"7" for a plain result, "7 maradék 1" when a remainder is present. Invariant-culture number formatting (ADR-007).</summary>
        public string ToDisplayString()
        {
            return Remainder.HasValue
                ? string.Format(CultureInfo.InvariantCulture, "{0} maradék {1}", Value, Remainder.Value)
                : Value.ToString(CultureInfo.InvariantCulture);
        }

        public bool Equals(EvaluationResult other) => Value == other.Value && Remainder == other.Remainder;

        public override bool Equals(object obj) => obj is EvaluationResult other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Value;
                hash = (hash * 397) ^ (Remainder ?? -1);
                return hash;
            }
        }

        public static bool operator ==(EvaluationResult left, EvaluationResult right) => left.Equals(right);

        public static bool operator !=(EvaluationResult left, EvaluationResult right) => !left.Equals(right);

        public override string ToString() => ToDisplayString();
    }
}
