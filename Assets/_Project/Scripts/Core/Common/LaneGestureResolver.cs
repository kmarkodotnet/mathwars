// LaneGestureResolver.cs
// F1-13 — the Unity-independent half of "egyujjas húzás/koppintás →
// sávváltás" (math-horde.md 3.3). Pure, stateless arithmetic over LaneIndex:
// given the CURRENT lane (read from ICombatService.PlayerLane — nothing
// here duplicates that state) and the arena's lane count, decide which lane
// a swipe or a tap targets, with explicit, testable boundary behaviour
// (swiping left from lane 0 stays at lane 0; there is no wraparound and no
// exception — ICombatService.MovePlayerToLane throws for an out-of-range
// lane, so the clamp below is what keeps every call in range).
//
// Deliberate layering note (flagged for the architect, not a kontrakt
// change): this type is not listed in docs/contracts/core-domain.md. It is
// placed in Core.Common — beside the LaneIndex it only reads — specifically
// so the dotnet mirror (game/tests/, ADR-003) and the EditMode test runner
// both exercise it, per the F1-13 task note "a Unity-független
// sávváltás-logika mehet EditMode-ba... ez az előnyösebb, ha meg tudod
// oldani a szétválasztást". It introduces no GameLogic/session state and no
// UnityEngine reference, so it does not violate the noEngineReferences rule
// or any existing interface; PlayerController (Presentation) is the only
// caller today.
using System;

namespace MathHorde.Core.Common
{
    /// <summary>One-finger lane-change gesture, classified from a drag delta.</summary>
    public enum SwipeDirection
    {
        Left = 0,
        Right = 1
    }

    public static class LaneGestureResolver
    {
        /// <summary>
        /// Swipe left/right moves exactly one lane, clamped at the arena
        /// edges (spec 3.3: "balra/jobbra húzás: átlépés a szomszédos
        /// sávba" — the outermost lane has no neighbour on that side, so
        /// the gesture is a no-op there, never a wraparound or an
        /// exception).
        /// </summary>
        public static LaneIndex ResolveSwipe(LaneIndex currentLane, int laneCount, SwipeDirection direction)
        {
            if (laneCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(laneCount), laneCount, "A sávszám legalább 1.");
            }

            int step = direction == SwipeDirection.Right ? 1 : -1;
            int target = Clamp(currentLane.Value + step, 0, laneCount - 1);
            return new LaneIndex(target);
        }

        /// <summary>
        /// Direct lane selection by tap position (spec 3.3: "sávra
        /// koppintás: közvetlen sávválasztás"). <paramref name="normalizedX"/>
        /// is the tap's horizontal position as a 0..1 fraction of the
        /// input surface (e.g. screen width); the arena is divided into
        /// <paramref name="laneCount"/> equal columns, left to right.
        /// </summary>
        public static LaneIndex ResolveTap(int laneCount, float normalizedX)
        {
            if (laneCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(laneCount), laneCount, "A sávszám legalább 1.");
            }

            float clampedX = normalizedX < 0f ? 0f : (normalizedX > 1f ? 1f : normalizedX);
            int index = (int)(clampedX * laneCount);
            index = Clamp(index, 0, laneCount - 1);
            return new LaneIndex(index);
        }

        /// <summary>
        /// Classifies a completed drag as a horizontal lane-swipe, or
        /// <c>null</c> if it does not qualify (too short, or
        /// vertical-dominant — e.g. a scroll/flick that was never meant as
        /// a lane change). The caller (InputAdapter) decides what a
        /// non-qualifying, short drag means (typically: a tap).
        /// </summary>
        public static SwipeDirection? ClassifyDrag(float deltaX, float deltaY, float minSwipeDistance)
        {
            float absX = Math.Abs(deltaX);
            float absY = Math.Abs(deltaY);

            if (absX < minSwipeDistance)
            {
                return null;
            }

            if (absX <= absY)
            {
                return null;
            }

            return deltaX > 0f ? SwipeDirection.Right : SwipeDirection.Left;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }
    }
}
