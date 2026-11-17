using System;
using System.Collections.Generic;
using MathHorde.Core.Expressions;
using MathHorde.Core.Problems;

namespace MathHorde.MathEngine
{
    /// <summary>
    /// Output of <see cref="IExpressionGenerator.Generate"/> (contract
    /// math-engine.md 3.) — an immutable, already-validated result (plain
    /// constructor, no <c>init</c>, mirroring <c>EvaluationResult</c>'s
    /// "already computed, not a candidate" style rather than
    /// <c>ExpressionGenerationRequest</c>'s caller-assembled style).
    /// </summary>
    public sealed class GeneratedExpression
    {
        /// <summary>EqualityNode, ha IncludeUnknown volt igaz; egyébként a sima kifejezésfa.</summary>
        public ExpressionNode Root { get; }

        /// <summary>
        /// Az ismeretlen nélküli kifejezés kiértékelt eredménye. IncludeUnknown
        /// esetén az egyenlet ISMERT (konstans) oldalának értéke — a tényleges
        /// ismeretlen-megoldást <see cref="UnknownSolution"/> adja, lásd ott.
        /// </summary>
        public EvaluationResult Value { get; }

        /// <summary>Csak IncludeUnknown esetén nem null — az IEquationSolver által verifikált egyetlen megoldás.</summary>
        public int? UnknownSolution { get; }

        /// <summary>
        /// Best-effort, BASIC-only skill tags detected from the tree's own
        /// operator shape (Addition/Subtraction/Multiplication/Division/
        /// Remainder/OperatorPrecedence/Parentheses/MissingOperand only).
        /// The fine-grained classification table (math-engine.md 6., e.g.
        /// TimesTable7/CrossingTen/SubtractionWithBorrowing) is
        /// ISkillClassifier's job (task F2-06, not a dependency of F2-05) —
        /// this field is an internal diagnostic/PreferredSkills-biasing aid,
        /// not the authoritative classification MathProblemGenerator (F2-09,
        /// contract math-engine.md 7. step 9) will use for the final
        /// MathProblem.SkillTags.
        /// </summary>
        public IReadOnlyList<SkillTag> DetectedSkills { get; }

        /// <summary>Hány próbálkozás kellett (>= 1, &lt;= request.MaxAttempts a sikeres esetben).</summary>
        public int AttemptsUsed { get; }

        public GeneratedExpression(
            ExpressionNode root,
            EvaluationResult value,
            int? unknownSolution,
            IReadOnlyList<SkillTag> detectedSkills,
            int attemptsUsed)
        {
            Root = root ?? throw new ArgumentNullException(nameof(root));
            Value = value;
            UnknownSolution = unknownSolution;
            DetectedSkills = detectedSkills ?? throw new ArgumentNullException(nameof(detectedSkills));
            AttemptsUsed = attemptsUsed;
        }
    }
}
