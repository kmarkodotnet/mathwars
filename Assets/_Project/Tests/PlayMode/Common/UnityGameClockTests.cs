// F1-11 — UnityGameClock lives in Infrastructure (UnityEngine.Time), so the
// EditMode/dotnet mirror cannot link it; this stays a plain [Test] (no
// running scene needed) in PlayMode, same precedent as
// GameConfigurationRepositoryTests (unity-csharp skill, "Buktatók
// (ScriptableObject-konfig, F1-09)").
using MathHorde.Infrastructure.Common;
using NUnit.Framework;
using UnityEngine;

namespace MathHorde.Tests.PlayMode.Common
{
    [TestFixture]
    public class UnityGameClockTests
    {
        [TearDown]
        public void TearDown()
        {
            // Defensive: never let a failed assertion leak a frozen
            // Time.timeScale into a later, unrelated PlayMode test.
            Time.timeScale = 1f;
        }

        [Test]
        public void Advance_AccumulatesGameTime()
        {
            var clock = new UnityGameClock();

            clock.Advance(0.5f);
            clock.Advance(0.25f);

            Assert.That(clock.GameTime, Is.EqualTo(0.75).Within(1e-6));
        }

        [Test]
        public void Advance_WhilePaused_DoesNotAdvanceGameTime()
        {
            var clock = new UnityGameClock();
            clock.Pause();

            clock.Advance(1.0f);

            Assert.That(clock.GameTime, Is.EqualTo(0.0));
        }

        [Test]
        public void Pause_SetsUnityTimeScaleToZero()
        {
            Time.timeScale = 1f;
            var clock = new UnityGameClock();

            clock.Pause();

            Assert.That(Time.timeScale, Is.EqualTo(0f));
            Assert.That(clock.IsPaused, Is.True);
        }

        [Test]
        public void Resume_RestoresPreviousUnityTimeScale()
        {
            Time.timeScale = 1f;
            var clock = new UnityGameClock();
            clock.Pause();

            clock.Resume();

            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(clock.IsPaused, Is.False);
        }

        [Test]
        public void Resume_RestoresTimeScaleThatWasActive_BeforePause_NotAlways1()
        {
            Time.timeScale = 2f;
            var clock = new UnityGameClock();
            clock.Pause();

            clock.Resume();

            Assert.That(Time.timeScale, Is.EqualTo(2f));
        }

        [Test]
        public void Pause_WhileAlreadyPaused_DoesNotOverwriteSavedTimeScaleWithZero()
        {
            Time.timeScale = 3f;
            var clock = new UnityGameClock();
            clock.Pause();

            // A second Pause() call (e.g. a duplicate AppFocusLost) must not
            // clobber the saved pre-pause value with the current (already
            // zeroed) Time.timeScale.
            clock.Pause();
            clock.Resume();

            Assert.That(Time.timeScale, Is.EqualTo(3f));
        }

        [Test]
        public void Advance_NegativeDeltaTime_IsTreatedAsZero()
        {
            var clock = new UnityGameClock();

            clock.Advance(-5.0f);

            Assert.That(clock.GameTime, Is.EqualTo(0.0));
        }
    }
}
