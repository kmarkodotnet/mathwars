// local-persistence skill: "periodikus autosave ÉS kulcs-eseményekre kötött
// mentés — nem kizárólag alkalmazás-bezáráskor" (ADR-011 5.). Lives under
// Tests/PlayMode because AutosaveScheduler is physically part of the
// MathHorde.Infrastructure assembly (EditMode's asmdef does not reference
// it), even though the class itself has no UnityEngine dependency.
using System;
using MathHorde.Infrastructure.Persistence;
using NUnit.Framework;

namespace MathHorde.Tests.PlayMode.Persistence
{
    [TestFixture]
    public class AutosaveSchedulerTests
    {
        [Test]
        public void Tick_BelowInterval_DoesNotSave()
        {
            int saveCount = 0;
            var scheduler = new AutosaveScheduler(() => saveCount++, intervalSeconds: 10f);

            scheduler.Tick(5f);

            Assert.That(saveCount, Is.EqualTo(0));
        }

        [Test]
        public void Tick_ReachingInterval_SavesExactlyOnce()
        {
            int saveCount = 0;
            var scheduler = new AutosaveScheduler(() => saveCount++, intervalSeconds: 10f);

            scheduler.Tick(6f);
            scheduler.Tick(5f);

            Assert.That(saveCount, Is.EqualTo(1));
        }

        [Test]
        public void Tick_AfterSaving_TimerResetsForNextInterval()
        {
            int saveCount = 0;
            var scheduler = new AutosaveScheduler(() => saveCount++, intervalSeconds: 10f);

            scheduler.Tick(10f);
            scheduler.Tick(9f);

            Assert.That(saveCount, Is.EqualTo(1));
        }

        [Test]
        public void NotifyKeyEvent_SavesImmediatelyRegardlessOfTimer()
        {
            int saveCount = 0;
            var scheduler = new AutosaveScheduler(() => saveCount++, intervalSeconds: 30f);

            scheduler.Tick(1f);
            scheduler.NotifyKeyEvent();

            Assert.That(saveCount, Is.EqualTo(1));
        }

        [Test]
        public void NotifyKeyEvent_ResetsPeriodicTimer()
        {
            int saveCount = 0;
            var scheduler = new AutosaveScheduler(() => saveCount++, intervalSeconds: 10f);

            scheduler.Tick(9f);
            scheduler.NotifyKeyEvent();
            saveCount = 0;
            scheduler.Tick(9f);

            Assert.That(saveCount, Is.EqualTo(0));
        }

        [Test]
        public void Constructor_NonPositiveInterval_Throws()
        {
            Assert.That(() => new AutosaveScheduler(() => { }, intervalSeconds: 0f), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void Constructor_NullSaveAction_Throws()
        {
            Assert.That(() => new AutosaveScheduler(null), Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void Tick_NegativeDeltaTime_Throws()
        {
            var scheduler = new AutosaveScheduler(() => { });

            Assert.That(() => scheduler.Tick(-1f), Throws.TypeOf<ArgumentOutOfRangeException>());
        }
    }
}
