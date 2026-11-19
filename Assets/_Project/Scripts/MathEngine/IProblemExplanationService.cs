using MathHorde.Core.Problems;

namespace MathHorde.MathEngine
{
    /// <summary>
    /// Optional explanations as localization keys and parameters, without
    /// presentation or combat side effects (contract math-engine.md 8.).
    /// </summary>
    public interface IProblemExplanationService
    {
        ExplanationModel Explain(MathProblem problem, MathAnswer answer);
    }
}
