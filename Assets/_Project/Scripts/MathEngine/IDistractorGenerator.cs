using System.Collections.Generic;
using MathHorde.Core.Common;

namespace MathHorde.MathEngine
{
    /// <summary>
    /// Produces plausible wrong answers from the 9 typical error models plus
    /// a bounded fallback (contract math-engine.md 4., spec 6.4, task F2-07).
    /// See <see cref="DistractorGenerator"/> for the concrete implementation
    /// and its design-note comments.
    /// </summary>
    public interface IDistractorGenerator
    {
        /// <summary>
        /// Generates <see cref="DistractorRequest.Count"/> distinct distractors.
        /// The returned list's every element differs from every other element
        /// AND from <see cref="DistractorRequest.CorrectAnswer"/>.
        ///
        /// Error codes: "DISTRACTOR_EXHAUSTED" — every applicable error model
        /// AND the correct±k fallback failed to produce
        /// <see cref="DistractorRequest.Count"/> distinct, in-range values.
        /// Only realistically reachable with a pathologically narrow
        /// [MinValue, MaxValue] window (not a documented contract error code
        /// — math-engine.md 4. names no explicit "Hibakódok" list for this
        /// interface the way section 1/2 do for the evaluator/solver; this
        /// name follows the same short, machine-readable style and is
        /// flagged for the architect/contract owner rather than invented
        /// silently).
        /// </summary>
        Result<IReadOnlyList<Distractor>> Generate(DistractorRequest request);
    }
}
