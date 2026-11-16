// FakeLaneInputSource.cs
// F1-13 — minimal ILaneInputSource test double. Lets PlayerControllerTests
// raise a swipe/tap gesture directly, without any real Input System device,
// to isolate "does the controller forward correctly" from "does the
// adapter read the pointer correctly" (the latter is InputAdapterTests).
using System;
using MathHorde.Core.Common;
using MathHorde.Infrastructure.Input;

namespace MathHorde.Tests.PlayMode.TestSupport
{
    public sealed class FakeLaneInputSource : ILaneInputSource
    {
        public event Action<SwipeDirection> SwipeDetected;
        public event Action<float> TapDetected;

        public void RaiseSwipe(SwipeDirection direction) => SwipeDetected?.Invoke(direction);

        public void RaiseTap(float normalizedX) => TapDetected?.Invoke(normalizedX);
    }
}
