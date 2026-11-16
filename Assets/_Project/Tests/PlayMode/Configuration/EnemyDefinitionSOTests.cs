// EnemyDefinitionSOTests.cs
// F1-09 — contract scriptable-object-schema.md 1. Covers the explicit
// "Validáció" rules (Boss/Swarmlet thresholds) plus the generic required-field
// and range checks every config SO shares (ConfigValidation).
using System.Collections.Generic;
using MathHorde.Core.Levels;
using MathHorde.Infrastructure.Configuration;
using MathHorde.Tests.PlayMode.TestSupport;
using NUnit.Framework;
using UnityEngine;

namespace MathHorde.Tests.PlayMode.Configuration
{
    [TestFixture]
    public class EnemyDefinitionSOTests
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

        private EnemyDefinitionSO CreateValid()
        {
            var so = ScriptableObject.CreateInstance<EnemyDefinitionSO>();
            _created.Add(so);

            var prefab = new GameObject("dummy_enemy_prefab");
            _created.Add(prefab);
            ConfigTestReflection.SetField(so, "_prefab", prefab);

            return so;
        }

        [Test]
        public void Validate_WithPrefabAssignedAndBasicDefaults_ReturnsNoErrors()
        {
            EnemyDefinitionSO so = CreateValid();

            IReadOnlyList<string> errors = so.Validate();

            Assert.That(errors.Count, Is.EqualTo(0));
        }

        [Test]
        public void Validate_WithoutPrefab_ReportsMissingPrefab()
        {
            var so = ScriptableObject.CreateInstance<EnemyDefinitionSO>();
            _created.Add(so);

            IReadOnlyList<string> errors = so.Validate();

            Assert.That(ConfigTestReflection.AnyContains(errors, "Prefab"), Is.True);
        }

        [Test]
        public void Validate_BossWithTooLowMaxHealth_ReportsBossHealthRule()
        {
            EnemyDefinitionSO so = CreateValid();
            ConfigTestReflection.SetField(so, "_type", EnemyType.Boss);
            ConfigTestReflection.SetField(so, "_maxHealth", 50);

            IReadOnlyList<string> errors = so.Validate();

            Assert.That(ConfigTestReflection.AnyContains(errors, "Boss"), Is.True);
        }

        [Test]
        public void Validate_BossWithSufficientMaxHealth_ReturnsNoErrors()
        {
            EnemyDefinitionSO so = CreateValid();
            ConfigTestReflection.SetField(so, "_type", EnemyType.Boss);
            ConfigTestReflection.SetField(so, "_maxHealth", 150);

            IReadOnlyList<string> errors = so.Validate();

            Assert.That(errors.Count, Is.EqualTo(0));
        }

        [Test]
        public void Validate_SwarmletWithLowHealthAndGroup_ReturnsNoErrors()
        {
            EnemyDefinitionSO so = CreateValid();
            ConfigTestReflection.SetField(so, "_type", EnemyType.Swarmlet);
            ConfigTestReflection.SetField(so, "_maxHealth", 1);
            ConfigTestReflection.SetField(so, "_spawnGroupSize", 4);

            IReadOnlyList<string> errors = so.Validate();

            Assert.That(errors.Count, Is.EqualTo(0));
        }

        [Test]
        public void Validate_SwarmletWithTooMuchHealth_ReportsSwarmletRule()
        {
            EnemyDefinitionSO so = CreateValid();
            ConfigTestReflection.SetField(so, "_type", EnemyType.Swarmlet);
            ConfigTestReflection.SetField(so, "_maxHealth", 5);
            ConfigTestReflection.SetField(so, "_spawnGroupSize", 4);

            IReadOnlyList<string> errors = so.Validate();

            Assert.That(ConfigTestReflection.AnyContains(errors, "Swarmlet"), Is.True);
        }

        [Test]
        public void Validate_SwarmletWithTooSmallGroup_ReportsSwarmletRule()
        {
            EnemyDefinitionSO so = CreateValid();
            ConfigTestReflection.SetField(so, "_type", EnemyType.Swarmlet);
            ConfigTestReflection.SetField(so, "_maxHealth", 1);
            ConfigTestReflection.SetField(so, "_spawnGroupSize", 1);

            IReadOnlyList<string> errors = so.Validate();

            Assert.That(ConfigTestReflection.AnyContains(errors, "SpawnGroupSize"), Is.True);
        }

        [Test]
        public void Validate_MaxHealthBelowRange_ReportsRangeError()
        {
            EnemyDefinitionSO so = CreateValid();
            ConfigTestReflection.SetField(so, "_maxHealth", 0);

            IReadOnlyList<string> errors = so.Validate();

            Assert.That(ConfigTestReflection.AnyContains(errors, "MaxHealth"), Is.True);
        }
    }
}
