using System;

namespace MathHorde.MathEngine
{
    /// <summary>A mathematical expression and its localized note key.</summary>
    public sealed class ExplanationStep
    {
        public string Expression { get; }
        public string NoteKey { get; }

        public ExplanationStep(string expression, string noteKey)
        {
            Expression = expression ?? throw new ArgumentNullException(nameof(expression));
            NoteKey = noteKey ?? throw new ArgumentNullException(nameof(noteKey));
        }
    }
}
