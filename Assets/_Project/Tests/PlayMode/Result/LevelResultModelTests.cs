// LevelResultModelTests.cs
// F1-16 — proves LevelResultModel (math-horde.md 12./7.1: "a rossz eredmény
// soha nem lehet megalázó") resolves every player-facing string through
// ILocalizationService (ADR-007: no Hungarian literal in code) and pairs
// each button with the exact AppTrigger that AppStateMachine's real
// transition table accepts from that AppState — so a rendered button can
// never be decorative/non-functional (spec 22.).
//
// Lives under Tests/PlayMode for the same assembly-reference reason as
// GameplayHudModelTests.cs — LevelResultModel is in the Presentation
// assembly, which Tests/EditMode (and the dotnet mirror) does not
// reference.
using System.Collections.Generic;
using NUnit.Framework;
using MathHorde.Core.Common;
using MathHorde.GameLogic.AppFlow;
using MathHorde.Presentation.Result;

namespace MathHorde.Tests.PlayMode.Result
{
    [TestFixture]
    public class LevelResultModelTests
    {
        private static DictionaryLocalizationService BuildLocalization()
        {
            var entries = new Dictionary<string, string>
            {
                ["result.gameOver"] = "Játék vége",
                ["result.gameOverEncouragement"] = "Egyre jobban megy! Próbáld meg újra.",
                ["result.victory"] = "Pálya teljesítve!",
                ["result.victoryEncouragement"] = "Szép munka, ügyes voltál!",
                ["result.retry"] = "Újrakezdés",
                ["result.nextLevel"] = "Következő pálya",
                ["result.backToWorlds"] = "Vissza a világokhoz",
            };
            return new DictionaryLocalizationService(entries);
        }

        private static AppStateMachine BuildMachineAt(AppState targetState)
        {
            var machine = new AppStateMachine();
            machine.Fire(AppTrigger.ConfigLoaded);
            machine.Fire(AppTrigger.ProfileLoaded); // Boot -> MainMenu
            machine.Fire(AppTrigger.PlayPressed);   // MainMenu -> WorldSelection
            machine.Fire(AppTrigger.LevelSelected);  // WorldSelection -> Gameplay

            if (targetState == AppState.GameOver)
            {
                machine.Fire(AppTrigger.PlayerHealthZero); // Gameplay -> GameOver
            }
            else if (targetState == AppState.LevelComplete)
            {
                machine.Fire(AppTrigger.VictoryConditionMet); // Gameplay -> LevelComplete
            }

            return machine;
        }

        [Test]
        public void TryBuild_GameOver_ReturnsNonShamingHeadline_AndRetryBackToWorldsButtons()
        {
            var model = new LevelResultModel(BuildLocalization());

            bool built = model.TryBuild(AppState.GameOver, out LevelResultContent content);

            Assert.That(built, Is.True);
            Assert.That(content.Headline, Is.EqualTo("Játék vége"));
            Assert.That(content.Encouragement, Is.EqualTo("Egyre jobban megy! Próbáld meg újra."));
            Assert.That(content.PrimaryLabel, Is.EqualTo("Újrakezdés"));
            Assert.That(content.PrimaryTrigger, Is.EqualTo(AppTrigger.Retry));
            Assert.That(content.SecondaryLabel, Is.EqualTo("Vissza a világokhoz"));
            Assert.That(content.SecondaryTrigger, Is.EqualTo(AppTrigger.BackToWorlds));
        }

        [Test]
        public void TryBuild_LevelComplete_ReturnsVictoryHeadline_AndNextLevelContinueButtons()
        {
            var model = new LevelResultModel(BuildLocalization());

            bool built = model.TryBuild(AppState.LevelComplete, out LevelResultContent content);

            Assert.That(built, Is.True);
            Assert.That(content.Headline, Is.EqualTo("Pálya teljesítve!"));
            Assert.That(content.Encouragement, Is.EqualTo("Szép munka, ügyes voltál!"));
            Assert.That(content.PrimaryLabel, Is.EqualTo("Következő pálya"));
            Assert.That(content.PrimaryTrigger, Is.EqualTo(AppTrigger.NextLevel));
            Assert.That(content.SecondaryLabel, Is.EqualTo("Vissza a világokhoz"));
            Assert.That(content.SecondaryTrigger, Is.EqualTo(AppTrigger.Continue));
        }

