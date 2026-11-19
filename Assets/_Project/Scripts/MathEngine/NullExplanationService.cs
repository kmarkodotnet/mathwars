using System;
using System.Collections.Generic;
using MathHorde.Core.Problems;

namespace MathHorde.MathEngine
{
    /// <summary>
    /// Phase 2-3 null object required by contract math-engine.md 8.
    /// Detailed explanations are optional until phase 5+. The shared empty
    /// model requires neither a localization lookup nor a per-call allocation.
    /// </summary>
    public sealed class NullExplanationService : IProblemExplanationService
    {
        private static readonly ExplanationModel Empty = new ExplanationModel(
            string.Empty, new Dictionary<string, string>(), Array.Empty<ExplanationStep>());

        public ExplanationModel Explain(MathProblem problem, MathAnswer answer)
        {
            if (problem == null) throw new ArgumentNullException(nameof(problem));
            if (answer == null) throw new ArgumentNullException(nameof(answer));

            return Empty;
        }
    }
}
