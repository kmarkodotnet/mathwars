using System;
using MathHorde.Core.Problems;

namespace MathHorde.MathEngine
{
    /// <summary>
    /// One generated wrong answer (contract math-engine.md 4.) — an
    /// immutable, already-computed result (plain constructor, no
    /// <c>init</c>, mirroring <c>EvaluationResult</c>/<c>GeneratedExpression</c>'s
    /// "already computed, not a candidate" style rather than
    /// <see cref="DistractorRequest"/>'s caller-assembled style).
    /// </summary>
    public sealed class Distractor
    {
        public EvaluationResult Value { get; }

        public ErrorCategory ErrorModel { get; }

        /// <summary>
        /// Rövid, gépi magyarázat-kulcs a jövőbeli string-táblához (pl.
        /// "distractor.carry") — soha nem tényleges magyar szöveg (ADR-007);
        /// a tényleges fordítás az Infrastructure/Presentation réteg dolga.
        /// </summary>
        public string ExplanationKey { get; }

        public Distractor(EvaluationResult value, ErrorCategory errorModel, string explanationKey)
        {
            if (string.IsNullOrEmpty(explanationKey))
            {
                throw new ArgumentException("ExplanationKey nem lehet üres vagy null.", nameof(explanationKey));
            }

            Value = value;
            ErrorModel = errorModel;
            ExplanationKey = explanationKey;
        }
    }
}