        [TestCase(AppState.Boot)]
        [TestCase(AppState.MainMenu)]
        [TestCase(AppState.WorldSelection)]
        [TestCase(AppState.Gameplay)]
        [TestCase(AppState.Pause)]
        [TestCase(AppState.ParentDashboard)]
        [TestCase(AppState.Settings)]
        public void TryBuild_NonResultStates_ReturnsFalse_ScreenMustStayHidden(AppState state)
        {
            var model = new LevelResultModel(BuildLocalization());

            bool built = model.TryBuild(state, out LevelResultContent content);

            Assert.That(built, Is.False);
        }

        [Test]
        public void GameOverButtons_ActuallyFireOnTheRealStateMachine_NeitherIsDecorative()
        {
            var machine = BuildMachineAt(AppState.GameOver);
            var model = new LevelResultModel(BuildLocalization());
            Assert.That(model.TryBuild(machine.Current, out LevelResultContent content), Is.True);

            bool primaryFired = machine.Fire(content.PrimaryTrigger);
            Assert.That(primaryFired, Is.True, "a Retry gombnak ebből az állapotból tényleges átmenetet kell indítania.");
            Assert.That(machine.Current, Is.EqualTo(AppState.Gameplay));

            var machine2 = BuildMachineAt(AppState.GameOver);
            Assert.That(model.TryBuild(machine2.Current, out LevelResultContent content2), Is.True);
            bool secondaryFired = machine2.Fire(content2.SecondaryTrigger);
            Assert.That(secondaryFired, Is.True, "a BackToWorlds gombnak ebből az állapotból tényleges átmenetet kell indítania.");
            Assert.That(machine2.Current, Is.EqualTo(AppState.WorldSelection));
        }

        [Test]
        public void LevelCompleteButtons_ActuallyFireOnTheRealStateMachine_NeitherIsDecorative()
        {
            var machine = BuildMachineAt(AppState.LevelComplete);
            var model = new LevelResultModel(BuildLocalization());
            Assert.That(model.TryBuild(machine.Current, out LevelResultContent content), Is.True);

            bool primaryFired = machine.Fire(content.PrimaryTrigger);
            Assert.That(primaryFired, Is.True, "a NextLevel gombnak ebből az állapotból tényleges átmenetet kell indítania.");
            Assert.That(machine.Current, Is.EqualTo(AppState.Gameplay));

            var machine2 = BuildMachineAt(AppState.LevelComplete);
            Assert.That(model.TryBuild(machine2.Current, out LevelResultContent content2), Is.True);
            bool secondaryFired = machine2.Fire(content2.SecondaryTrigger);
            Assert.That(secondaryFired, Is.True, "a Continue gombnak ebből az állapotból tényleges átmenetet kell indítania.");
            Assert.That(machine2.Current, Is.EqualTo(AppState.WorldSelection));
        }

        [Test]
        public void MissingLocalizationKey_RendersAsVisiblePlaceholder_NotBlankOrCrash()
        {
            var model = new LevelResultModel(new DictionaryLocalizationService(new Dictionary<string, string>()));

            Assert.That(model.TryBuild(AppState.GameOver, out LevelResultContent content), Is.True);
            Assert.That(content.Headline, Is.EqualTo("[[result.gameOver]]"));
        }

        [Test]
        public void Constructor_RejectsNullLocalizationService()
        {
            Assert.That(() => new LevelResultModel(null), Throws.TypeOf<System.ArgumentNullException>());
        }
    }
}
