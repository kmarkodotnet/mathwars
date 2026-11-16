// WeaponConfigSOTests.cs
// F1-09 — contract scriptable-object-schema.md 2. The DPS-vs-GameBalanceSO
// warning described under section 2 is NOT tested here (GameBalanceSO does not
// exist yet in this task's scope — see WeaponConfigSO.cs's header comment).
using System.Collections.Generic;
using MathHorde.Infrastructure.Configuration;
using MathHorde.Tests.PlayMode.TestSupport;
using NUnit.Framework;
using UnityEngine;

namespace MathHorde.Tests.PlayMode.Configuration
{
    [TestFixture]
    public class WeaponConfigSOTests
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
        public void Validate_DefaultHitscanDisabled_ProjectilePrefabMissing_ReportsError()
        {
            var so = ScriptableObject.CreateInstance<WeaponConfigSO>();
            _created.Add(so);

            // Default ProjectileSpeed (40) > 0 and ProjectilePrefab is unassigned.
            IReadOnlyList<string> errors = so.Validate();

            Assert.That(ConfigTestReflection.AnyContains(errors, "ProjectilePrefab"), Is.True);
        }

        [Test]
        public void Validate_HitscanWeapon_DoesNotRequireProjectilePrefab()
        {
            var so = ScriptableObject.CreateInstance<WeaponConfigSO>();
            _created.Add(so);
            ConfigTestReflection.SetField(so, "_projectileSpeed", 0f);

            IReadOnlyList<string> errors = so.Validate();

            Assert.That(ConfigTestReflection.AnyContains(errors, "ProjectilePrefab"), Is.False);
        }

        [Test]
        public void Validate_FullyAssignedProjectileWeapon_ReturnsNoErrors()
        {
            var so = ScriptableObject.CreateInstance<WeaponConfigSO>();
            _created.Add(so);
            var prefab = new GameObject("dummy_projectile_prefab");
            _created.Add(prefab);
            ConfigTestReflection.SetField(so, "_projectilePrefab", prefab);

            IReadOnlyList<string> errors = so.Validate();

            Assert.That(errors.Count, Is.EqualTo(0));
        }

        [Test]
        public void Validate_DamageBelowRange_ReportsRangeError()
        {
            var so = ScriptableObject.CreateInstance<WeaponConfigSO>();
            _created.Add(so);
            ConfigTestReflection.SetField(so, "_damage", 0);

            IReadOnlyList<string> errors = so.Validate();

            Assert.That(ConfigTestReflection.AnyContains(errors, "Damage"), Is.True);
        }
    }
}
