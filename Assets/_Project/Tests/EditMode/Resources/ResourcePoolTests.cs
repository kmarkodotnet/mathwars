using System;
using MathHorde.Core.Resources;
using NUnit.Framework;

namespace MathHorde.Tests.EditMode.Resources
{
    // Contract core-domain.md 5.: "Min <= Current <= Max mindig fennáll; a
    // vágás soha nem dob kivételt" — ResourcePool itself enforces that
    // invariant at construction by clamping, and only throws for a
    // structurally invalid Min/Max shape (a programmer error).
    [TestFixture]
    public class ResourcePoolTests
    {
        [Test]
        public void Construct_CurrentAboveMax_ClampsToMax()
        {
            var pool = new ResourcePool(ResourceType.Health, current: 500, max: 200, min: 0);

            Assert.That(pool.Current, Is.EqualTo(200));
        }

        [Test]
        public void Construct_CurrentBelowMin_ClampsToMin()
        {
            var pool = new ResourcePool(ResourceType.Health, current: -10, max: 100, min: 0);

            Assert.That(pool.Current, Is.EqualTo(0));
        }

        [Test]
        public void Construct_CurrentWithinRange_IsUnchanged()
        {
            var pool = new ResourcePool(ResourceType.Shield, current: 42, max: 100, min: 0);

            Assert.That(pool.Current, Is.EqualTo(42));
        }

        [Test]
        public void Construct_MaxNotPositive_Throws()
        {
            Assert.That(
                () => new ResourcePool(ResourceType.Health, current: 0, max: 0, min: 0),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void Construct_MinGreaterThanOrEqualToMax_Throws()
        {
            Assert.That(
                () => new ResourcePool(ResourceType.Health, current: 0, max: 10, min: 10),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void IsDepleted_CurrentEqualsMin_IsTrue()
        {
            var pool = new ResourcePool(ResourceType.Health, current: 0, max: 200, min: 0);

            Assert.That(pool.IsDepleted, Is.True);
        }

        [Test]
        public void IsDepleted_CurrentAboveMin_IsFalse()
        {
            var pool = new ResourcePool(ResourceType.Health, current: 1, max: 200, min: 0);

            Assert.That(pool.IsDepleted, Is.False);
        }
    }
}
