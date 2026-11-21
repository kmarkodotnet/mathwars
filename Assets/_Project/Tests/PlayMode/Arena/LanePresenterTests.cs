#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using MathHorde.Core.Common;
using MathHorde.Core.Expressions;
using MathHorde.Core.Levels;
using MathHorde.Core.Problems;
using MathHorde.Core.Progress;
using MathHorde.Core.Resources;
using MathHorde.Core.Skills;
using MathHorde.GameLogic;
using MathHorde.GameLogic.Events;
using MathHorde.MathEngine;
using MathHorde.Presentation.Arena;
using MathHorde.Presentation.Enemies;
using MathHorde.Presentation.Events;
using MathHorde.Presentation.Player;
using MathHorde.Tests.PlayMode.TestSupport;
using Object = UnityEngine.Object;

namespace MathHorde.Tests.PlayMode.Arena
{
    public class LanePresenterTests
    {
        private const string PrefabPath = "Assets/_Project/Prefabs/Arena/LanePresentation.prefab";
        private GameObject _root;
        private LanePresenter _presenter;
        private LaneHighlightView[] _views;
        private LaneAnchor[] _lanes;
        private GameEventBus _bus;
        private GameClock _clock;
        private ResourceService _resources;
        private CombatService _combat;
        private MathEventDirector _director;

        [TearDown]
        public void TearDown() { if (_root != null) Object.DestroyImmediate(_root); }

