using System;
using System.Collections.Generic;
using MathHorde.GameLogic;
using NUnit.Framework;

namespace MathHorde.Tests.EditMode.GameLogic
{
    [TestFixture]
    public class MathEventIdTests
    {
        [Test]
        public void Create_HasStableFormatAndRoundTripsThroughGuid()
        {
            MathEventId id = MathEventId.Create(1234, 5678);
            Assert.That(id.ToString(), Is.EqualTo("000004d2-162e-0000-4d61-746845767401"));
            Assert.That(new MathEventId(Guid.Parse(id.ToString())), Is.EqualTo(id));
            Assert.That(id.Value, Is.Not.EqualTo(Guid.Empty));
        }

        [TestCase(0, 0)]
        [TestCase(-1, -1)]
        [TestCase(int.MinValue, int.MaxValue)]
        [TestCase(int.MaxValue, int.MinValue)]
        public void Create_SameSeedAndIndexHaveValueEquality(int seed, int index)
        {
            MathEventId first = MathEventId.Create(seed, index);
            MathEventId second = MathEventId.Create(seed, index);
            Assert.That(first == second, Is.True);
            Assert.That(first != second, Is.False);
            Assert.That(first.Equals((object)second), Is.True);
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
            Assert.That(first.Equals(null), Is.False);
            Assert.That(first.Equals(first.Value), Is.False);
        }

        [Test]
        public void Create_DifferentSeedsAndAllIndexBitsRemainDistinct()
        {
            var ids = new HashSet<MathEventId>();
            foreach (int seed in new[] { int.MinValue, -1, 0, 1, int.MaxValue })
            {
                foreach (int index in new[] { int.MinValue, -1, 0, 1, 65535, 65536, int.MaxValue })
                {
                    Assert.That(ids.Add(MathEventId.Create(seed, index)), Is.True);
                }
            }
            Assert.That(MathEventId.Create(1, 1) != MathEventId.Create(1, 65537), Is.True);
        }

        [Test]
        public void Create_TenThousandEventOrdinalsAreUniqueAndReproducible()
        {
            var ids = new HashSet<MathEventId>();
            for (int i = 0; i < 10000; i++)
            {
                Assert.That(ids.Add(MathEventId.Create(-76543, i)), Is.True);
                Assert.That(ids.Contains(MathEventId.Create(-76543, i)), Is.True);
            }
        }
    }
}
