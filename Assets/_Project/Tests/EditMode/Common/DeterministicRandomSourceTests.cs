using System;
using System.Collections.Generic;
using MathHorde.Core.Common;
using NUnit.Framework;

namespace MathHorde.Tests.EditMode.Common
{
    // Exercises DeterministicRandomSource (MathHorde.Core.Common) only — zero
    // UnityEngine dependency, so this fixture compiles and runs both under
    // the Unity Test Runner and the headless tests/MathHorde.Tests.sln
    // mirror (ADR-003).
    //
    // Uses the NUnit 4 constraint model (Assert.That) exclusively — see
    // .claude/skills/unity-testing/SKILL.md "NUnit API-kompatibilitás":
    // Assert.AreEqual/IsTrue/... are not members of NUnit.Framework.Assert
    // in NUnit 4.2, and Assert.Multiple is NUnit 3.6+/4-only, so neither is
    // used here.
    [TestFixture]
    public class DeterministicRandomSourceTests
    {
        // ADR-006 "Kötelező teszt": a known seed must reproduce a known,
        // byte-for-byte sequence. This is a DELIBERATE golden sample
        // (regression snapshot) — it is EXPECTED to fail if anyone changes
        // the xoshiro128**/SplitMix32 algorithm or the seeding scheme; that
        // is the whole point of ADR-006 (platform-independent seed
        // stability between dotnet test and Unity Mono/IL2CPP). The values
        // below were produced by running this exact implementation once
        // (seed 12345, Next(0, 1000), first 10 draws) and are pinned here
        // as constants, not re-derived from the production code.
        [Test]
        public void Next_GoldenSample_MatchesPinnedSequence()
        {
            var rng = new DeterministicRandomSource(12345);

            var actual = new int[10];
            for (int i = 0; i < actual.Length; i++)
            {
                actual[i] = rng.Next(0, 1000);
            }

            var expected = new[] { 547, 357, 573, 158, 660, 443, 520, 777, 786, 672 };
            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public void Next_SameSeed_ProducesSameSequenceTwice()
        {
            var rngA = new DeterministicRandomSource(4242);
            var rngB = new DeterministicRandomSource(4242);

            var sequenceA = DrawSequence(rngA, 20);
            var sequenceB = DrawSequence(rngB, 20);

            Assert.That(sequenceA, Is.EqualTo(sequenceB));
        }

        [Test]
        public void Next_DifferentSeeds_ProduceDifferentSequences()
        {
            var rngA = new DeterministicRandomSource(1);
            var rngB = new DeterministicRandomSource(2);

            var sequenceA = DrawSequence(rngA, 20);
            var sequenceB = DrawSequence(rngB, 20);

            Assert.That(sequenceA, Is.Not.EqualTo(sequenceB));
        }

        [Test]
        public void Constructor_SeedZero_DoesNotYieldDegenerateState()
        {
            var rng = new DeterministicRandomSource(0);

            var sequence = DrawSequence(rng, 10);

            // Degenerate state would repeat the same value (e.g. all 0s) —
            // a distinct-value set of this size means the SplitMix32 seed
            // expansion escaped the trivial all-zero xoshiro128** state.
            Assert.That(new HashSet<int>(sequence).Count, Is.GreaterThan(1));
        }

        [Test]
        public void Seed_ReturnsConstructorValue()
        {
            var rng = new DeterministicRandomSource(9876);

            Assert.That(rng.Seed, Is.EqualTo(9876));
        }

        [Test]
        public void Derive_SamePurposeKey_YieldsIdenticalChildSequence()
        {
            var parent = new DeterministicRandomSource(777);

            var childA = parent.Derive("expression");
            var childB = parent.Derive("expression");

            Assert.That(DrawSequence(childA, 10), Is.EqualTo(DrawSequence(childB, 10)));
        }

        [Test]
        public void Derive_DifferentPurposeKey_YieldsDifferentChildSequence()
        {
            var parent = new DeterministicRandomSource(777);

            var expressionChild = parent.Derive("expression");
            var distractorsChild = parent.Derive("distractors");

            Assert.That(DrawSequence(expressionChild, 10), Is.Not.EqualTo(DrawSequence(distractorsChild, 10)));
        }

        [Test]
        public void Derive_IsIndependentOfParentCallsMadeBeforeDeriving()
        {
            var untouchedParent = new DeterministicRandomSource(777);
            var usedParent = new DeterministicRandomSource(777);

            // Advance usedParent's internal state before deriving — this
            // must NOT change what Derive("expression") produces (ADR-006:
            // one subsystem's call count must never shift another's output).
            for (int i = 0; i < 37; i++)
            {
                usedParent.Next(0, 1_000_000);
            }

            var childFromUntouched = untouchedParent.Derive("expression");
            var childFromUsed = usedParent.Derive("expression");

            Assert.That(DrawSequence(childFromUntouched, 10), Is.EqualTo(DrawSequence(childFromUsed, 10)));
        }

        [Test]
        public void Derive_NullPurposeKey_Throws()
        {
            var parent = new DeterministicRandomSource(1);

            Assert.That(() => parent.Derive(null), Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void Next_InvalidRange_Throws()
        {
            var rng = new DeterministicRandomSource(1);

            Assert.That(() => rng.Next(5, 5), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => rng.Next(5, 4), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void Next_LargeSample_NeverLeavesRequestedRange()
        {
            var rng = new DeterministicRandomSource(5);

            for (int i = 0; i < 200_000; i++)
            {
                int value = rng.Next(5, 15);
                Assert.That(value, Is.GreaterThanOrEqualTo(5));
                Assert.That(value, Is.LessThan(15));
            }
        }

        [Test]
        public void Next_LargeSample_IsReasonablyUniform()
        {
            var rng = new DeterministicRandomSource(999);
            const int bucketCount = 10;
            const int totalDraws = 200_000;
            var buckets = new int[bucketCount];

            for (int i = 0; i < totalDraws; i++)
            {
                buckets[rng.Next(0, bucketCount)]++;
            }

            int expectedPerBucket = totalDraws / bucketCount;
            int tolerance = (int)(expectedPerBucket * 0.05);

            foreach (int count in buckets)
            {
                Assert.That(count, Is.InRange(expectedPerBucket - tolerance, expectedPerBucket + tolerance));
            }
        }

        [Test]
        public void NextDouble_LargeSample_StaysWithinZeroToOneExclusiveUpper()
        {
            var rng = new DeterministicRandomSource(1);

            for (int i = 0; i < 200_000; i++)
            {
                double value = rng.NextDouble();
                Assert.That(value, Is.GreaterThanOrEqualTo(0.0));
                Assert.That(value, Is.LessThan(1.0));
            }
        }

        private static int[] DrawSequence(IRandomSource rng, int count)
        {
            var values = new int[count];
            for (int i = 0; i < count; i++)
            {
                values[i] = rng.Next(0, 1_000_000_000);
            }

            return values;
        }
    }
}
