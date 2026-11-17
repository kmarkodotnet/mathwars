// PlayerViewTests.cs
// F1-17 — PlayerView maps LaneIndex -> LaneAnchor.CenterX and animates the
// player's Transform toward it; this is the "játékos vizuális mozgatása
// sávváltáskor" piece F1-13 explicitly deferred to this task (no Player
// prefab/view existed yet — see that task's Napló).
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TestTools;
using MathHorde.Core.Common;
using MathHorde.GameLogic.Events;
using MathHorde.Presentation.Arena;
using MathHorde.Presentation.Events;
using MathHorde.Presentation.Player;
using NUnit.Framework;

namespace MathHorde.Tests.PlayMode.Player
{
    [TestFixture]
    public class PlayerViewTests
    {
        private readonly List<Object> _created = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            // See the identical note in GameplaySessionIntegrationTests.cs:
            // the one UnityTest below waits across many real-time frames,
            // long enough to risk coinciding with TextMeshPro's unrelated,
            // one-time delayed package-importer check that logs an error
            // under -nographics batch mode.
            LogAssert.ignoreFailingMessages = true;
        }

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;

            foreach (Object obj in _created)
            {
                Object.DestroyImmediate(obj);
            }

            _created.Clear();
        }

        private LaneAnchor CreateAnchor(int laneIndex, float centerX)
        {
            var go = new GameObject($"Lane_{laneIndex}");
            _created.Add(go);
            LaneAnchor anchor = go.AddComponent<LaneAnchor>();
            anchor.Initialize(laneIndex, centerX, width: 2.2f, length: 24f);
            return anchor;
        }

        private PlayerView CreatePlayerView()
        {
            var go = new GameObject("Player");
            _created.Add(go);
            return go.AddComponent<PlayerView>();
        }

        [Test]
        public void Initialize_SnapsImmediatelyToInitialLaneCenterX()
        {
            var anchors = new[] { CreateAnchor(0, -2.38f), CreateAnchor(1, 0f), CreateAnchor(2, 2.38f) };
            PlayerView view = CreatePlayerView();
            var bus = new GameEventBus();

            view.Initialize(bus, anchors, new LaneIndex(2));

            Assert.That(view.transform.position.x, Is.EqualTo(2.38f).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator PlayerLaneChanged_MovesTransformTowardsTheNewLaneCenterX()
        {
            var anchors = new[] { CreateAnchor(0, -2.38f), CreateAnchor(1, 0f), CreateAnchor(2, 2.38f) };
            PlayerView view = CreatePlayerView();
            var bus = new GameEventBus();
            view.Initialize(bus, anchors, new LaneIndex(0));

            bus.Publish(new PlayerLaneChanged(new LaneIndex(0), new LaneIndex(2), gameTime: 1.0));

            float deadline = Time.realtimeSinceStartup + 5f;
            while (!Mathf.Approximately(view.transform.position.x, 2.38f) && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(view.transform.position.x, Is.EqualTo(2.38f).Within(0.01f),
                "A játékos Transformja a sávváltás után a célsáv X-középpontjába kell, hogy kerüljön.");
        }

        [Test]
        public void Constructor_rejectsEmptyLaneAnchorList()
        {
            PlayerView view = CreatePlayerView();
            var bus = new GameEventBus();

            Assert.That(
                () => view.Initialize(bus, new LaneAnchor[0], new LaneIndex(0)),
                Throws.TypeOf<System.ArgumentException>());
        }
    }
}
