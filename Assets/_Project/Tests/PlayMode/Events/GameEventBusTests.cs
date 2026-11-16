// GameEventBusTests.cs
// F1-14/F1-15 — proves GameEventBus is pure routing (contract game-logic.md
// 1., rule G3): every subscriber for a published event type is invoked with
// the exact payload, unrelated event types are untouched, and Unsubscribe
// actually stops delivery. Lives under Tests/PlayMode for the same
// assembly-reference reason as ObjectPoolTests.cs.
using NUnit.Framework;
using MathHorde.GameLogic.Events;
using MathHorde.Presentation.Events;

namespace MathHorde.Tests.PlayMode.Events
{
    [TestFixture]
    public class GameEventBusTests
    {
        [Test]
        public void Publish_InvokesSubscribedHandler_WithTheExactPayload()
        {
            var bus = new GameEventBus();
            WaveStarted? received = null;
            bus.Subscribe<WaveStarted>(e => received = e);

            bus.Publish(new WaveStarted(3, 12.5));

            Assert.That(received, Is.Not.Null);
            Assert.That(received.Value.WaveIndex, Is.EqualTo(3));
            Assert.That(received.Value.GameTime, Is.EqualTo(12.5));
        }

        [Test]
        public void Publish_WithNoSubscribers_DoesNotThrow()
        {
            var bus = new GameEventBus();

            Assert.That(() => bus.Publish(new WaveStarted(1, 0.0)), Throws.Nothing);
        }

        [Test]
        public void Publish_InvokesEveryRegisteredHandler_ForThatEventType()
        {
            var bus = new GameEventBus();
            int firstCalls = 0;
            int secondCalls = 0;
            bus.Subscribe<WaveStarted>(_ => firstCalls++);
            bus.Subscribe<WaveStarted>(_ => secondCalls++);

            bus.Publish(new WaveStarted(1, 0.0));

            Assert.That(firstCalls, Is.EqualTo(1));
            Assert.That(secondCalls, Is.EqualTo(1));
        }

        [Test]
        public void Publish_OnlyInvokesHandlersForThatSpecificEventType()
        {
            var bus = new GameEventBus();
            int waveCalls = 0;
            int clearedCalls = 0;
            bus.Subscribe<WaveStarted>(_ => waveCalls++);
            bus.Subscribe<WaveCleared>(_ => clearedCalls++);

            bus.Publish(new WaveStarted(1, 0.0));

            Assert.That(waveCalls, Is.EqualTo(1));
            Assert.That(clearedCalls, Is.EqualTo(0));
        }

        [Test]
        public void Unsubscribe_StopsFurtherDelivery_ToThatHandler()
        {
            var bus = new GameEventBus();
            int calls = 0;
            System.Action<WaveStarted> handler = _ => calls++;
            bus.Subscribe(handler);

            bus.Publish(new WaveStarted(1, 0.0));
            bus.Unsubscribe(handler);
            bus.Publish(new WaveStarted(2, 0.0));

            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void Unsubscribe_LeavesOtherHandlersForTheSameEventTypeIntact()
        {
            var bus = new GameEventBus();
            int firstCalls = 0;
            int secondCalls = 0;
            System.Action<WaveStarted> first = _ => firstCalls++;
            bus.Subscribe(first);
            bus.Subscribe<WaveStarted>(_ => secondCalls++);

            bus.Unsubscribe(first);
            bus.Publish(new WaveStarted(1, 0.0));

            Assert.That(firstCalls, Is.EqualTo(0));
            Assert.That(secondCalls, Is.EqualTo(1));
        }

        [Test]
        public void Unsubscribe_UnknownHandler_IsANoOp()
        {
            var bus = new GameEventBus();

            Assert.That(() => bus.Unsubscribe<WaveStarted>(_ => { }), Throws.Nothing);
        }
    }
}
