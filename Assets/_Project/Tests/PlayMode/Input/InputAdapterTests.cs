// InputAdapterTests.cs
// F1-13 — proves InputAdapter.ApplyGesture (the one piece of its gesture
// classification not already covered by Tests/EditMode/Common/
// LaneGestureResolverTests.cs) raises exactly the right event for a tap vs.
// a swipe vs. an ambiguous drag, without needing to simulate a real Input
// System device — ApplyGesture is public precisely so tests can drive it
// with plain Vector2 positions (see InputAdapter.cs header comment).
using NUnit.Framework;
using UnityEngine;
using MathHorde.Core.Common;
using MathHorde.Infrastructure.Input;
using MathHorde.Tests.PlayMode.TestSupport;

namespace MathHorde.Tests.PlayMode.Input
{
    [TestFixture]
    public class InputAdapterTests
    {
        private const string MinSwipeDistanceField = "_minSwipeDistancePixels";

        private GameObject _gameObject;
        private InputAdapter _adapter;

        [SetUp]
        public void SetUp()
        {
            _gameObject = new GameObject("InputAdapterUnderTest");
            _adapter = _gameObject.AddComponent<InputAdapter>();
            ConfigTestReflection.SetField(_adapter, MinSwipeDistanceField, 60f);
        }

        [TearDown]
        public void TearDown()
        {
            if (_gameObject != null)
            {
                Object.DestroyImmediate(_gameObject);
            }
        }

        [Test]
        public void ApplyGesture_ShortMovement_RaisesTap_NotSwipe()
        {
            SwipeDirection? raisedSwipe = null;
            float? raisedTapX = null;
            _adapter.SwipeDetected += direction => raisedSwipe = direction;
            _adapter.TapDetected += x => raisedTapX = x;

            _adapter.ApplyGesture(new Vector2(100f, 200f), new Vector2(110f, 205f), screenWidth: 1080f);

            Assert.That(raisedSwipe, Is.Null);
            Assert.That(raisedTapX, Is.Not.Null);
            Assert.That(raisedTapX.Value, Is.EqualTo(110f / 1080f).Within(0.0001f));
        }

        [Test]
        public void ApplyGesture_HorizontalDragPastThreshold_RaisesSwipeRight_NotTap()
        {
            SwipeDirection? raisedSwipe = null;
            float? raisedTapX = null;
            _adapter.SwipeDetected += direction => raisedSwipe = direction;
            _adapter.TapDetected += x => raisedTapX = x;

            _adapter.ApplyGesture(new Vector2(100f, 200f), new Vector2(300f, 205f), screenWidth: 1080f);

            Assert.That(raisedSwipe, Is.EqualTo(SwipeDirection.Right));
            Assert.That(raisedTapX, Is.Null);
        }

        [Test]
        public void ApplyGesture_HorizontalDragNegativePastThreshold_RaisesSwipeLeft()
        {
            SwipeDirection? raisedSwipe = null;
            _adapter.SwipeDetected += direction => raisedSwipe = direction;

            _adapter.ApplyGesture(new Vector2(300f, 200f), new Vector2(100f, 205f), screenWidth: 1080f);

            Assert.That(raisedSwipe, Is.EqualTo(SwipeDirection.Left));
        }

        [Test]
        public void ApplyGesture_VerticalDominantDrag_RaisesNeitherEvent()
        {
            SwipeDirection? raisedSwipe = null;
            float? raisedTapX = null;
            _adapter.SwipeDetected += direction => raisedSwipe = direction;
            _adapter.TapDetected += x => raisedTapX = x;

            // Large movement on both axes, but vertical-dominant and also
            // past the "short movement" tap window -> ambiguous, not a tap
            // and not a lane-swipe (math-horde.md 3.3 only defines
            // horizontal swipes and taps; a vertical flick is neither).
            _adapter.ApplyGesture(new Vector2(100f, 200f), new Vector2(170f, 500f), screenWidth: 1080f);

            Assert.That(raisedSwipe, Is.Null);
            Assert.That(raisedTapX, Is.Null);
        }

        [Test]
        public void ApplyGesture_TapAtScreenEdge_NormalizedXClamped()
        {
            float? raisedTapX = null;
            _adapter.TapDetected += x => raisedTapX = x;

            _adapter.ApplyGesture(new Vector2(-50f, 200f), new Vector2(-50f, 205f), screenWidth: 1080f);

            Assert.That(raisedTapX, Is.Not.Null);
            Assert.That(raisedTapX.Value, Is.InRange(0f, 1f));
        }
    }
}
