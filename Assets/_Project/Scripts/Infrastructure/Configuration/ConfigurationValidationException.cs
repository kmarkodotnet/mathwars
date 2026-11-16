// ConfigurationValidationException.cs
// F1-10 — GameConfigurationRepository throws this instead of silently falling
// back to a default when one or more Configuration/ ScriptableObject assets
// fail their own IValidatableConfig.Validate() (contract scriptable-object-
// schema.md 0.) or a repository-level cross-reference check (duplicate id,
// a level referencing a weapon/enemy that is not in the supplied catalog).
// "A fordítás legyen validáló... ne csendes default" (F1-10 task brief).
using System;
using System.Collections.Generic;
using System.Linq;

namespace MathHorde.Infrastructure.Configuration
{
    public sealed class ConfigurationValidationException : Exception
    {
        public IReadOnlyList<string> Errors { get; }

        public ConfigurationValidationException(IReadOnlyList<string> errors)
            : base(BuildMessage(errors))
        {
            Errors = errors ?? Array.Empty<string>();
        }

        private static string BuildMessage(IReadOnlyList<string> errors)
        {
            if (errors == null || errors.Count == 0)
            {
                return "Configuration validation failed.";
            }

            return $"Configuration validation failed with {errors.Count} error(s):{Environment.NewLine}" +
                   string.Join(Environment.NewLine, errors.Select(e => $"  - {e}"));
        }
    }
}
