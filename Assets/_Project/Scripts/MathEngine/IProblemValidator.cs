using MathHorde.Core.Problems;
using MathHorde.Core.Skills;

namespace MathHorde.MathEngine
{
    public interface IProblemValidator
    {
        ValidationReport Validate(MathProblem problem, DifficultyProfile profile);
        bool IsValid(MathProblem problem, DifficultyProfile profile);
    }
}
