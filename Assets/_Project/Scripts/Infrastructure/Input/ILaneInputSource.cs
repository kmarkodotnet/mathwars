// ILaneInputSource.cs
// F1-13 — the Presentation-facing seam over the raw touch/pointer device.
// PlayerController depends on this interface only, never on InputAdapter or
// on UnityEngine.InputSystem directly, so it stays testable with a fake
// source and swappable (e.g. a replay/bot source for the balancing
// simulation's human-input stand-in, if that is ever needed outside the
// headless GameLogic simulation).
using System;

namespace MathHorde.Infrastructure.Input
{
    public interface ILaneInputSource
    {
        /// <summary>Raised once per qualifying left/right drag (math-horde.md 3.3).</summary>
        event Action<MathHorde.Core.Common.SwipeDirection> SwipeDetected;

        /// <summary>
        /// Raised once per tap that was not a swipe. The payload is the
        /// tap's horizontal position as a 0..1 fraction of the input
        /// surface width.
        /// </summary>
        event Action<float> TapDetected;
    }
}
