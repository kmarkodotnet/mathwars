// SessionOutcomeBridgeTests.cs
// F1-17 — proves the SessionEnded -> AppTrigger translation (see the
// production file's header for why this bridge has to live in Presentation,
// not GameLogic). PlayMode, not EditMode: the production type references
// MathHorde.Presentation.Events.IGameEventSource, which lives in the
// Presentation assembly the dotnet mirror/EditMode asmdef do not reference
// (unity-csharp skill, F1-13 "Buktatók" entry).
using MathHorde.Core.Progress;
using MathHorde.GameLogic.AppFlow;
using MathHorde.GameLogic.Events;
using MathHorde.Presentation.AppFlow;
using MathHorde.Presentation.Events;
using NUnit.Framework;

namespace MathHorde.Tests.PlayMode.AppFlow
{
    [TestFixture]
    public class SessionOutcomeBridgeTests
    {
        private static AppStateMachine AtGameplay()
        {
            var machine = new AppStateMachine();
            machine.Fire(AppTrigger.ConfigLoaded);
            machine.Fire(AppTrigger.ProfileLoaded);
            machine.Fire(AppTrigger.PlayPressed);
            machine.Fire(AppTrigger.LevelSelected);
            return machine;
        }

        [Test]
        public void SessionEndedWithVictory_FiresVictoryConditionMet_MovingToLevelComplete()
        {
            var bus = new GameEventBus();
            AppStateMachine machine = AtGameplay();
            using (new SessionOutcomeBridge(bus, machine))
            {
                bus.Publish(new SessionEnded(GameSessionId.New(), SessionResult.Victory, FailureReason.None, 12.0));

                Assert.That(machine.Current, Is.EqualTo(AppState.LevelComplete));
            }
        }

        [Test]
        public void SessionEndedWithDefeat_FiresPlayerHealthZero_MovingToGameOver()
        {
            var bus = new GameEventBus();
            AppStateMachine machine = AtGameplay();
            using (new SessionOutcomeBridge(bus, machine))
            {
                bus.Publish(new SessionEnded(GameSessionId.New(), SessionResult.Defeat, FailureReason.HealthDepleted, 7.0));

                Assert.That(machine.Current, Is.EqualTo(AppState.GameOver));
            }
        }

        [Test]
        public void Dispose_UnsubscribesFromSessionEnded()
        {
            var bus = new GameEventBus();
            AppStateMachine machine = AtGameplay();
            var bridge = new SessionOutcomeBridge(bus, machine);

            bridge.Dispose();
            bus.Publish(new SessionEnded(GameSessionId.New(), SessionResult.Victory, FailureReason.None, 1.0));

            Assert.That(machine.Current, Is.EqualTo(AppState.Gameplay), "Leiratkozás után az esemény már nem válthat állapotot.");
        }

        [Test]
        public void Constructor_NullArguments_Throw()
        {
            var bus = new GameEventBus();
            var machine = new AppStateMachine();

            Assert.That(
                () => new SessionOutcomeBridge(null, machine),
                Throws.TypeOf<System.ArgumentNullException>());
            Assert.That(
                () => new SessionOutcomeBridge(bus, null),
                Throws.TypeOf<System.ArgumentNullException>());
        }
    }
}
