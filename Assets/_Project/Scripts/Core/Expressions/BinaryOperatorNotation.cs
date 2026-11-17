using System;

namespace MathHorde.Core.Expressions
{
    /// <summary>
    /// Display notation for BinaryOperator (contract core-domain.md 2.
    /// precedence table): the Unicode glyph used by ToDisplayString and the
    /// precedence level that decides when a child needs parentheses.
    ///
    /// Internal — not part of the MathEngine-facing contract surface. Kept as
    /// a small static lookup (rather than inlined switch statements repeated
    /// per node type) so the one precedence table in the contract has one
    /// place to change.
    ///
    /// Precedence levels (lower number = binds tighter):
    ///   1 (strongest) — parentheses (not an operator, handled separately)
    ///   2             — ×, ÷, mod, ÷r (left-associative)
    ///   3             — +, − (left-associative)
    /// </summary>
    internal static class BinaryOperatorNotation
    {
        internal static int GetPrecedence(BinaryOperator op)
        {
            switch (op)
            {
                case BinaryOperator.Addition:
                case BinaryOperator.Subtraction:
                    return 3;
                case BinaryOperator.Multiplication:
                case BinaryOperator.Division:
                case BinaryOperator.Modulo:
                case BinaryOperator.IntegerDivisionWithRemainder:
                    return 2;
                default:
                    throw new ArgumentOutOfRangeException(nameof(op), op, "Ismeretlen BinaryOperator.");
            }
        }

        internal static string GetSymbol(BinaryOperator op)
        {
            switch (op)
            {
                case BinaryOperator.Addition:
                    return "+";
                case BinaryOperator.Subtraction:
                    return "−"; // −
                case BinaryOperator.Multiplication:
                    return "×"; // ×
                case BinaryOperator.Division:
                    return "÷"; // ÷
                case BinaryOperator.Modulo:
                    return "mod";
                case BinaryOperator.IntegerDivisionWithRemainder:
                    return "÷r"; // ÷r
                default:
                    throw new ArgumentOutOfRangeException(nameof(op), op, "Ismeretlen BinaryOperator.");
            }
        }
    }
}
