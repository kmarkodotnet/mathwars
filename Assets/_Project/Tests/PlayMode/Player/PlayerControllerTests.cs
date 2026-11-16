// PlayerControllerTests.cs
// F1-13 — proves PlayerController does nothing but forward: it reads
// ICombatService.PlayerLane, asks LaneGestureResolver (already proven
// separately under Tests/EditMode/Common/LaneGestureResolverTests.cs) for
// the target lane, and calls ICombatService.MovePlayerToLane with exactly
// that result — never anything else on the fake (FakeCombatService throws
// on every other member). Lives under Tests/PlayMode because PlayerController
// is a MonoBehaviour in the Presentation assembly (same reason as
// ObjectPoolTests.cs/GameplayHudModelTests.cs).
using NUnit.Framework;
using UnityEngine;
using MathHorde.Core.Common;
using MathHorde.Presentation.Player;
using MathHorde.Tests.PlayMode.TestSupport;

namespace MathHorde.Tests.PlayMode.Player
{
    [TestFixture]
    public class PlayerControllerTests
    {
        private GameObject _gameObject;
        private PlayerController _controller;
        private FakeCombatService _combatService;
        private FakeLaneInputSource _inputSource;

        [SetUp]
        public void SetUp()
        {
            _gameObject = new GameObject("PlayerControllerUnderTest");
            _controller = _gameObject.AddComponent<PlayerController>();
            _combatService = new FakeCombatService();
            _inputSource = new FakeLaneInputSource();
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
        public void SwipeRight_FromMiddleLane_MovesToNextLane()
        {
            _combatService.PlayerLane = new LaneIndex(1);
            _controller.Initialize(_combatService, _inputSource, laneCount: 3);

            _inputSource.RaiseSwipe(SwipeDirection.Right);

            Assert.That(_combatService.MoveCallCount, Is.EqualTo(1));
            Assert.That(_combatService.LastRequestedLane.Value.Value, Is.EqualTo(2));
        }

        [Test]
        public void SwipeLeft_FromFirstLane_RequestsSameLane_NeverThrows()
        {
            _combatService.PlayerLane = new LaneIndex(0);
            _controller.Initialize(_combatService, _inputSource, laneCount: 3);

            _inputSource.RaiseSwipe(SwipeDirection.Left);

            Assert.That(_combatService.MoveCallCount, Is.EqualTo(1));
            Assert.That(_combatService.LastRequestedLane.Value.Value, Is.EqualTo(0));
        }

        [Test]
        public void ConsecutiveSwipes_ReadCurrentLaneFreshEachTime_NeverFromItsOwnState()
        {
            // PlayerController must not cache "current lane" itself
            // (game-logic.md 3.: ICombatService.PlayerLane is the single
            // source of truth) — this is only observable by having the fake
            // advance its own PlayerLane on every MovePlayerToLane call (as
            // the real CombatService does) and checking two consecutive
            // swipes land two lanes over, not one.
            _combatService.PlayerLane = new LaneIndex(0);
            _controller.Initialize(_combatService, _inputSource, laneCount: 3);

            _inputSource.RaiseSwipe(SwipeDirection.Right);
            _inputSource.RaiseSwipe(SwipeDirection.Right);

            Assert.That(_combatService.MoveCallCount, Is.EqualTo(2));
            Assert.That(_combatService.LastRequestedLane.Value.Value, Is.EqualTo(2));
        }

        [Test]
        public void Tap_ResolvesDirectlyToTappedLane()
        {
            _combatService.PlayerLane = new LaneIndex(0);
            _controller.Initialize(_combatService, _inputSource, laneCount: 3);

            _inputSource.RaiseTap(0.9f);

            Assert.That(_combatService.MoveCallCount, Is.EqualTo(1));
            Assert.That(_combatService.LastRequestedLane.Value.Value, Is.EqualTo(2));
        }

        [Test]
        public void Destroyed_StopsForwardingGestures()
        {
            _combatService.PlayerLane = new LaneIndex(0);
            _controller.Initialize(_combatService, _inputSource, laneCount: 3);

            Object.DestroyImmediate(_gameObject);
            _gameObject = null;

            _inputSource.RaiseSwipe(SwipeDirection.Right);

            Assert.That(_combatService.MoveCallCount, Is.EqualTo(0));
        }
    }
}
