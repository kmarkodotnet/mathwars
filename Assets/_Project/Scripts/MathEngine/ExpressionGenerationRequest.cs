using System;
using System.Collections.Generic;
using MathHorde.Core.Common;
using MathHorde.Core.Expressions;
using MathHorde.Core.Problems;

namespace MathHorde.MathEngine
{
    /// <summary>
    /// Input to <see cref="IExpressionGenerator.Generate"/> (contract
    /// math-engine.md 3.). Every mathematical bound here is expected to
    /// already be derived from a <c>DifficultyProfile</c>/
    /// <c>ProblemGenerationRequest</c> by the caller (P7, math-engine.md
    /// 0.: "Az engine nem olvas konfigurációt") — this type does not
    /// duplicate or reference DifficultyProfile itself.
    ///
    /// DESIGN NOTE (flagged per the unity-csharp skill's "contract-szabály"
    /// — this is a plain INPUT the caller assembles, not a generator
    /// CANDIDATE output like <c>MathProblem</c>, so — unlike MathProblem's
    /// deliberately permissive constructor (core-domain.md 3., I1-I11
    /// deferred to ProblemValidator) — a malformed request here IS treated
    /// as a caller contract violation: <see cref="ExpressionGenerator.Generate"/>
    /// throws <see cref="ArgumentException"/> for the preconditions the
    /// contract spells out inline (e.g. "nem üres", "&gt; MinOperandValue",
    /// "&gt;= 2", "&lt;= 5 (MVP)"), the same way <c>LevelDefinition</c>'s
    /// constructor enforces its own inline-commented single-field bounds.
    /// </summary>
    public sealed class ExpressionGenerationRequest
    {
        /// <summary>Kötelező. Every random draw this generator makes comes from this single source (ADR-006) — no internal Derive() calls (that is MathProblemGenerator's job, math-engine.md 7.).</summary>
        public IRandomSource Random { get; init; }

        /// <summary>Kötelező, nem üres.</summary>
        public IReadOnlyCollection<BinaryOperator> AllowedOperators { get; init; }

        /// <summary>&gt;= 0 alapesetben.</summary>
        public int MinOperandValue { get; init; }

        /// <summary>&gt; MinOperandValue.</summary>
        public int MaxOperandValue { get; init; }

        /// <summary>A VÉGEREDMÉNY felső korlátja (DifficultyProfile-ból, CL10: 999). Operandus-tartománytól független.</summary>
        public int MaxResultValue { get; init; }

        /// <summary>&gt;= 2.</summary>
        public int MinOperandCount { get; init; }

        /// <summary>&lt;= 5 (MVP).</summary>
        public int MaxOperandCount { get; init; }

        public bool AllowParentheses { get; init; }

        /// <summary>0..2. Csak AllowParentheses == true esetén értelmezett.</summary>
        public int MaxParenthesisDepth { get; init; }

        /// <summary>Kényszerített zárójel-feladat.</summary>
        public bool RequireParentheses { get; init; }

        /// <summary>MissingOperand/EquationSolving.</summary>
        public bool IncludeUnknown { get; init; }

        public UnknownPlacement UnknownPlacement { get; init; } = UnknownPlacement.None;

        /// <summary>
        /// Not marked "kötelező" in the contract (unlike Random/
        /// AllowedOperators); defaults to <see cref="EvaluationOptions.Default"/>
        /// so a caller that only cares about the structural generation
        /// rules is not forced to restate the engine's own MVP default.
        /// </summary>
        public EvaluationOptions Evaluation { get; init; } = EvaluationOptions.Default;

        /// <summary>Ha nem üres, a generátor PREFERÁLJA ezeket a készségeket (nem garancia).</summary>
        public IReadOnlyCollection<SkillTag> PreferredSkills { get; init; } = Array.Empty<SkillTag>();

        /// <summary>Alap 64 (contract: "alap 64").</summary>
        public int MaxAttempts { get; init; } = 64;
    }
}
