using System;
using MathHorde.Core.Common;
using NUnit.Framework;

namespace MathHorde.Tests.EditMode.Common
{
    // Exercises LaneIndex and Vec2 (MathHorde.Core.Common) — zero
    // UnityEngine dependency, compiles and runs both under the Unity Test
    // Runner and the headless tests/MathHorde.Tests.sln mirror (ADR-003).
    [TestFixture]
    public class LaneIndexTests
    {
        [Test]
        public void Constructor_NonNegativeValue_StoresValue()
        {
            var lane = new LaneIndex(2);

            Assert.That(lane.Value, Is.EqualTo(2));
        }

        [Test]
        public void Constructor_NegativeValue_Throws()
        {
            Assert.That(() => new LaneIndex(-1), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void Equals_SameValue_AreEqual()
        {
            var a = new LaneIndex(3);
            var b = new LaneIndex(3);

            Assert.That(a.Equals(b), Is.True);
            Assert.That(a == b, Is.True);
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
        }

        [Test]
        public void Equals_DifferentValue_AreNotEqual()
        {
            var a = new LaneIndex(0);
            var b = new LaneIndex(1);

            Assert.That(a.Equals(b), Is.False);
            Assert.That(a != b, Is.True);
        }
    }

    [TestFixture]
    public class Vec2Tests
    {
        [Test]
        public void Constructor_StoresXAndY()
        {
            var v = new Vec2(1.5f, -2.5f);

            Assert.That(v.X, Is.EqualTo(1.5f));
            Assert.That(v.Y, Is.EqualTo(-2.5f));
        }
    }
}
