using MathHorde.GameLogic.AppFlow;
using NUnit.Framework;

namespace MathHorde.Tests.EditMode.GameLogic
{
    // F1-11 — IAppStateMachine is Unity-free (ARCHITECTURE.md 4.: "az
    // állapotgép döntési logikája legyen Unity-független és tesztelhető"),
    // so its full transition table is covered here instead of PlayMode.
    [TestFixture]
    public class AppStateMachineTests
    {
        [Test]
        public void InitialState_IsBoot()
        {
            var machine = new AppStateMachine();

            Assert.That(machine.Current, Is.EqualTo(AppState.Boot));
        }

        [Test]
        public void Boot_RequiresBothConfigAndProfileLoaded_BeforeMainMenu()
        {
            var machine = new AppStateMachine();

            machine.Fire(AppTrigger.ConfigLoaded);
            Assert.That(machine.Current, Is.EqualTo(AppState.Boot));

            machine.Fire(AppTrigger.ProfileLoaded);
            Assert.That(machine.Current, Is.EqualTo(AppState.MainMenu));
        }

        [Test]
        public void Boot_AcceptsConfigAndProfileLoaded_InEitherOrder()
        {
            var machine = new AppStateMachine();

            machine.Fire(AppTrigger.ProfileLoaded);
            machine.Fire(AppTrigger.ConfigLoaded);

            Assert.That(machine.Current, Is.EqualTo(AppState.MainMenu));
        }

        [Test]
        public void Boot_IgnoresUnrelatedTrigger_AndStaysInBoot()
        {
            var machine = new AppStateMachine();

            bool fired = machine.Fire(AppTrigger.PlayPressed);

            Assert.That(fired, Is.False);
            Assert.That(machine.Current, Is.EqualTo(AppState.Boot));
        }

        private static AppStateMachine AtMainMenu()
        {
            var machine = new AppStateMachine();
            machine.Fire(AppTrigger.ConfigLoaded);
            machine.Fire(AppTrigger.ProfileLoaded);
            return machine;
        }

        [Test]
        public void MainMenu_PlayPressed_GoesToWorldSelection()
        {
            var machine = AtMainMenu();

            bool fired = machine.Fire(AppTrigger.PlayPressed);

            Assert.That(fired, Is.True);
            Assert.That(machine.Current, Is.EqualTo(AppState.WorldSelection));
        }

        [Test]
        public void MainMenu_SettingsPressed_GoesToSettings()
        {
            var machine = AtMainMenu();

            machine.Fire(AppTrigger.SettingsPressed);

            Assert.That(machine.Current, Is.EqualTo(AppState.Settings));
        }

        [Test]
        public void MainMenu_ParentPinAccepted_GoesToParentDashboard()
        {
            var machine = AtMainMenu();

            machine.Fire(AppTrigger.ParentPinAccepted);

            Assert.That(machine.Current, Is.EqualTo(AppState.ParentDashboard));
        }

        [Test]
        public void WorldSelection_BackPressed_ReturnsToMainMenu()
        {
            var machine = AtMainMenu();
            machine.Fire(AppTrigger.PlayPressed);

            machine.Fire(AppTrigger.BackPressed);

            Assert.That(machine.Current, Is.EqualTo(AppState.MainMenu));
        }

        [Test]
        public void WorldSelection_LevelSelected_GoesToGameplay()
        {
            var machine = AtMainMenu();
            machine.Fire(AppTrigger.PlayPressed);

            machine.Fire(AppTrigger.LevelSelected);

            Assert.That(machine.Current, Is.EqualTo(AppState.Gameplay));
        }

        private static AppStateMachine AtGameplay()
        {
            var machine = AtMainMenu();
            machine.Fire(AppTrigger.PlayPressed);
            machine.Fire(AppTrigger.LevelSelected);
            return machine;
        }

        [Test]
        public void Gameplay_PausePressed_GoesToPause()
        {
            var machine = AtGameplay();

            machine.Fire(AppTrigger.PausePressed);

            Assert.That(machine.Current, Is.EqualTo(AppState.Pause));
        }

        [Test]
        public void Gameplay_AppFocusLost_AlsoGoesToPause()
        {
            var machine = AtGameplay();

            machine.Fire(AppTrigger.AppFocusLost);

            Assert.That(machine.Current, Is.EqualTo(AppState.Pause));
        }

        [Test]
        public void Pause_ResumePressed_ReturnsToGameplay()
        {
            var machine = AtGameplay();
            machine.Fire(AppTrigger.PausePressed);

            machine.Fire(AppTrigger.ResumePressed);

            Assert.That(machine.Current, Is.EqualTo(AppState.Gameplay));
        }

        [Test]
        public void Pause_QuitToMenu_GoesToMainMenu()
        {
            var machine = AtGameplay();
            machine.Fire(AppTrigger.PausePressed);

            machine.Fire(AppTrigger.QuitToMenu);

            Assert.That(machine.Current, Is.EqualTo(AppState.MainMenu));
        }

