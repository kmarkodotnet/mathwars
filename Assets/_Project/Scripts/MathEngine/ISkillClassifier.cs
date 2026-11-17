using System.Collections.Generic;
using MathHorde.Core.Expressions;
using MathHorde.Core.Problems;
using MathHorde.Core.Skills;

namespace MathHorde.MathEngine
{
    /// <summary>
    /// Skill classification, error categorization and the prerequisite
    /// graph (contract math-engine.md 6., spec 7.2/7.4/8., task F2-06). The
    /// authoritative source for <see cref="MathHorde.Core.Problems.MathProblem.SkillTags"/>
    /// — <see cref="MathHorde.MathEngine.GeneratedExpression.DetectedSkills"/>
    /// is only a best-effort, coarse diagnostic (see its own doc comment);
    /// <c>IMathProblemGenerator</c> (F2-09, contract math-engine.md 7. step
    /// 9) calls <see cref="Classify"/> for the final tags.
    /// </summary>
    public interface ISkillClassifier
    {
        /// <summary>Which skills the expression actually exercises (math-engine.md 6. classification table).</summary>
        IReadOnlyList<SkillTag> Classify(ExpressionNode expression, ProblemType type);

        /// <summary>
        /// A wrong answer's error category from the correct and selected
        /// values. If the selected <c>AnswerOption</c> was a generated
        /// distractor, its own <c>DistractorErrorModel</c> takes priority
        /// over this method's heuristic (math-engine.md 6.) — that
        /// precedence is the CALLER's responsibility (e.g. GameLogic's
        /// answer-processing step), since this method's signature does not
        /// receive the <c>AnswerOption</c>/<c>DistractorErrorModel</c>
        /// itself, only the two <see cref="EvaluationResult"/> values.
        /// </summary>
        ErrorCategory ClassifyError(ExpressionNode expression, EvaluationResult correct, EvaluationResult selected);

        /// <summary>The skill's direct prerequisites (math-engine.md 6. "Előfeltétel-gráf", spec 7.2/7.4) — empty if it has none.</summary>
        IReadOnlyCollection<SkillTag> GetPrerequisites(SkillTag skill);

        /// <summary>Whether the profile's current state allows the generator to offer this skill (math-engine.md 6., ADR-010 rule 5).</summary>
        bool IsEligible(SkillTag skill, SkillProfile profile);
    }
}
