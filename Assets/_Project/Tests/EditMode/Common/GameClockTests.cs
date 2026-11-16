using MathHorde.Core.Common;
using NUnit.Framework;

namespace MathHorde.Tests.EditMode.Common
{
    // GameClock is the default IGameClock (core-domain.md 1.) used by
    // ResourceService (and, later, GameSessionService) to stamp events with
    // "now" instead of reading UnityEngine.Time (rule G1).
    [TestFixture]
    public class GameClockTests
    {
        [Test]
        public void Advance_AccumulatesGameTime()
        {
            var clock = new GameClock();

            clock.Advance(0.5f);
            clock.Advance(0.25f);

            Assert.That(clock.GameTime, Is.EqualTo(0.75).Within(1e-6));
        }

        [Test]
        public void Advance_WhilePaused_DoesNotAdvanceGameTime()
        {
            var clock = new GameClock();
            clock.Pause();

            clock.Advance(1.0f);

            Assert.That(clock.GameTime, Is.EqualTo(0.0));
            Assert.That(clock.IsPaused, Is.True);
        }

        [Test]
        public void Resume_AfterPause_AdvancesAgain()
        {
            var clock = new GameClock();
            clock.Pause();
            clock.Advance(1.0f);
            clock.Resume();

            clock.Advance(1.0f);

            Assert.That(clock.GameTime, Is.EqualTo(1.0).Within(1e-6));
            Assert.That(clock.IsPaused, Is.False);
        }

        [Test]
        public void SetTimeScale_ScalesSubsequentAdvance()
        {
            var clock = new GameClock();
            clock.SetTimeScale(2.0f);

            clock.Advance(1.0f);

            Assert.That(clock.GameTime, Is.EqualTo(2.0).Within(1e-6));
            Assert.That(clock.TimeScale, Is.EqualTo(2.0f));
        }

        [Test]
        public void Advance_NegativeDeltaTime_IsTreatedAsZero()
        {
            var clock = new GameClock();

            clock.Advance(-5.0f);

            Assert.That(clock.GameTime, Is.EqualTo(0.0));
        }
    }
}
