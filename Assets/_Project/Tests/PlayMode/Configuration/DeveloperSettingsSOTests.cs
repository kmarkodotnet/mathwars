// DeveloperSettingsSOTests.cs
// F1-09 — contract scriptable-object-schema.md 12. Also covers the
// ForceNextProblemType nullable-enum-over-Unity-serialization workaround
// documented in DeveloperSettingsSO.cs.
using System.Collections.Generic;
using MathHorde.Core.Problems;
using MathHorde.Infrastructure.Configuration;
using MathHorde.Tests.PlayMode.TestSupport;
using NUnit.Framework;
using UnityEngine;

namespace MathHorde.Tests.PlayMode.Configuration
{
    [TestFixture]
    public class DeveloperSettingsSOTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object obj in _created)
            {
                Object.DestroyImmediate(obj);
            }

            _created.Clear();
        }

        [Test]
        public void Validate_Defaults_ReturnNoErrors()
        {
            var so = ScriptableObject.CreateInstance<DeveloperSettingsSO>();
            _created.Add(so);

            IReadOnlyList<string> errors = so.Validate();

            Assert.That(errors.Count, Is.EqualTo(0));
        }

        [Test]
        public void Validate_ForceChallengeLevelAboveRange_ReportsRangeError()
        {
            var so = ScriptableObject.CreateInstance<DeveloperSettingsSO>();
            _created.Add(so);
            ConfigTestReflection.SetField(so, "_forceChallengeLevel", 11);

            IReadOnlyList<string> errors = so.Validate();

            Assert.That(ConfigTestReflection.AnyContains(errors, "ForceChallengeLevel"), Is.True);
        }

        [Test]
        public void Validate_TimeScaleAboveRange_ReportsRangeError()
        {
            var so = ScriptableObject.CreateInstance<DeveloperSettingsSO>();
            _created.Add(so);
            ConfigTestReflection.SetField(so, "_timeScale", 10f);

            IReadOnlyList<string> errors = so.Validate();

            Assert.That(ConfigTestReflection.AnyContains(errors, "TimeScale"), Is.True);
        }

        [Test]
        public void ForceNextProblemType_DisabledByDefault_IsNull()
        {
            var so = ScriptableObject.CreateInstance<DeveloperSettingsSO>();
            _created.Add(so);

            Assert.That(so.ForceNextProblemType, Is.Null);
        }

        [Test]
        public void ForceNextProblemType_WhenEnabled_ReturnsTheConfiguredValue()
        {
            var so = ScriptableObject.CreateInstance<DeveloperSettingsSO>();
            _created.Add(so);
            ConfigTestReflection.SetField(so, "_forceNextProblemTypeEnabled", true);
            ConfigTestReflection.SetField(so, "_forceNextProblemType", ProblemType.EquationSolving);

            Assert.That(so.ForceNextProblemType, Is.EqualTo(ProblemType.EquationSolving));
        }
    }
}
