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
using TMPro;
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
using MathHorde.Presentation.Gates;
using Object = UnityEngine.Object;

namespace MathHorde.Tests.PlayMode.Gates
{
    public class GatePresenterTests
    {
        private GameObject _root;
        private GatePresenter _presenter;
        private Camera _camera;
        private GameEventBus _bus;
        private ResourceService _resources;
        private MathEventDirector _director;
        private LevelDefinition _level;

        [TearDown]
        public void TearDown() { if (_root != null) Object.DestroyImmediate(_root); }

        private void Create(ProblemType type = ProblemType.ResourceOperationComparison, int count = 2,
            GateGoal goal = GateGoal.Maximize, ResourceType resource = ResourceType.Ammo, bool repeated = false)
        {
            _root = new GameObject("GatePresenterTest");
            _camera = new GameObject("Camera").AddComponent<Camera>();
            _camera.transform.SetParent(_root.transform);
            _camera.transform.position = new Vector3(0, 7.5f, -4.5f);
            _camera.transform.LookAt(new Vector3(0, .5f, 13.2f));
            _camera.fieldOfView = 55;
            _camera.aspect = 9f / 16;
            _presenter = _root.AddComponent<GatePresenter>();
            Set("_resourceGatePrefab", Prefab("ResourceGate"));
            Set("_answerCarrierPrefab", Prefab("AnswerCarrier"));
            Set("_camera", _camera);
            var lanes = new LaneAnchor[4];
            for (int i = 0; i < 4; i++)
            {
                lanes[i] = new GameObject("Lane" + i).AddComponent<LaneAnchor>();
                lanes[i].transform.SetParent(_root.transform);
                lanes[i].Initialize(i, (i - 1.5f) * 2.38f, 2.2f, 24);
            }
            Set("_lanes", lanes);
            _bus = new GameEventBus();
            var entry = new MathEventScheduleEntry(0, type, 1, 6, resource, goal);
            var entries = repeated ? new[] { entry, new MathEventScheduleEntry(10, type, 1, 6, resource, goal) } : new[] { entry };
            _level = new LevelDefinition(new LevelId("gate-test"), new WorldId("w1"), "level.test", 4, 200,
                null, entries, null, 20, 30, 20, 30, 20, 30, 0, "basic");
            _resources = new ResourceService(_bus, new GameClock());
            _resources.Initialize(_level);
            if (resource == ResourceType.EnemyArmor)
            {
                _resources.SetMax(resource, 30, "LEVEL_INIT");
                _resources.ApplyDelta(resource, 20, "LEVEL_INIT");
            }
            var rewards = new Dictionary<int, RewardSpec>();
            var targets = new Dictionary<int, int>();
            for (int i = 0; i < entries.Length; i++)
            {
                rewards[i] = new RewardSpec(resource, type == ProblemType.ResourceOperationComparison ? 0 : 10, type == ProblemType.ResourceOperationComparison);
                if (goal == GateGoal.ReachTarget || type == ProblemType.TargetValueSelection) targets[i] = 0;
            }
            _director = new MathEventDirector(_resources, _bus, new MathEventDirectorSettings(3, 5, 3, 2, 1, rewards, targets));
            // Four operations must stay distinct at snapshot zero as well.
            bool subtraction = goal == GateGoal.Minimize || (type == ProblemType.ResourceOperationComparison && count > 2);
            var operators = subtraction ? new[] { BinaryOperator.Subtraction } : new[] { BinaryOperator.Addition, BinaryOperator.Modulo };
            var profile = new DifficultyProfile(1, 1, 1, 1, operators,
                (SkillTag[])Enum.GetValues(typeof(SkillTag)), 0, 100, 1000, 2, 2, false, 0, false, 0, subtraction, count, 6, 1, false);
            _director.Initialize(_level, profile, new MathProblemGenerator(), new DeterministicRandomSource(7));
            var preparation = _director.PrepareUpcomingEvents(0);
            Assert.That(preparation.IsSuccess, Is.True, preparation.ErrorCode);
            _presenter.Initialize(_director, _bus);
        }

