using System.Collections.Generic;
using MathHorde.Core.Common;
using NUnit.Framework;

namespace MathHorde.Tests.EditMode.Common
{
    // Exercises DictionaryLocalizationService (MathHorde.Core.Common) only —
    // it has zero UnityEngine dependency, so this fixture compiles and runs
    // both under the Unity Test Runner and the headless tests/MathHorde.Tests.sln
    // mirror (ADR-003). The Unity-facing Resources loader
    // (MathHorde.Infrastructure.ResourcesLocalizationService) is deliberately
    // not covered here — it has no logic of its own beyond reading a
    // TextAsset, so it is not reachable from this Unity-independent fixture.
    //
    // Uses the NUnit 4 constraint model (Assert.That) — the classic
    // Assert.AreEqual/IsTrue/... methods are no longer members of
    // NUnit.Framework.Assert in NUnit 4.2 (moved to NUnit.Framework.Legacy.ClassicAssert).
    [TestFixture]
    public class DictionaryLocalizationServiceTests
    {
        private static ILocalizationService CreateService(out List<string> missingKeys)
        {
            missingKeys = new List<string>();
            var entries = new Dictionary<string, string>
            {
                ["hud.health"] = "Életerő",
                ["hud.wave"] = "{0}. hullám",
            };
            return new DictionaryLocalizationService(entries, missingKeys.Add);
        }

        [Test]
        public void Resolve_ExistingKey_ReturnsConfiguredText()
        {
            var service = CreateService(out var missingKeys);

            var result = service.Resolve("hud.health");

            Assert.That(result, Is.EqualTo("Életerő"));
            Assert.That(missingKeys, Is.Empty);
        }

        [Test]
        public void Resolve_MissingKey_ReturnsPlaceholderAndNotifies()
        {
            var service = CreateService(out var missingKeys);

            var result = service.Resolve("hud.doesNotExist");

            Assert.That(result, Is.EqualTo("[[hud.doesNotExist]]"));
            Assert.That(missingKeys, Is.EqualTo(new[] { "hud.doesNotExist" }));
        }

        [Test]
        public void Resolve_WithParameters_SubstitutesPlaceholders()
        {
            var service = CreateService(out var missingKeys);

            var result = service.Resolve("hud.wave", 3);

            Assert.That(result, Is.EqualTo("3. hullám"));
            Assert.That(missingKeys, Is.Empty);
        }

        [Test]
        public void TryResolve_MissingKey_ReturnsFalseWithoutNotifying()
        {
            var service = CreateService(out var missingKeys);

            var found = service.TryResolve("hud.doesNotExist", out var value);

            Assert.That(found, Is.False);
            Assert.That(value, Is.Null);
            Assert.That(missingKeys, Is.Empty);
        }

        [Test]
        public void Resolve_NullKey_ReturnsPlaceholderWithoutThrowing()
        {
            var service = CreateService(out var missingKeys);

            var result = service.Resolve(null);

            Assert.That(result, Is.EqualTo("[[]]"));
        }
    }
}
