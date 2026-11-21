// GameplayHudModelTests.cs
// F1-15 — proves GameplayHudModel (math-horde.md 3.2: élet/lőszer/pajzs/
// hullám/pálya mindig látható) reads resource numbers only through
// IResourceService (never mutates it) and resolves every player-facing
// string through ILocalizationService (ADR-007: no Hungarian literal in
// code). Lives under Tests/PlayMode for the same assembly-reference reason
// as ObjectPoolTests.cs — GameplayHudModel is in the Presentation assembly.
using System.Collections.Generic;
using NUnit.Framework;
using MathHorde.Core.Common;
using MathHorde.Core.Resources;
using MathHorde.Presentation.Hud;
using MathHorde.Tests.PlayMode.TestSupport;

namespace MathHorde.Tests.PlayMode.Hud
{
    [TestFixture]
    public class GameplayHudModelTests
    {
        private static DictionaryLocalizationService BuildLocalization()
        {
            var entries = new Dictionary<string, string>
            {
                ["hud.health"] = "Életerő",
                ["hud.ammo"] = "Lőszer",
                ["hud.shield"] = "Pajzs",
                ["hud.wave"] = "{0}. hullám",
                ["hud.level"] = "{0}. pálya",
                ["hud.pause"] = "Szünet",
            };
            return new DictionaryLocalizationService(entries);
        }

        [Test]
        public void Initialize_ReadsCurrentAndMax_ForEachResourceType_FromTheResourceService()
        {
            var resourceService = new FakeResourceService();
            resourceService.SetPool(ResourceType.Health, current: 80, max: 100);
            resourceService.SetPool(ResourceType.Shield, current: 0, max: 50);

            var model = new GameplayHudModel(BuildLocalization());
            model.Initialize(resourceService);

            Assert.That(model.Health, Is.EqualTo(80));
            Assert.That(model.MaxHealth, Is.EqualTo(100));
            Assert.That(typeof(GameplayHudModel).GetProperty("Ammo"), Is.Null);
            Assert.That(typeof(GameplayHudModel).GetProperty("MaxAmmo"), Is.Null);
            Assert.That(model.Shield, Is.EqualTo(0));
            Assert.That(model.MaxShield, Is.EqualTo(50));
        }

        [Test]
        public void ApplyResourceChanged_OnlyRefreshesTheChangedResourceType()
        {
            var resourceService = new FakeResourceService();
            resourceService.SetPool(ResourceType.Health, current: 100, max: 100);
            resourceService.SetPool(ResourceType.Shield, current: 60, max: 200);

            var model = new GameplayHudModel(BuildLocalization());
            model.Initialize(resourceService);

            // Simulate the real service applying a WEAPON_FIRE delta between
            // the event firing and the HUD reacting to it.
            resourceService.SetPool(ResourceType.Shield, current: 59, max: 200);
            model.ApplyResourceChanged(resourceService, ResourceType.Shield);

            Assert.That(model.Shield, Is.EqualTo(59));
            Assert.That(model.Health, Is.EqualTo(100), "a Health-en kívüli esemény nem módosíthatja a Health kijelzést.");
        }

        [Test]
        public void HasShield_IsFalse_WhenMaxShieldIsZero_NoShieldPoolOnThisLevel()
        {
            var resourceService = new FakeResourceService();
            resourceService.SetPool(ResourceType.Shield, current: 0, max: 0);

            var model = new GameplayHudModel(BuildLocalization());
            model.Initialize(resourceService);

            Assert.That(model.HasShield, Is.False);
        }

        [Test]
        public void HasShield_IsTrue_WhenTheLevelHasAShieldPool()
        {
            var resourceService = new FakeResourceService();
            resourceService.SetPool(ResourceType.Shield, current: 10, max: 50);

            var model = new GameplayHudModel(BuildLocalization());
            model.Initialize(resourceService);

            Assert.That(model.HasShield, Is.True);
        }

        [Test]
        public void Labels_AndFormattedValues_ComeFromTheLocalizationTable_NotFromHardcodedText()
        {
            var resourceService = new FakeResourceService();
            resourceService.SetPool(ResourceType.Health, current: 7, max: 10);

            var model = new GameplayHudModel(BuildLocalization());
            model.Initialize(resourceService);

            Assert.That(model.HealthLabel, Is.EqualTo("Életerő"));
            Assert.That(typeof(GameplayHudModel).GetProperty("AmmoLabel"), Is.Null);
            Assert.That(model.ShieldLabel, Is.EqualTo("Pajzs"));
            Assert.That(model.PauseLabel, Is.EqualTo("Szünet"));
            Assert.That(model.HealthValueText, Is.EqualTo("7/10"));
        }

        [Test]
        public void WaveText_And_LevelText_FormatThroughTheLocalizationTemplate()
        {
            var model = new GameplayHudModel(BuildLocalization());
            model.ApplyWaveStarted(3);
            model.SetLevelNumber(5);

            Assert.That(model.WaveText, Is.EqualTo("3. hullám"));
            Assert.That(model.LevelText, Is.EqualTo("5. pálya"));
        }

        [Test]
        public void MissingLocalizationKey_RendersAsVisiblePlaceholder_NotBlankOrCrash()
        {
            var model = new GameplayHudModel(new DictionaryLocalizationService(new Dictionary<string, string>()));

            Assert.That(model.HealthLabel, Is.EqualTo("[[hud.health]]"));
        }

        [Test]
        public void Constructor_RejectsNullLocalizationService()
        {
            Assert.That(() => new GameplayHudModel(null), Throws.TypeOf<System.ArgumentNullException>());
        }
    }
}
