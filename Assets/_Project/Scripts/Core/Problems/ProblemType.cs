namespace MathHorde.Core.Problems
{
    /// <summary>
    /// Contract core-domain.md 3. Introduced ahead of the rest of the Problems
    /// package (MathProblem, AnswerOption, ...) only because
    /// LevelDefinition.MathEvents (Core.Levels, F1-02) needs it as the
    /// MathEventScheduleEntry.ProblemType field type. The remaining Problems
    /// types belong to the MathEngine/Problems task.
    /// </summary>
    public enum ProblemType
    {
        ResourceOperationComparison,
        AnswerLaneSelection,
        MissingOperand,
        EquationSolving,
        TargetValueSelection
    }
}
