// GameSessionAppFlowBinderTests.cs
// F1-17 — covers the two bindings F1-11's Napló flagged as this task's job
// (factory-docs/contract-findings.md #5): Pause/Resume/Finalize on the
// matching AppState transitions, and the autosave key-event callback on
// AppFocusLost and on LevelComplete/GameOver entry.
using System.Collections.Generic;
using MathHorde.Core.Progress;
using MathHorde.GameLogic;
using MathHorde.GameLogic.AppFlow;
using NUnit.Framework;

namespace MathHorde.Tests.EditMode.GameLogic
{
    [TestFixture]
    public class GameSessionAppFlowBinderTests
    {
        private sealed class RecordingSessionService : IGameSessionService
        {
            public List<string> Calls { get; } = new List<string>();

            public GameSessionId SessionId { get; } = GameSessionId.New();
            public SessionResult Result { get; set; } = SessionResult.Victory;
            public bool IsRunning { get; private set; } = true;
            public IReadOnlyList<MathHorde.Core.Levels.WeaponDefinition> AvailableWeapons => throw new System.NotSupportedException();
            public string CurrentWeaponId => throw new System.NotSupportedException();
            public MathHorde.Core.Common.Result<bool> SelectWeapon(string id) => throw new System.NotSupportedException();
            public FailureReason FailureReason { get; set; } = FailureReason.None;

            public void StartSession(
                MathHorde.Core.Levels.LevelDefinition level,
                IReadOnlyList<MathHorde.Core.Levels.WeaponDefinition> weapons,
                IReadOnlyDictionary<string, MathHorde.Core.Levels.EnemyDefinition> enemyCatalog,
                int seed)
            {
                Calls.Add("StartSession");
            }

            public void Tick(float deltaTime) => Calls.Add("Tick");

            public void Pause() => Calls.Add("Pause");

            public void Resume() => Calls.Add("Resume");

            public GameSessionRecord Finalize(SessionResult result, MathHorde.Core.Progress.FailureReason reason)
            {
                Calls.Add($"Finalize({result},{reason})");
                return null;
            }
        }

        private static (GameSessionAppFlowBinder Binder, AppStateMachine Machine, RecordingSessionService Session, List<string> KeyEvents) CreateSut()
        {
            var machine = new AppStateMachine();
            var session = new RecordingSessionService();
            var keyEvents = new List<string>();
            var binder = new GameSessionAppFlowBinder(machine, session, () => keyEvents.Add("persist"));
            return (binder, machine, session, keyEvents);
        }

        private static AppStateMachine AtGameplay(AppStateMachine machine)
        {
            machine.Fire(AppTrigger.ConfigLoaded);
            machine.Fire(AppTrigger.ProfileLoaded);
            machine.Fire(AppTrigger.PlayPressed);
            machine.Fire(AppTrigger.LevelSelected);
            return machine;
        }

        [Test]
        public void EnteringPause_CallsSessionPause()
        {
            var (_, machine, session, _) = CreateSut();
            AtGameplay(machine);

            machine.Fire(AppTrigger.PausePressed);

            Assert.That(session.Calls.Contains("Pause"), Is.True);
        }

        [Test]
        public void ReturningToGameplayFromPause_CallsSessionResume()
        {
            var (_, machine, session, _) = CreateSut();
            AtGameplay(machine);
            machine.Fire(AppTrigger.PausePressed);

            machine.Fire(AppTrigger.ResumePressed);

            Assert.That(session.Calls.Contains("Resume"), Is.True);
        }

        [Test]
        public void EnteringGameOver_CallsFinalizeExactlyOnceWithSessionResultAndReason()
        {
            var (_, machine, session, _) = CreateSut();
            AtGameplay(machine);
            session.Result = SessionResult.Defeat;
            session.FailureReason = FailureReason.HealthDepleted;

            machine.Fire(AppTrigger.PlayerHealthZero);

            Assert.That(session.Calls.Contains("Finalize(Defeat,HealthDepleted)"), Is.True);
            int finalizeCalls = session.Calls.FindAll(c => c.StartsWith("Finalize")).Count;
            Assert.That(finalizeCalls, Is.EqualTo(1));
        }

        [Test]
        public void EnteringLevelComplete_CallsFinalizeAndPersistsKeyEvent()
        {
            var (_, machine, session, keyEvents) = CreateSut();
            AtGameplay(machine);
            session.Result = SessionResult.Victory;
            session.FailureReason = FailureReason.None;

            machine.Fire(AppTrigger.VictoryConditionMet);

            Assert.That(session.Calls.Contains("Finalize(Victory,None)"), Is.True);
            Assert.That(keyEvents.Count, Is.EqualTo(1));
        }

        [Test]
        public void AppFocusLost_InvokesPersistKeyEvent_EvenThoughItAlsoPauses()
        {
            var (_, machine, session, keyEvents) = CreateSut();
            AtGameplay(machine);

            machine.Fire(AppTrigger.AppFocusLost);

            Assert.That(session.Calls.Contains("Pause"), Is.True);
            Assert.That(keyEvents.Count, Is.EqualTo(1));
        }

        [Test]
        public void UnrelatedTransition_DoesNotTouchSessionOrPersist()
        {
            var (_, machine, session, keyEvents) = CreateSut();

            machine.Fire(AppTrigger.ConfigLoaded);
            machine.Fire(AppTrigger.ProfileLoaded);
            machine.Fire(AppTrigger.SettingsPressed);

            Assert.That(session.Calls, Is.Empty);
            Assert.That(keyEvents, Is.Empty);
        }

        [Test]
        public void Dispose_UnsubscribesFromTransitioned()
        {
            var (binder, machine, session, _) = CreateSut();
            AtGameplay(machine);

            binder.Dispose();
            machine.Fire(AppTrigger.PausePressed);

            Assert.That(session.Calls, Is.Empty);
        }

        [Test]
        public void Constructor_NullArguments_Throw()
        {
            var machine = new AppStateMachine();
            var session = new RecordingSessionService();

            Assert.That(
                () => new GameSessionAppFlowBinder(null, session, () => { }),
                Throws.TypeOf<System.ArgumentNullException>());
            Assert.That(
                () => new GameSessionAppFlowBinder(machine, null, () => { }),
                Throws.TypeOf<System.ArgumentNullException>());
            Assert.That(
                () => new GameSessionAppFlowBinder(machine, session, null),
                Throws.TypeOf<System.ArgumentNullException>());
        }
    }
}
