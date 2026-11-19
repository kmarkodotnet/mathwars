using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MathHorde.MathEngine
{
    /// <summary>
    /// Immutable explanation data. Keys are resolved by the presentation
    /// layer; an empty key and empty collections represent no explanation.
    /// </summary>
    public sealed class ExplanationModel
    {
        public string TemplateKey { get; }
        public IReadOnlyDictionary<string, string> Parameters { get; }
        public IReadOnlyList<ExplanationStep> Steps { get; }

        public ExplanationModel(
            string templateKey,
            IReadOnlyDictionary<string, string> parameters,
            IReadOnlyList<ExplanationStep> steps)
        {
            TemplateKey = templateKey ?? throw new ArgumentNullException(nameof(templateKey));
            if (parameters == null) throw new ArgumentNullException(nameof(parameters));
            if (steps == null) throw new ArgumentNullException(nameof(steps));

            var parameterCopy = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var parameter in parameters)
            {
                if (parameter.Value == null)
                    throw new ArgumentException("Parameters cannot contain null values.", nameof(parameters));
                parameterCopy.Add(parameter.Key, parameter.Value);
            }

            var stepCopy = new ExplanationStep[steps.Count];
            for (int i = 0; i < stepCopy.Length; i++)
                stepCopy[i] = steps[i] ?? throw new ArgumentException("Steps cannot contain null.", nameof(steps));

            Parameters = new ReadOnlyDictionary<string, string>(parameterCopy);
            Steps = Array.AsReadOnly(stepCopy);
        }
    }
}
