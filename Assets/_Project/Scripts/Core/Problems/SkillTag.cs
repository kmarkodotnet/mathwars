namespace MathHorde.Core.Problems
{
    /// <summary>
    /// Contract core-domain.md 3. (spec 8.). Introduced ahead of the rest of
    /// the Problems package (MathProblem, AnswerOption, EvaluationResult,
    /// RewardSpec — MathEngine/Problems task scope) only because
    /// QuestionAttempt (Core.Progress, spec 15., save-data-engineer scope)
    /// needs it as its SkillTags field's element type — see the
    /// GameSessionId.cs comment for the established pattern of landing one
    /// contracted type ahead of its full package when a different task
    /// depends on it.
    ///
    /// Explicit numeric values per the enum rule (core-domain.md 0.): a
    /// persisted SkillTag is stored as its int value in every save/export
    /// file written by the Infrastructure persistence layer, so reordering
    /// would silently reinterpret existing saves and parent-dashboard
    /// exports. New values go ONLY at the end, with the next free number.
    /// </summary>
    public enum SkillTag
    {
        Addition = 0,
        Subtraction = 1,
        Multiplication = 2,
        Division = 3,
        Remainder = 4,
        OperatorPrecedence = 5,
        Parentheses = 6,
        MissingOperand = 7,
        MultiStepExpressions = 8,
        ResourceComparison = 9,

        // Fine-grained skills (spec 8.)
        SingleDigitAddition = 10,
        TwoDigitAddition = 11,
        CrossingTen = 12,
        SubtractionWithBorrowing = 13,
        TimesTable2 = 14,
        TimesTable3 = 15,
        TimesTable4 = 16,
        TimesTable5 = 17,
        TimesTable6 = 18,
        TimesTable7 = 19,
        TimesTable8 = 20,
        TimesTable9 = 21,
        TimesTable10 = 22,
        TwoDigitMultiplication = 23,
        DivisionWithRemainder = 24
    }
}
