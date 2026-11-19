using System;
using System.Collections.Generic;

namespace MathHorde.MathEngine
{
    public sealed class ValidationReport
    {
        public bool IsValid { get; }
        public IReadOnlyList<ValidationIssue> Issues { get; }

        public ValidationReport(IReadOnlyList<ValidationIssue> issues)
        {
            if (issues == null) throw new ArgumentNullException(nameof(issues));
            var copy = new ValidationIssue[issues.Count];
            bool valid = true;
            for (int i = 0; i < copy.Length; i++)
            {
                copy[i] = issues[i] ?? throw new ArgumentException("Issues cannot contain null.", nameof(issues));
                if (copy[i].Severity == ValidationSeverity.Error) valid = false;
            }

            Issues = Array.AsReadOnly(copy);
            IsValid = valid;
        }
    }
}
