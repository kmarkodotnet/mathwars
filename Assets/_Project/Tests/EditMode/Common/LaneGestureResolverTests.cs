using System;
using MathHorde.Core.Common;
using NUnit.Framework;

namespace MathHorde.Tests.EditMode.Common
{
    // Exercises LaneGestureResolver (MathHorde.Core.Common) — zero
    // UnityEngine dependency, compiles and runs both under the Unity Test
    // Runner and the headless tests/MathHorde.Tests.sln mirror (ADR-003).
    // This is the actual F1-13 "a sávváltás döntési logikája legyen
    // Unity-független és tesztelhető" requirement.
    [TestFixture]
    public class LaneGestureResolverTests
    {
        [TestCase(0, 3, SwipeDirection.Right, 1)]
        [TestCase(1, 3, SwipeDirection.Right, 2)]
        [TestCase(1, 3, SwipeDirection.Left, 0)]
        [TestCase(2, 4, SwipeDirection.Right, 3)]
        public void ResolveSwipe_WithinBounds_MovesOneLane(int current, int laneCount, SwipeDirection direction, int expected)
        {
            LaneIndex result = LaneGestureResolver.ResolveSwipe(new LaneIndex(current), laneCount, direction);

            Assert.That(result.Value, Is.EqualTo(expected));
        }

        [Test]
        public void ResolveSwipe_LeftFromFirstLane_StaysAtFirstLane()
        {
            LaneIndex result = LaneGestureResolver.ResolveSwipe(new LaneIndex(0), 3, SwipeDirection.Left);

            Assert.That(result.Value, Is.EqualTo(0));
        }

        [Test]
        public void ResolveSwipe_RightFromLastLane_StaysAtLastLane()
        {
            LaneIndex result = LaneGestureResolver.ResolveSwipe(new LaneIndex(2), 3, SwipeDirection.Right);

            Assert.That(result.Value, Is.EqualTo(2));
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void ResolveSwipe_SingleLaneArena_NeverMoves(int laneCount)
        {
            // Regression guard: a 1-lane arena is not a real level (spec 3.2
            // requires 2..4), but the resolver itself must not crash or wrap
            // for any laneCount passed to it — it clamps, it never throws
            // for valid (>=1) laneCount values.
            LaneIndex result = LaneGestureResolver.ResolveSwipe(new LaneIndex(0), 1, SwipeDirection.Right);

            Assert.That(result.Value, Is.EqualTo(0));
        }

        [Test]
        public void ResolveSwipe_InvalidLaneCount_Throws()
        {
            Assert.That(
                () => LaneGestureResolver.ResolveSwipe(new LaneIndex(0), 0, SwipeDirection.Right),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [TestCase(3, 0.0f, 0)]
        [TestCase(3, 0.1f, 0)]
        [TestCase(3, 0.5f, 1)]
        [TestCase(3, 0.9f, 2)]
        [TestCase(3, 1.0f, 2)]
        [TestCase(2, 0.49f, 0)]
        [TestCase(2, 0.51f, 1)]
        [TestCase(4, 0.99f, 3)]
        public void ResolveTap_NormalizedX_SelectsExpectedLane(int laneCount, float normalizedX, int expected)
        {
            LaneIndex result = LaneGestureResolver.ResolveTap(laneCount, normalizedX);

            Assert.That(result.Value, Is.EqualTo(expected));
        }

        [TestCase(-0.5f)]
        [TestCase(1.5f)]
        public void ResolveTap_OutOfRangeNormalizedX_Clamps(float normalizedX)
        {
            // Tap coordinates should already be 0..1, but a rounding/edge
            // value from the input device must never throw or select an
            // out-of-range lane (that would make MovePlayerToLane throw).
            LaneIndex result = LaneGestureResolver.ResolveTap(3, normalizedX);

            Assert.That(result.Value, Is.InRange(0, 2));
        }

        [Test]
        public void ResolveTap_InvalidLaneCount_Throws()
        {
            Assert.That(
                () => LaneGestureResolver.ResolveTap(0, 0.5f),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void ClassifyDrag_HorizontalPastThreshold_ReturnsRight()
        {
            SwipeDirection? result = LaneGestureResolver.ClassifyDrag(deltaX: 100f, deltaY: 5f, minSwipeDistance: 60f);

            Assert.That(result, Is.EqualTo(SwipeDirection.Right));
        }

        [Test]
        public void ClassifyDrag_HorizontalNegativePastThreshold_ReturnsLeft()
        {
            SwipeDirection? result = LaneGestureResolver.ClassifyDrag(deltaX: -100f, deltaY: -5f, minSwipeDistance: 60f);

            Assert.That(result, Is.EqualTo(SwipeDirection.Left));
        }

        [Test]
        public void ClassifyDrag_BelowThreshold_ReturnsNull()
        {
            SwipeDirection? result = LaneGestureResolver.ClassifyDrag(deltaX: 20f, deltaY: 2f, minSwipeDistance: 60f);

            Assert.That(result, Is.Null);
        }

        [Test]
        public void ClassifyDrag_VerticalDominant_ReturnsNull()
        {
            // A big vertical move (e.g. a scroll-like flick) is not a
            // lane-swipe even if it also has a large horizontal component.
            SwipeDirection? result = LaneGestureResolver.ClassifyDrag(deltaX: 70f, deltaY: 200f, minSwipeDistance: 60f);

            Assert.That(result, Is.Null);
        }

        [Test]
        public void ClassifyDrag_EqualHorizontalAndVertical_ReturnsNull()
        {
            // |dx| == |dy| is not "horizontal dominant" (strict >), so this
            // ambiguous diagonal drag must not be treated as a lane-swipe.
            SwipeDirection? result = LaneGestureResolver.ClassifyDrag(deltaX: 80f, deltaY: 80f, minSwipeDistance: 60f);

            Assert.That(result, Is.Null);
        }
    }
}
