// CombatBalanceSOTests.cs
// F1-09 — contract scriptable-object-schema.md 7. The Table-vs-Step
// double-scaling warning is NOT tested here (CombatDifficultyTableSO does not
// exist yet in this task's scope — see CombatBalanceSO.cs's header comment).
using System.Collections.Generic;
using MathHorde.Infrastructure.Configuration;
using MathHorde.Tests.PlayMode.TestSupport;
using NUnit.Framework;
using UnityEngine;

namespace MathHorde.Tests.PlayMode.Configuration
{
    [TestFixture]
    public class CombatBalanceSOTests
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
            var so = ScriptableObject.CreateInstance<CombatBalanceSO>();
            _created.Add(so);

            IReadOnlyList<string> errors = so.Validate();

            Assert.That(errors.Count, Is.EqualTo(0));
        }

        [Test]
        public void Validate_EnemySpeedMultiplierAboveRange_ReportsRangeError()
        {
            var so = ScriptableObject.CreateInstance<CombatBalanceSO>();
            _created.Add(so);
            ConfigTestReflection.SetField(so, "_enemySpeedMultiplier", 3.0f);

            IReadOnlyList<string> errors = so.Validate();

            Assert.That(ConfigTestReflection.AnyContains(errors, "EnemySpeedMultiplier"), Is.True);
        }

        [Test]
        public void Validate_MaxActiveEnemiesBelowRange_ReportsRangeError()
        {
            var so = ScriptableObject.CreateInstance<CombatBalanceSO>();
            _created.Add(so);
            ConfigTestReflection.SetField(so, "_maxActiveEnemies", 5);

            IReadOnlyList<string> errors = so.Validate();

            Assert.That(ConfigTestReflection.AnyContains(errors, "MaxActiveEnemies"), Is.True);
        }

        [Test]
        public void ShieldAbsorbsBeforeHealth_DefaultsToTrue()
        {
            var so = ScriptableObject.CreateInstance<CombatBalanceSO>();
            _created.Add(so);

            Assert.That(so.ShieldAbsorbsBeforeHealth, Is.True);
        }
    }
}
