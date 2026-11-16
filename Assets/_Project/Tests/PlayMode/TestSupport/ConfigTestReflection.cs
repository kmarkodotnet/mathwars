// ConfigTestReflection.cs
// F1-09 — small shared test helper so Configuration tests can set the private
// [SerializeField] backing fields of the ScriptableObject configs directly
// (those types intentionally expose read-only properties, not public setters,
// because they are editor-authored data, not runtime-mutable state).
using System;
using System.Collections.Generic;
using System.Reflection;

namespace MathHorde.Tests.PlayMode.TestSupport
{
    internal static class ConfigTestReflection
    {
        public static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                throw new ArgumentException($"Field '{fieldName}' not found on {target.GetType()}.", nameof(fieldName));
            }

            field.SetValue(target, value);
        }

        // Deliberately a plain loop, not Has.Some/Has.Member — unity-testing skill
        // (NUnit API compatibility section) flags reflection-based Has.* constraints
        // as behaving differently between NUnit 4 (dotnet test) and the Unity-side
        // NUnit 3.5; a manual scan avoids that trap entirely.
        public static bool AnyContains(IReadOnlyList<string> messages, string substring)
        {
            for (int i = 0; i < messages.Count; i++)
            {
                if (messages[i] != null && messages[i].Contains(substring))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
