// ConfigValidation.cs
// F1-09 — small, shared helper so every ScriptableObject under this folder reports
// out-of-range/missing-reference problems the same way (contract
// scriptable-object-schema.md 0.: "Tartomány ... KÖTELEZŐ" + "Validáció" column).
// The [Range]/[Min] attributes only clamp the Inspector slider — a value assigned
// through code, duplication or a future migration can still land outside the
// declared bound, so Validate() re-checks it explicitly.
using System.Collections.Generic;

namespace MathHorde.Infrastructure.Configuration
{
    internal static class ConfigValidation
    {
        public static void RequireNonEmpty(List<string> errors, string fieldName, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                errors.Add($"{fieldName} nem lehet üres.");
            }
        }

        public static void RequireNotNull(List<string> errors, string fieldName, object value)
        {
            if (value == null)
            {
                errors.Add($"{fieldName} nem lehet null.");
            }
        }

        public static void RequireRange(List<string> errors, string fieldName, int value, int min, int max)
        {
            if (value < min || value > max)
            {
                errors.Add($"{fieldName} tartománya [{min}, {max}], de a jelenlegi érték {value}.");
            }
        }

        public static void RequireRange(List<string> errors, string fieldName, float value, float min, float max)
        {
            if (value < min || value > max)
            {
                errors.Add($"{fieldName} tartománya [{min}, {max}], de a jelenlegi érték {value}.");
            }
        }

        public static void RequireCount(List<string> errors, string fieldName, int count, int min, int max)
        {
            if (count < min || count > max)
            {
                errors.Add($"{fieldName} elemszáma [{min}, {max}] kell legyen, de {count}.");
            }
        }
    }
}
