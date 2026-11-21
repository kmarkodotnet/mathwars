using MathHorde.Core.Common;
using MathHorde.GameLogic.Events;
using MathHorde.Tests.EditMode.TestSupport;
using NUnit.Framework;

namespace MathHorde.Tests.EditMode.Events
{
    // Contract game-logic.md 1. — exercises the IGameEventSink mechanism and
    // a couple of the Phase 1 event structs directly, independent of any
    // GameLogic service, so a regression in the sink plumbing itself (not
    // just in ResourceService's use of it) shows up here.
    [TestFixture]
    public class GameEventSinkTests
    {
        [Test]
        public void Publish_GenericEvent_IsRecordedBySink()
        {
            var sink = new RecordingGameEventSink();

            sink.Publish(new WaveStarted(waveIndex: 2, gameTime: 12.5));

            var waveStartedEvents = sink.OfType<WaveStarted>();
            Assert.That(waveStartedEvents.Count, Is.EqualTo(1));
            Assert.That(waveStartedEvents[0].WaveIndex, Is.EqualTo(2));
            Assert.That(waveStartedEvents[0].GameTime, Is.EqualTo(12.5));
        }

        [Test]
        public void Publish_DifferentEventTypes_AreFilterableIndependently()
        {
            var sink = new RecordingGameEventSink();

            sink.Publish(new WaveStarted(0, 0.0));
            sink.Publish(new EnemyKilled(enemyInstanceId: 7, enemyId: "basic_bot", gameTime: 1.0));
            sink.Publish(new WeaponFired(1, "weapon", 5, gameTime: 2.0));

            Assert.That(sink.PublishedCount, Is.EqualTo(3));
            Assert.That(sink.OfType<EnemyKilled>().Count, Is.EqualTo(1));
            Assert.That(sink.OfType<WeaponFired>().Count, Is.EqualTo(1));
            Assert.That(sink.OfType<WaveCleared>(), Is.Empty);
        }

        [Test]
        public void PlayerLaneChanged_CarriesFromAndToLanes()
        {
            var sink = new RecordingGameEventSink();

            sink.Publish(new PlayerLaneChanged(new LaneIndex(0), new LaneIndex(2), gameTime: 3.0));

            var changed = sink.OfType<PlayerLaneChanged>()[0];
            Assert.That(changed.From.Value, Is.EqualTo(0));
            Assert.That(changed.To.Value, Is.EqualTo(2));
        }
    }
}
