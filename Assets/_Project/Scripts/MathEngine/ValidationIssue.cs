using System;

namespace MathHorde.MathEngine
{
    public sealed class ValidationIssue
    {
        public string Code { get; }
        public string Message { get; }
        public ValidationSeverity Severity { get; }

        public ValidationIssue(string code, string message, ValidationSeverity severity)
        {
            Code = code ?? throw new ArgumentNullException(nameof(code));
            Message = message ?? throw new ArgumentNullException(nameof(message));
            Severity = severity;
        }
    }
}
