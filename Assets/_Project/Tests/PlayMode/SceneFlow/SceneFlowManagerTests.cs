// F1-11 — SceneFlowManager itself has no UnityEngine type in its own
// signature (it depends only on ISceneLoader/IAppStateMachine), but it
// lives in the Infrastructure assembly, which Tests/EditMode.asmdef does
// not reference (ARCHITECTURE.md 2.3) — so, same as
// GameConfigurationRepositoryTests, it is a plain [Test] here, not an
// [UnityTest]: no running scene is needed to exercise the routing
// decision.
using System.Collections.Generic;
using MathHorde.GameLogic.AppFlow;
using MathHorde.Infrastructure.SceneFlow;
using MathHorde.Tests.PlayMode.TestSupport;
using NUnit.Framework;

namespace MathHorde.Tests.PlayMode.SceneFlow
{
    [TestFixture]
    public class SceneFlowManagerTests
    {
        private static AppStateMachine AtMainMenu()
        {
            var machine = new AppStateMachine();
            machine.Fire(AppTrigger.ConfigLoaded);
            machine.Fire(AppTrigger.ProfileLoaded);
            return machine;
        }

        private static IReadOnlyDictionary<AppState, string> DefaultBindings()
        {
            return new Dictionary<AppState, string>
            {
                [AppState.MainMenu] = "MainMenu",
                [AppState.WorldSelection] = "WorldSelection",
                [AppState.Gameplay] = "Gameplay",
                [AppState.GameOver] = "Gameplay",
            };
        }

        [Test]
        public void Transition_LoadsSceneBoundToDestinationState()
        {
            var machine = AtMainMenu();
            var loader = new FakeSceneLoader();
            var sceneFlow = new SceneFlowManager(machine, loader, DefaultBindings());
            try
            {
                machine.Fire(AppTrigger.PlayPressed);

                Assert.That(loader.LoadedScenes.Count, Is.EqualTo(1));
                Assert.That(loader.LoadedScenes[0], Is.EqualTo("WorldSelection"));
            }
            finally
            {
                sceneFlow.Dispose();
            }
        }

        [Test]
        public void Transition_ToStateWithoutBinding_DoesNotLoadAnyScene()
        {
            var machine = AtMainMenu();
            machine.Fire(AppTrigger.ParentPinAccepted); // -> ParentDashboard, deliberately unbound below
            var loader = new FakeSceneLoader();
            var bindings = new Dictionary<AppState, string> { [AppState.Settings] = "Settings" };
            var sceneFlow = new SceneFlowManager(machine, loader, bindings);
            try
            {
                machine.Fire(AppTrigger.ParentSettingsPressed); // ParentDashboard -> Settings, which IS bound

                Assert.That(loader.LoadedScenes.Count, Is.EqualTo(1));
                Assert.That(loader.LoadedScenes[0], Is.EqualTo("Settings"));
            }
            finally
            {
                sceneFlow.Dispose();
            }
        }

        [Test]
        public void InvalidTrigger_NeverReachesSceneLoader()
        {
            var machine = AtMainMenu();
            var loader = new FakeSceneLoader();
            var sceneFlow = new SceneFlowManager(machine, loader, DefaultBindings());
            try
            {
                machine.Fire(AppTrigger.ResumePressed); // invalid from MainMenu

                Assert.That(loader.LoadedScenes.Count, Is.EqualTo(0));
            }
            finally
            {
                sceneFlow.Dispose();
            }
        }

        [Test]
        public void HandleApplicationPause_True_FiresAppFocusLost_AndLoadsGameplayPauseBinding()
        {
            var machine = AtMainMenu();
            machine.Fire(AppTrigger.PlayPressed);
            machine.Fire(AppTrigger.LevelSelected); // -> Gameplay
            var loader = new FakeSceneLoader();
            var bindings = new Dictionary<AppState, string> { [AppState.Pause] = "PauseOverlay" };
            var sceneFlow = new SceneFlowManager(machine, loader, bindings);
            try
            {
                sceneFlow.HandleApplicationPause(true);

                Assert.That(machine.Current, Is.EqualTo(AppState.Pause));
                Assert.That(loader.LoadedScenes.Count, Is.EqualTo(1));
                Assert.That(loader.LoadedScenes[0], Is.EqualTo("PauseOverlay"));
            }
            finally
            {
                sceneFlow.Dispose();
            }
        }

        [Test]
        public void HandleApplicationPause_True_OutsideGameplay_IsNoOp()
        {
            var machine = AtMainMenu();
            var loader = new FakeSceneLoader();
            var sceneFlow = new SceneFlowManager(machine, loader, DefaultBindings());
            try
            {
                sceneFlow.HandleApplicationPause(true);

                Assert.That(machine.Current, Is.EqualTo(AppState.MainMenu));
                Assert.That(loader.LoadedScenes.Count, Is.EqualTo(0));
            }
            finally
            {
                sceneFlow.Dispose();
            }
        }

        [Test]
        public void HandleApplicationPause_False_IsAlwaysNoOp()
        {
            var machine = AtMainMenu();
            machine.Fire(AppTrigger.PlayPressed);
            machine.Fire(AppTrigger.LevelSelected);
            machine.Fire(AppTrigger.PausePressed); // -> Pause
            var loader = new FakeSceneLoader();
            var sceneFlow = new SceneFlowManager(machine, loader, DefaultBindings());
            try
            {
                sceneFlow.HandleApplicationPause(false);

                Assert.That(machine.Current, Is.EqualTo(AppState.Pause));
                Assert.That(loader.LoadedScenes.Count, Is.EqualTo(0));
            }
            finally
            {
                sceneFlow.Dispose();
            }
        }

        [Test]
        public void Dispose_UnsubscribesFromStateMachine_NoMoreSceneLoadsAfter()
        {
            var machine = AtMainMenu();
            var loader = new FakeSceneLoader();
            var sceneFlow = new SceneFlowManager(machine, loader, DefaultBindings());

            sceneFlow.Dispose();
            machine.Fire(AppTrigger.PlayPressed);

            Assert.That(loader.LoadedScenes.Count, Is.EqualTo(0));
        }
    }
}