        private void Set(string field, object value) => typeof(GatePresenter).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_presenter, value);
        private static GateCarrierView Prefab(string name) => AssetDatabase.LoadAssetAtPath<GateCarrierView>("Assets/_Project/Prefabs/Gates/" + name + ".prefab");
        private GateCarrierView[] Views() => _root.GetComponentsInChildren<GateCarrierView>();
        private void Spawn() { _director.Tick(0); _presenter.RenderPending(); _director.Tick(1); _presenter.RenderPending(); }

        [TestCase("ResourceGate")]
        [TestCase("AnswerCarrier")]
        public void VersionedPrefabHasReadableTmpAndNoShootingOrPointerTarget(string name)
        {
            var prefab = Prefab(name);
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponentsInChildren<Collider>(true).Length, Is.EqualTo(0));
            Assert.That(prefab.GetComponentsInChildren<Collider2D>(true).Length, Is.EqualTo(0));
            Assert.That(prefab.GetComponentsInChildren<EnemyView>(true).Length, Is.EqualTo(0));
            Assert.That(prefab.GetComponentsInChildren<Selectable>(true).Length, Is.EqualTo(0));
            Assert.That(prefab.GetComponentsInChildren<GraphicRaycaster>(true).Length, Is.EqualTo(0));
            Assert.That(prefab.GetComponent<Canvas>().renderMode, Is.EqualTo(RenderMode.WorldSpace));
            foreach (var child in prefab.GetComponentsInChildren<Transform>(true))
                Assert.That(child.gameObject.layer, Is.EqualTo(LayerMask.NameToLayer("Ignore Raycast")));
            foreach (var graphic in prefab.GetComponentsInChildren<Graphic>(true)) Assert.That(graphic.raycastTarget, Is.False);
            var label = prefab.transform.Find("Option").GetComponent<TMP_Text>();
            Assert.That(label.font, Is.Not.Null);
            Assert.That(label.fontSizeMin, Is.GreaterThanOrEqualTo(48));
            Assert.That(label.fontSizeMax, Is.GreaterThanOrEqualTo(96));
        }

        [TestCase(ProblemType.ResourceOperationComparison, 1920)]
        [TestCase(ProblemType.ResourceOperationComparison, 2340)]
        [TestCase(ProblemType.AnswerLaneSelection, 1920)]
        [TestCase(ProblemType.AnswerLaneSelection, 2340)]
        public void LabelContentFitsAtStandardAndTallPortraitAspect(ProblemType type, int height)
        {
            Create(type, count: 4);
            _camera.pixelRect = new Rect(0, 0, 1080, height);
            Spawn();
            Canvas.ForceUpdateCanvases();
            foreach (var view in Views())
            {
                foreach (var text in view.GetComponentsInChildren<TMP_Text>())
                {
                    text.ForceMeshUpdate();
                    Assert.That(text.isTextOverflowing, Is.False, text.text);
                    Assert.That(text.fontSize, Is.GreaterThanOrEqualTo(text.fontSizeMin));
                }
            }
        }

        [TestCase(GateGoal.Maximize, ResourceType.Ammo, "Lőszer: 17\nLegtöbb", "MaximizeIcon")]
        [TestCase(GateGoal.Minimize, ResourceType.EnemyArmor, "Páncél: 17\nLegkevesebb", "MinimizeIcon")]
        [TestCase(GateGoal.ReachTarget, ResourceType.Shield, "Pajzs: 17\nCél: 0", "TargetIcon")]
        public void ResourcePreviewIsEmptyAndSpawnShowsFrozenSnapshotGoalIconAndOnlyOperation(GateGoal goal, ResourceType resource, string context, string icon)
        {
            Create(goal: goal, resource: resource);
            _director.Tick(0);
            _presenter.RenderPending();
            Assert.That(Views().Length, Is.EqualTo(0));
            Assert.That(_director.ActiveEvents[0].Problem, Is.Null);
            _resources.ApplyDelta(resource, -3, "WEAPON_FIRE");
            _director.Tick(1);
            Assert.That(Views().Length, Is.EqualTo(0), "Synchronous Tick handlers must not touch UI.");
            _presenter.RenderPending();
            var active = _director.ActiveEvents[0];
            var views = Views();
            Assert.That(views.Length, Is.EqualTo(active.Carriers.Count));
            foreach (var view in views)
            {
                Assert.That(view.ContextText, Is.EqualTo(context));
                Assert.That(view.OptionText, Is.EqualTo(active.Carriers[view.OptionIndex].DisplayLabel));
                Assert.That(view.OptionText.Contains("="), Is.False, "No result before a choice.");
                Assert.That(view.transform.Find(icon).gameObject.activeSelf, Is.True);
            }
            _resources.ApplyDelta(resource, -2, "WEAPON_FIRE");
            _presenter.RenderPending();
            foreach (var view in views) Assert.That(view.ContextText, Is.EqualTo(context));
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void AnswerCarriersUseAllOptionLabelsAndArenaLaneCentersAndMoveWithoutEvents(int count)
        {
            Create(ProblemType.AnswerLaneSelection, count);
            Spawn();
            var views = Views();
            Assert.That(views.Length, Is.EqualTo(count));
            int created = _presenter.CreatedCarrierCount;
            var active = _director.ActiveEvents[0];
            foreach (var view in views)
            {
                var carrier = active.Carriers[view.OptionIndex];
                Assert.That(view.OptionText, Is.EqualTo(carrier.DisplayLabel));
                Assert.That(view.ContextText, Is.Empty);
                Assert.That(view.transform.position.x, Is.EqualTo((carrier.Lane.Value - 1.5f) * 2.38f).Within(.001f));
                Assert.That(view.transform.position.z, Is.EqualTo(5));
                Assert.That(Quaternion.Angle(view.transform.rotation, _camera.transform.rotation), Is.LessThan(.001f));
            }
            _director.Tick(1.5);
            _presenter.RenderPending();
            foreach (var view in views) Assert.That(view.transform.position.z, Is.EqualTo(4));
            Assert.That(_presenter.CreatedCarrierCount, Is.EqualTo(created));
        }

        [Test]
        public void ChoiceExpirySessionEndAndRepeatedEventsReturnAndReusePrewarmedViews()
        {
            Create(repeated: true);
            Spawn();
            var first = Views();
            int created = _presenter.CreatedCarrierCount;
            _director.Tick(2);
            _director.OnPlayerLaneSelected(_director.ActiveEvents[0].Carriers[0].Lane, 2);
            Assert.That(Views().Length, Is.EqualTo(first.Length));
            _presenter.RenderPending();
            Assert.That(Views().Length, Is.EqualTo(0));
            _director.OnRewardProcessed(new RewardApplication(_director.ActiveEvents[0].EventId, RewardApplicationStatus.Applied, 1, 1, ResourceType.Ammo), 2);
            _director.Tick(10); _director.Tick(11); _presenter.RenderPending();
            Assert.That(Views().Length, Is.EqualTo(2));
            foreach (var view in Views()) Assert.That(Array.IndexOf(first, view), Is.GreaterThanOrEqualTo(0));
            _director.Tick(18); _presenter.RenderPending();
            Assert.That(_presenter.ActiveCarrierCount, Is.EqualTo(0));
            Assert.That(_presenter.CreatedCarrierCount, Is.EqualTo(created));
            _director.OnSessionEnded(18); _presenter.RenderPending();
            Assert.That(_presenter.ActiveCarrierCount, Is.EqualTo(0));
        }

        [Test]
        public void PreparationFailureHidesNoResultAndProducesNoChildFeedback()
        {
            Create(); _director.Tick(0); _presenter.RenderPending();
            int resolved = 0;
            _bus.Subscribe<MathEventResolved>(e => resolved++);
            _resources.SetMax(ResourceType.Ammo, 40, "INVALID_ORDER");
            _director.Tick(1); _presenter.RenderPending();
            Assert.That(_director.ActiveEvents[0].State, Is.EqualTo(MathEventState.Expired));
            Assert.That(_presenter.ActiveCarrierCount, Is.EqualTo(0));
            Assert.That(resolved, Is.EqualTo(0));
        }

        [Test]
        public void AllTickHandlersAllocateZeroAndDisposeUnsubscribesAndClearsBindings()
        {
            Create(); Spawn();
            var id = _director.ActiveEvents[0].EventId;
            Action publish = () =>
            {
                _bus.Publish(new MathEventPreviewStarted(id, ProblemType.ResourceOperationComparison, null, string.Empty, ResourceType.Ammo, GateGoal.Maximize, null, 1));
                _bus.Publish(new MathEventGatesSpawned(id, 20, ResourceType.Ammo, 1));
                _bus.Publish(new MathEventDecisionWindowOpened(id, 8, 2));
                _bus.Publish(new MathEventResolved(id, new LaneIndex(0), true, 2));
                _bus.Publish(new MathEventCompleted(id, 10, ResourceType.Ammo, 2));
                _bus.Publish(new MathEventExpired(id, "DEADLINE", 8));
                _bus.Publish(new MathEventPreparationFailed(id, "CONFIG", 8));
            };
            publish();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 100; i++) publish();
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(bytes, Is.EqualTo(0));
            _presenter.Dispose(); _presenter.Dispose();
            publish(); _presenter.RenderPending();
            Assert.That(Views().Length, Is.EqualTo(0));
            foreach (var view in _root.GetComponentsInChildren<GateCarrierView>(true))
            {
                Assert.That(view.OptionIndex, Is.EqualTo(-1));
                Assert.That(view.OptionText, Is.Empty);
                Assert.That(view.ContextText, Is.Empty);
            }
            Assert.That((bool)typeof(GatePresenter).GetField("_dirty", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(_presenter), Is.True);
            // Reset the flag after disposal: publishing again must not reach the old handlers.
            Set("_dirty", false); publish();
            Assert.That((bool)typeof(GatePresenter).GetField("_dirty", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(_presenter), Is.False);
        }

        [Test]
        public void MissingWiringFailsBeforeSubscriptionOrSpawning()
        {
            _root = new GameObject("UnwiredPresenter");
            _presenter = _root.AddComponent<GatePresenter>();
            Assert.That(() => _presenter.Initialize(new NullMathEventDirector(), new GameEventBus()), Throws.TypeOf<InvalidOperationException>());
            Assert.That(() => _presenter.Initialize(null, new GameEventBus()), Throws.TypeOf<ArgumentNullException>());
        }

        [UnityTest]
        public IEnumerator ContinuousAutomaticFireOnlyDamagesCombatEnemyAndLeavesMathCarriersAlive()
        {
            Create(); Spawn(); _director.Tick(2); _presenter.RenderPending();
            var combat = new CombatService(_resources, _bus);
            var enemy = new EnemyDefinition("bot", EnemyType.Basic, 10000, .1f, 1, 1, 0, "bot");
            combat.Initialize(_level, new WeaponDefinition("basic", 10, 10, 20, 1, 0, TargetingMode.OwnLaneThenAdjacent, "fx"),
                new Dictionary<string, EnemyDefinition> { ["bot"] = enemy });
            combat.SpawnEnemy("bot", new LaneIndex(0), 5);
            for (int i = 1; i <= 5; i++)
            {
                yield return null;
                double now = 2 + i * .1;
                combat.Tick(now, .1f);
                _director.Tick(now);
                _presenter.RenderPending();
            }
            Assert.That(_resources.GetCurrent(ResourceType.Ammo), Is.LessThan(20));
            Assert.That(combat.ActiveEnemies[0].CurrentHealth, Is.LessThan(10000));
            Assert.That(_presenter.ActiveCarrierCount, Is.EqualTo(2));
            Assert.That(_director.ActiveEvents[0].State, Is.EqualTo(MathEventState.AwaitingDecision));
        }
    }
}
#endif
