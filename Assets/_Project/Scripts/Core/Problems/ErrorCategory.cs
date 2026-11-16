namespace MathHorde.Core.Problems
{
    /// <summary>
    /// Contract core-domain.md 3. Introduced ahead of the rest of the
    /// Problems package (DistractorGenerator's MathEngine-side consumer)
    /// because QuestionAttempt (Core.Progress, spec 15., save-data-engineer
    /// scope) needs it as its ErrorCategory field type — see SkillTag.cs for
    /// the same pattern. "Timeout" doubles as both a distractor error model
    /// (none, here — it is attempt-log only, per the contract's own
    /// "csak attempt-naplóban" note) and the "no answer given" marker on a
    /// QuestionAttempt.
    ///
    /// Explicit numeric values per the enum rule (core-domain.md 0.) — a
    /// persisted ErrorCategory is stored as its int value in every save/
    /// export file. New values go ONLY at the end, with the next free
    /// number.
    /// </summary>
    public enum ErrorCategory
    {
        None = 0,
        NeighbourInTimesTable = 1,
        OnesDigitError = 2,
        TensDigitError = 3,
        CarryError = 4,
        OperatorPrecedenceError = 5,
        IgnoredParentheses = 6,
        RemainderMisread = 7,
        SignError = 8,
        ReverseCalculationError = 9,
        /// <summary>No answer was given before the think-time elapsed. Only ever appears in an attempt log, never as a distractor error model.</summary>
        Timeout = 10,
        Unclassified = 11
    }
}