        [Test]
        public void Gameplay_VictoryConditionMet_GoesToLevelComplete()
        {
            var machine = AtGameplay();

            machine.Fire(AppTrigger.VictoryConditionMet);

            Assert.That(machine.Current, Is.EqualTo(AppState.LevelComplete));
        }

        [Test]
        public void Gameplay_PlayerHealthZero_GoesToGameOver()
        {
            var machine = AtGameplay();

            machine.Fire(AppTrigger.PlayerHealthZero);

            Assert.That(machine.Current, Is.EqualTo(AppState.GameOver));
        }

        [Test]
        public void LevelComplete_Continue_GoesToWorldSelection()
        {
            var machine = AtGameplay();
            machine.Fire(AppTrigger.VictoryConditionMet);

            machine.Fire(AppTrigger.Continue);

            Assert.That(machine.Current, Is.EqualTo(AppState.WorldSelection));
        }

        [Test]
        public void LevelComplete_NextLevel_GoesToGameplay()
        {
            var machine = AtGameplay();
            machine.Fire(AppTrigger.VictoryConditionMet);

            machine.Fire(AppTrigger.NextLevel);

            Assert.That(machine.Current, Is.EqualTo(AppState.Gameplay));
        }

        [Test]
        public void GameOver_Retry_GoesToGameplay()
        {
            var machine = AtGameplay();
            machine.Fire(AppTrigger.PlayerHealthZero);

            machine.Fire(AppTrigger.Retry);

            Assert.That(machine.Current, Is.EqualTo(AppState.Gameplay));
        }

        [Test]
        public void GameOver_BackToWorlds_GoesToWorldSelection()
        {
            var machine = AtGameplay();
            machine.Fire(AppTrigger.PlayerHealthZero);

            machine.Fire(AppTrigger.BackToWorlds);

            Assert.That(machine.Current, Is.EqualTo(AppState.WorldSelection));
        }

        [Test]
        public void ParentDashboard_ParentSettingsPressed_GoesToSettings()
        {
            var machine = AtMainMenu();
            machine.Fire(AppTrigger.ParentPinAccepted);

            machine.Fire(AppTrigger.ParentSettingsPressed);

            Assert.That(machine.Current, Is.EqualTo(AppState.Settings));
        }

        [Test]
        public void ParentDashboard_BackPressed_GoesToMainMenu()
        {
            var machine = AtMainMenu();
            machine.Fire(AppTrigger.ParentPinAccepted);

            machine.Fire(AppTrigger.BackPressed);

            Assert.That(machine.Current, Is.EqualTo(AppState.MainMenu));
        }

        [Test]
        public void Settings_BackPressed_GoesToMainMenu()
        {
            var machine = AtMainMenu();
            machine.Fire(AppTrigger.SettingsPressed);

            machine.Fire(AppTrigger.BackPressed);

            Assert.That(machine.Current, Is.EqualTo(AppState.MainMenu));
        }

        [Test]
        public void InvalidTrigger_ForCurrentState_ReturnsFalse_AndDoesNotChangeState()
        {
            var machine = AtMainMenu();

            bool fired = machine.Fire(AppTrigger.ResumePressed);

            Assert.That(fired, Is.False);
            Assert.That(machine.Current, Is.EqualTo(AppState.MainMenu));
        }

        [Test]
        public void InvalidTrigger_DoesNotRaiseTransitioned()
        {
            var machine = AtMainMenu();
            int raisedCount = 0;
            machine.Transitioned += _ => raisedCount++;

            machine.Fire(AppTrigger.ResumePressed);

            Assert.That(raisedCount, Is.EqualTo(0));
        }

        [Test]
        public void CanFire_ReflectsFire_WithoutMutatingState()
        {
            var machine = AtMainMenu();

            bool canPlay = machine.CanFire(AppTrigger.PlayPressed);
            bool canResume = machine.CanFire(AppTrigger.ResumePressed);

            Assert.That(canPlay, Is.True);
            Assert.That(canResume, Is.False);
            Assert.That(machine.Current, Is.EqualTo(AppState.MainMenu));
        }

        [Test]
        public void SuccessfulFire_RaisesTransitioned_WithFromToAndTrigger()
        {
            var machine = AtMainMenu();
            AppStateTransition? captured = null;
            machine.Transitioned += t => captured = t;

            machine.Fire(AppTrigger.PlayPressed);

            Assert.That(captured.HasValue, Is.True);
            Assert.That(captured.Value.From, Is.EqualTo(AppState.MainMenu));
            Assert.That(captured.Value.To, Is.EqualTo(AppState.WorldSelection));
            Assert.That(captured.Value.Trigger, Is.EqualTo(AppTrigger.PlayPressed));
        }
    }
}
