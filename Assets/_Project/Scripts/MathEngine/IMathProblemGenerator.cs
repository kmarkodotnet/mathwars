using System.Collections.Generic;
using MathHorde.Core.Common;
using MathHorde.Core.Problems;

namespace MathHorde.MathEngine
{
    public interface IMathProblemGenerator
    {
        Result<MathProblem> Generate(ProblemGenerationRequest request);
        IReadOnlyList<Result<MathProblem>> GenerateBatch(ProblemGenerationRequest request, int count);
    }
}
