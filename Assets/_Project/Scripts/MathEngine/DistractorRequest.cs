using System;
using System.Collections.Generic;
using MathHorde.Core.Common;
using MathHorde.Core.Expressions;
using MathHorde.Core.Problems;

namespace MathHorde.MathEngine
{
    /// <summary>
    /// Input to <see cref="IDistractorGenerator.Generate"/> (contract
    /// math-engine.md 4.). Every mathematical bound here is expected to
    /// already be derived from a <c>DifficultyProfile</c>/
    /// <c>ProblemGenerationRequest</c> by the caller (P7, math-engine.md 0.:
    /// "Az engine nem olvas konfigurációt") — this type does not duplicate or
    /// reference DifficultyProfile itself.
    ///
    /// DESIGN NOTE (flagged per the unity-csharp skill's "contract-szabály",
    /// mirroring <see cref="ExpressionGenerationRequest"/>'s precedent): this
    /// is a plain INPUT the caller assembles, not a generator CANDIDATE
    /// output, so a malformed request (null Random/Expression, Count outside
    /// 1..3, MinValue &gt; MaxValue) is a caller contract violation —
    /// <see cref="DistractorGenerator.Generate"/> throws rather than
    /// returning a business <c>Result</c> failure for these.
    /// </summary>
    public sealed class DistractorRequest
    {
        /// <summary>Kötelező. Every random draw this generator makes comes from this single source (ADR-006) — no internal Derive() calls (that is MathProblemGenerator's job, math-engine.md 7.).</summary>
        public IRandomSource Random { get; init; }

        /// <summary>Kötelező. A feladat fája — a disztraktorok ebből a struktúrából (gyökér-operátor, konstans operandusok) derülnek ki.</summary>
        public ExpressionNode Expression { get; init; }

        public EvaluationResult CorrectAnswer { get; init; }

        /// <summary>1..3 (⇒ 2..4 opció).</summary>
        public int Count { get; init; }

        /// <summary>Preferált hibamodellek; üres ⇒ az engine az Expression alakjából választ.</summary>
        public IReadOnlyCollection<ErrorCategory> PreferredModels { get; init; } = Array.Empty<ErrorCategory>();

        public int MinValue { get; init; }

        public int MaxValue { get; init; }

        public bool AllowNegative { get; init; }
    }
}