        private void Create(ProblemType type = ProblemType.AnswerLaneSelection, int laneCount = 4, int options = 2, int simultaneous = 1)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null);
            _root = Object.Instantiate(prefab);
            _presenter = _root.GetComponent<LanePresenter>();
            _views = _root.GetComponentsInChildren<LaneHighlightView>(true);
            _lanes = new LaneAnchor[laneCount];
            for (int i = 0; i < laneCount; i++)
            {
                _lanes[i] = new GameObject("Anchor" + i).AddComponent<LaneAnchor>();
                _lanes[i].transform.SetParent(_root.transform);
                _lanes[i].Initialize(i, (i - (laneCount - 1) * .5f) * 2.38f, 2.2f, 24);
            }
            _clock = new GameClock(); _bus = new GameEventBus();
            var entries = new MathEventScheduleEntry[simultaneous];
            var rewards = new Dictionary<int, RewardSpec>();
            for (int i = 0; i < simultaneous; i++)
            {
                entries[i] = new MathEventScheduleEntry(0, type, 1, 6, ResourceType.Health, GateGoal.Maximize);
                rewards[i] = new RewardSpec(ResourceType.Health, type == ProblemType.ResourceOperationComparison ? 0 : 10,
                    type == ProblemType.ResourceOperationComparison);
            }
            var level = new LevelDefinition(new LevelId("lane-test"), new WorldId("w1"), "level.test", laneCount, 200,
                null, entries, null, 20, 30, 20, 30, 0, "basic");
            _resources = new ResourceService(_bus, _clock); _resources.Initialize(level);
            _combat = new CombatService(_resources, _bus);
            _combat.Initialize(level, new WeaponDefinition("basic", 10, 10, 20, 0, TargetingMode.OwnLaneThenAdjacent, "fx"),
                new Dictionary<string, EnemyDefinition> { ["bot"] = new EnemyDefinition("bot", EnemyType.Basic, 10000, .1f, 1, 1, "bot") });
            var profile = new DifficultyProfile(1, 1, 1, 1, new[] { BinaryOperator.Addition, BinaryOperator.Modulo },
                (SkillTag[])Enum.GetValues(typeof(SkillTag)), 0, 100, 1000, 2, 2, false, 0, false, 0, false, options, 6, 1, false);
            _director = new MathEventDirector(_resources, _bus, new MathEventDirectorSettings(3, 5, 3, 2, 1, rewards));
            _director.Initialize(level, profile, new MathProblemGenerator(), new DeterministicRandomSource(7));
            var preparation = _director.PrepareUpcomingEvents(0);
            Assert.That(preparation.IsSuccess, Is.True, preparation.ErrorCode);
            _presenter.BindGeometry(_lanes, 3);
            _presenter.Initialize(_combat, _director, _bus);
        }

        private void Tick(float delta)
        {
            _clock.Advance(delta); _combat.Tick(_clock.GameTime, delta); _director.Tick(_clock.GameTime);
            _presenter.RenderPending();
        }

        private void OpenDecision() { Tick(0); Tick(1); Tick(1); }

        private int ExpectedDecisionMask()
        {
            int mask = 0;
            foreach (var item in _director.ActiveEvents)
                if (item.State == MathEventState.AwaitingDecision)
                    foreach (var carrier in item.Carriers) mask |= 1 << carrier.Lane.Value;
            return mask;
        }

        private void AssertDecisionMask(int mask)
        {
            for (int i = 0; i < _views.Length; i++)
                Assert.That(_views[i].IsDecisionLane, Is.EqualTo((mask & (1 << i)) != 0), "Lane " + i);
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void GeometryAndInitialSelectionUseTheArenaAnchors(int count)
        {
            Create(laneCount: count, options: count);
            Assert.That(_views.Length, Is.EqualTo(4), "All views are prebuilt.");
            foreach (var view in _views) Assert.That(view.IsSelected, Is.False, "No work in Initialize's event path.");
            _presenter.RenderPending();
            for (int i = 0; i < 4; i++)
            {
                Assert.That(_views[i].gameObject.activeSelf, Is.EqualTo(i < count));
                Assert.That(_views[i].IsSelected, Is.EqualTo(i == _combat.PlayerLane.Value));
                if (i >= count) continue;
                Assert.That(_views[i].transform.position.x, Is.EqualTo(_lanes[i].CenterX));
                var rail = _views[i].transform.Find("SelectedRailsAndChevron/LeftRail");
                Assert.That(rail.localScale.z, Is.EqualTo(_lanes[i].Length));
                Assert.That(rail.position.x, Is.EqualTo(_lanes[i].CenterX - _lanes[i].Width * .46f).Within(.001f));
                Assert.That(_views[i].transform.Find("DecisionWindowOutline").position.z, Is.EqualTo(3));
            }
            AssertDecisionMask(0);
        }

        [Test]
        public void ExistingControllerSwipeAndTapMoveTheActualCombatLaneAndOnlyDrainChangesMarkers()
        {
            Create(); _presenter.RenderPending();
            var input = new FakeLaneInputSource();
            _root.AddComponent<PlayerController>().Initialize(_combat, input, 4);
            input.RaiseSwipe(SwipeDirection.Right);
            Assert.That(_combat.PlayerLane.Value, Is.EqualTo(1));
            Assert.That(_views[0].IsSelected, Is.True);
            Assert.That(_views[1].IsSelected, Is.False);
            _presenter.RenderPending();
            Assert.That(_views[1].IsSelected, Is.True);
            input.RaiseTap(.95f); _presenter.RenderPending();
            for (int i = 0; i < 4; i++) Assert.That(_views[i].IsSelected, Is.EqualTo(i == 3));
            Assert.That(_combat.PlayerLane.Value, Is.EqualTo(3));
        }

        [TestCase(ProblemType.AnswerLaneSelection, 2)]
        [TestCase(ProblemType.AnswerLaneSelection, 3)]
        [TestCase(ProblemType.AnswerLaneSelection, 4)]
        [TestCase(ProblemType.ResourceOperationComparison, 2)]
        public void DecisionWindowHighlightsEveryParticipatingLaneWithoutGivingACorrectnessHint(ProblemType type, int options)
        {
            Create(type, options: options);
            Tick(0); AssertDecisionMask(0);
            Tick(1); AssertDecisionMask(0);
            _clock.Advance(1); _combat.Tick(2, 1); _director.Tick(2);
            AssertDecisionMask(0); // Synchronous decision event must only mark dirty.
            _presenter.RenderPending();
            AssertDecisionMask(ExpectedDecisionMask());
            Assert.That(_director.ActiveEvents[0].Carriers.Count, Is.EqualTo(options));
            foreach (var carrier in _director.ActiveEvents[0].Carriers)
            {
                var outline = _views[carrier.Lane.Value].transform.Find("DecisionWindowOutline");
                Assert.That(outline.gameObject.activeSelf, Is.True);
                Assert.That(outline.Find("NearEdge").GetComponent<MeshRenderer>().sharedMaterial,
                    Is.SameAs(_views[_director.ActiveEvents[0].Carriers[0].Lane.Value].transform.Find("DecisionWindowOutline/NearEdge").GetComponent<MeshRenderer>().sharedMaterial));
            }
            Assert.That(_director.PendingResolutions.Count, Is.EqualTo(0));
            Assert.That(_resources.GetCurrent(ResourceType.Health), Is.EqualTo(20));
        }

        [Test]
        public void ConcurrentWindowsUseTheUnionAndResolvingClearsEvenWithoutAnotherLaneEvent()
        {
            Create(simultaneous: 3); OpenDecision();
            Assert.That(_director.ActiveEvents.Count, Is.EqualTo(3));
            AssertDecisionMask(ExpectedDecisionMask());
            var item = _director.ActiveEvents[0];
            _director.OnPlayerLaneSelected(item.Carriers[0].Lane, 2);
            Assert.That(item.State, Is.EqualTo(MathEventState.Resolving));
            _presenter.RenderPending(); AssertDecisionMask(ExpectedDecisionMask());
            _director.OnRewardProcessed(new RewardApplication(item.EventId, RewardApplicationStatus.Applied, 10, 10, ResourceType.Health), 2);
            _presenter.RenderPending(); AssertDecisionMask(ExpectedDecisionMask());
            Tick(6); AssertDecisionMask(0);
            Tick(2); AssertDecisionMask(0);
        }

        [Test]
        public void ControllerChoiceForwardsToDirectorAndNeverResolvesOrRewardsFromPresenter()
        {
            Create(); OpenDecision();
            var input = new FakeLaneInputSource();
            _root.AddComponent<PlayerController>().Initialize(_combat, input, 4);
            _bus.Subscribe<PlayerLaneChanged>(e => _director.OnPlayerLaneSelected(e.To, e.GameTime));
            int target = -1;
            foreach (var carrier in _director.ActiveEvents[0].Carriers)
                if (carrier.Lane != _combat.PlayerLane) target = carrier.Lane.Value;
            Assert.That(target, Is.GreaterThanOrEqualTo(0));
            input.RaiseTap((target + .5f) / 4);
            _presenter.RenderPending(); AssertDecisionMask(0);
            Assert.That(_director.ActiveEvents[0].State, Is.EqualTo(MathEventState.Resolving));
            Assert.That(_director.PendingResolutions.Count, Is.EqualTo(1));
            Assert.That(_resources.GetCurrent(ResourceType.Health), Is.EqualTo(20));
            Assert.That(_views[target].IsSelected, Is.True);
        }

        [Test]
        public void PreparationFailureAndSessionEndClearWithoutAChildResult()
        {
            Create(ProblemType.ResourceOperationComparison); Tick(0);
            _resources.SetMax(ResourceType.Health, 40, "INVALID_ORDER");
            Tick(1); AssertDecisionMask(0);
            Assert.That(_director.ActiveEvents[0].State, Is.EqualTo(MathEventState.Expired));
            Assert.That(_director.PendingResolutions.Count, Is.EqualTo(0));
            _director.OnSessionEnded(1);
            _bus.Publish(new SessionEnded(default, SessionResult.Defeat, FailureReason.HealthDepleted, 1));
            _presenter.RenderPending();
            foreach (var view in _views) { Assert.That(view.IsSelected, Is.False); Assert.That(view.IsDecisionLane, Is.False); }
        }

        [Test]
        public void SessionEndDuringAWindowClearsBothMarkerShapes()
        {
            Create(); OpenDecision();
            _director.OnSessionEnded(2);
            _bus.Publish(new SessionEnded(default, SessionResult.Defeat, FailureReason.HealthDepleted, 2));
            _presenter.RenderPending();
            foreach (var view in _views) { Assert.That(view.IsSelected, Is.False); Assert.That(view.IsDecisionLane, Is.False); }
        }

        [Test]
        public void HandlersAndQuietDrainAllocateZeroAndDisposeUnsubscribes()
        {
            Create(); OpenDecision();
            var id = _director.ActiveEvents[0].EventId;
            Action publish = () =>
            {
                _bus.Publish(new PlayerLaneChanged(new LaneIndex(0), new LaneIndex(1), 2));
                _bus.Publish(new MathEventDecisionWindowOpened(id, 8, 2));
                _bus.Publish(new MathEventResolved(id, new LaneIndex(0), true, 2));
                _bus.Publish(new MathEventCompleted(id, 1, ResourceType.Health, 2));
                _bus.Publish(new MathEventExpired(id, "DEADLINE", 2));
                _bus.Publish(new MathEventPreparationFailed(id, "CONFIG", 2));
            };
            publish(); _presenter.RenderPending();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 100; i++) { publish(); _presenter.RenderPending(); }
            for (int i = 0; i < 100; i++) _presenter.RenderPending();
            for (int i = 0; i < 100; i++)
            {
                _bus.Publish(new SessionEnded(default, SessionResult.Defeat, FailureReason.HealthDepleted, 2));
                _presenter.RenderPending();
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.EqualTo(0));
            _presenter.Dispose(); _presenter.Dispose();
            var dirty = typeof(LanePresenter).GetField("_dirty", BindingFlags.NonPublic | BindingFlags.Instance);
            dirty.SetValue(_presenter, false); publish();
            _bus.Publish(new SessionEnded(default, SessionResult.Defeat, FailureReason.HealthDepleted, 2));
            _presenter.RenderPending();
            Assert.That((bool)dirty.GetValue(_presenter), Is.False);
            foreach (var view in _views) { Assert.That(view.IsSelected, Is.False); Assert.That(view.IsDecisionLane, Is.False); }
        }

        [Test]
        public void IncompleteBindingFailsBeforeSubscriptionAndCannotReinitializeADisposedPresenter()
        {
            Create();
            Assert.That(() => _presenter.Initialize(_combat, _director, _bus), Throws.TypeOf<InvalidOperationException>());
            Assert.That(() => _presenter.BindGeometry(_lanes, 3), Throws.TypeOf<InvalidOperationException>());
            _presenter.Dispose();
            Assert.That(() => _presenter.Initialize(_combat, _director, _bus), Throws.TypeOf<InvalidOperationException>());
            var unbound = new GameObject("Unbound"); unbound.transform.SetParent(_root.transform);
            var presenter = unbound.AddComponent<LanePresenter>();
            Assert.That(() => presenter.Initialize(_combat, _director, _bus), Throws.TypeOf<InvalidOperationException>());
            Assert.That(() => presenter.Initialize(null, _director, _bus), Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void InvalidAnchorOrderOrDepthIsRejectedBeforeBinding()
        {
            Create(); _presenter.Dispose();
            var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), _root.transform);
            var presenter = instance.GetComponent<LanePresenter>();
            Assert.That(() => presenter.BindGeometry(_lanes, float.NaN), Throws.TypeOf<ArgumentOutOfRangeException>());
            _lanes[1].Initialize(0, 0, 2, 24);
            Assert.That(() => presenter.BindGeometry(_lanes, 3), Throws.TypeOf<InvalidOperationException>());
        }

        [TestCase(1920)]
        [TestCase(2340)]
        public void PortraitProjectionKeepsTheOpenDecisionMarkersOnTheArenaFloor(int height)
        {
            Create(); OpenDecision();
            var camera = new GameObject("Camera").AddComponent<Camera>(); camera.transform.SetParent(_root.transform);
            camera.transform.position = new Vector3(0, 7.5f, -4.5f); camera.transform.LookAt(new Vector3(0, .5f, 13.2f));
            camera.fieldOfView = 55; camera.pixelRect = new Rect(0, 0, 1080, height);
            foreach (var view in _views)
            {
                var zone = view.transform.Find("DecisionWindowOutline");
                var viewport = camera.WorldToViewportPoint(zone.position);
                Assert.That(viewport.z, Is.GreaterThan(0));
                Assert.That(viewport.x, Is.InRange(0f, 1f));
                Assert.That(viewport.y, Is.InRange(0f, .66f));
                foreach (var renderer in view.GetComponentsInChildren<MeshRenderer>(true))
                    Assert.That(renderer.bounds.max.y, Is.LessThan(.15f), "No panel may cover combat above the floor.");
            }
        }

        [Test]
        public void VersionedPrefabHasDistinctShapesSharedUnlitMaterialsAndNoPhysicsOrInputTargets()
        {
            Create(); OpenDecision();
            Assert.That(_root.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(_root.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            Assert.That(_root.GetComponentsInChildren<EnemyView>(true), Is.Empty);
            Assert.That(_root.GetComponentsInChildren<GraphicRaycaster>(true), Is.Empty);
            Assert.That(_root.GetComponentsInChildren<Selectable>(true), Is.Empty);
            foreach (var transform in _root.GetComponentsInChildren<Transform>(true))
                if (transform.GetComponent<LaneAnchor>() == null) Assert.That(transform.gameObject.layer, Is.EqualTo(LayerMask.NameToLayer("Ignore Raycast")));
            foreach (var view in _views)
            {
                Assert.That(view.transform.Find("SelectedRailsAndChevron/Chevron/LeftArm"), Is.Not.Null);
                Assert.That(view.transform.Find("DecisionWindowOutline").childCount, Is.EqualTo(4));
                foreach (var renderer in view.GetComponentsInChildren<MeshRenderer>(true))
                    Assert.That(renderer.sharedMaterial.shader.name, Is.EqualTo("Universal Render Pipeline/Unlit"));
            }
        }

        [UnityTest]
        public IEnumerator AutomaticCombatContinuesOverRealFramesWhileTheDecisionWindowRemainsVisible()
        {
            Create(); OpenDecision();
            _combat.SpawnEnemy("bot", _combat.PlayerLane, 5);
            int shots = 0; _bus.Subscribe<WeaponFired>(_ => shots++);
            int viewCount = _root.GetComponentsInChildren<LaneHighlightView>(true).Length;
            for (int i = 0; i < 6; i++) { yield return null; Tick(.1f); }
            Assert.That(shots, Is.GreaterThan(0));
            Assert.That(_combat.ActiveEnemies[0].CurrentHealth, Is.LessThan(10000));
            Assert.That(_director.ActiveEvents[0].State, Is.EqualTo(MathEventState.AwaitingDecision));
            AssertDecisionMask(ExpectedDecisionMask());
            Assert.That(_root.GetComponentsInChildren<LaneHighlightView>(true).Length, Is.EqualTo(viewCount));
            Assert.That(_views[_combat.PlayerLane.Value].IsSelected, Is.True);
        }
    }
}
#endif
