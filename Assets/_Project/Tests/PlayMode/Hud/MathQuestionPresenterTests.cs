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
using MathHorde.Presentation.Events;
using MathHorde.Presentation.Hud;
using MathHorde.Presentation.UI;
using Object = UnityEngine.Object;

namespace MathHorde.Tests.PlayMode.Hud
{
    public class MathQuestionPresenterTests
    {
        private const string Path = "Assets/_Project/Prefabs/UI/MathQuestionHUD.prefab";
        private GameObject _root;
        private MathQuestionPresenter _presenter;
        private MathEventDirector _director;
        private GameEventBus _bus;
        private GameClock _clock;
        private ResourceService _resources;
        private LevelDefinition _level;

        [TearDown]
        public void TearDown() { if (_root != null) Object.DestroyImmediate(_root); }

        private void Create(ProblemType type = ProblemType.AnswerLaneSelection, GateGoal goal = GateGoal.Maximize,
            ResourceType resource = ResourceType.Health, int simultaneous = 1)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Path);
            Assert.That(prefab, Is.Not.Null);
            _root = Object.Instantiate(prefab);
            _presenter = _root.GetComponent<MathQuestionPresenter>();
            _clock = new GameClock(); _bus = new GameEventBus();
            var entries = new MathEventScheduleEntry[simultaneous];
            var rewards = new Dictionary<int, RewardSpec>();
            var targets = new Dictionary<int, int>();
            for (int i = 0; i < simultaneous; i++)
            {
                entries[i] = new MathEventScheduleEntry(0, type, 1, 6, resource, goal);
                rewards[i] = new RewardSpec(resource, type == ProblemType.ResourceOperationComparison ? 0 : 10,
                    type == ProblemType.ResourceOperationComparison);
                if (goal == GateGoal.ReachTarget || type == ProblemType.TargetValueSelection) targets[i] = 0;
            }
            _level = new LevelDefinition(new LevelId("question-test"), new WorldId("w1"), "level.test", 4, 200,
                null, entries, null, 20, 30, 20, 30, 0, "basic", availableWeaponIds: new[] { "basic" });
            _resources = new ResourceService(_bus, _clock); _resources.Initialize(_level);
            if (resource == ResourceType.EnemyArmor)
            {
                _resources.SetMax(resource, 30, "LEVEL_INIT");
                _resources.ApplyDelta(resource, 20, "LEVEL_INIT");
            }
            var profile = new DifficultyProfile(1, 1, 1, 1,
                goal == GateGoal.Minimize ? new[] { BinaryOperator.Subtraction } : new[] { BinaryOperator.Addition, BinaryOperator.Modulo },
                (SkillTag[])Enum.GetValues(typeof(SkillTag)), 0, 100, 1000, 2, 2, false, 0, false, 0, goal == GateGoal.Minimize, 2, 6, 1, false);
            _director = new MathEventDirector(_resources, _bus, new MathEventDirectorSettings(3, 5, 3, 2, 1, rewards, targets));
            _director.Initialize(_level, profile, new MathProblemGenerator(), new DeterministicRandomSource(7));
            var preparation = _director.PrepareUpcomingEvents(0);
            Assert.That(preparation.IsSuccess, Is.True, preparation.ErrorCode);
            _presenter.BindClock(_clock); _presenter.Initialize(_director, _bus);
        }

        private MathQuestionView[] Visible() => _root.GetComponentsInChildren<MathQuestionView>();
        private void Tick(float delta)
        {
            _clock.Advance(delta); _director.Tick(_clock.GameTime); _presenter.RenderPending();
        }

        [Test]
        public void FullExpressionAppearsAtPreviewAndDoesNotRevealAnswerOrOptions()
        {
            Create();
            _director.Tick(0);
            Assert.That(Visible().Length, Is.EqualTo(0), "Synchronous event handler must not touch the UI.");
            _presenter.RenderPending();
            var item = _director.ActiveEvents[0];
            Assert.That(item.State, Is.EqualTo(MathEventState.Preview));
            Assert.That(item.Carriers.Count, Is.EqualTo(0));
            Assert.That(Visible()[0].QuestionText, Is.EqualTo(item.Problem.DisplayExpression));
            Assert.That(Visible()[0].TimeText, Is.EqualTo("Készülj!"));
            Tick(1);
            Assert.That(Visible()[0].QuestionText, Is.EqualTo(item.Problem.DisplayExpression));
            Assert.That(Visible()[0].TimeText, Is.EqualTo("Közeledik"));
        }

        [TestCase(GateGoal.Maximize, "Legtöbb", ResourceType.Health, "Életerő")]
        [TestCase(GateGoal.Minimize, "Legkevesebb", ResourceType.EnemyArmor, "Páncél")]
        [TestCase(GateGoal.Maximize, "Legtöbb", ResourceType.Health, "Életerő")]
        [TestCase(GateGoal.ReachTarget, "Cél: 0", ResourceType.Shield, "Pajzs")]
        public void ResourcePreviewContainsOnlyResourceAndGoalThenShowsFrozenSpawnSnapshot(GateGoal goal, string label, ResourceType resource, string name)
        {
            Create(ProblemType.ResourceOperationComparison, goal, resource);
            Tick(0);
            var item = _director.ActiveEvents[0];
            Assert.That(item.Problem, Is.Null);
            Assert.That(Visible()[0].QuestionText, Is.EqualTo(name + "\n" + label));
            _resources.ApplyDelta(resource, -7, "TEST");
            Tick(1);
            string expected = name + ": 13\n" + label;
            Assert.That(Visible()[0].QuestionText, Is.EqualTo(expected));
            _resources.ApplyDelta(resource, -5, "TEST");
            Tick(.25f);
            Assert.That(Visible()[0].QuestionText, Is.EqualTo(expected));
            // No option result, operation, Explanation or correctness is bound to this view.
            Assert.That(Visible()[0].GetComponentsInChildren<TMP_Text>().Length, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator CountdownUsesSessionGameTimeAndFreezesWhilePaused()
        {
            Create(); Tick(0); Tick(1); Tick(1);
            var view = Visible()[0];
            Assert.That(view.TimeText, Is.EqualTo("6 mp"));
            Assert.That(view.TimeFraction, Is.EqualTo(1).Within(.001));
            Assert.That(view.transform.Find("TimeFill").GetComponent<Image>().sprite, Is.Not.Null,
                "The filled timer must have a sprite so its rendered geometry follows fillAmount.");
            Tick(1.25f);
            Assert.That(view.TimeText, Is.EqualTo("5 mp"));
            _clock.Pause();
            for (int i = 0; i < 3; i++) { yield return null; Tick(1); }
            Assert.That(view.TimeText, Is.EqualTo("5 mp"));
            _clock.Resume(); Tick(1);
            Assert.That(view.TimeText, Is.EqualTo("4 mp"));
            Tick(10);
            Assert.That(Visible().Length, Is.EqualTo(0));
            foreach (var row in _root.GetComponentsInChildren<MathQuestionView>(true))
            {
                Assert.That(row.QuestionText, Is.Empty);
                Assert.That(row.TimeText, Is.Empty);
                Assert.That(row.TimeFraction, Is.EqualTo(0));
            }
        }

        [Test]
        public void CountdownReflectsDirectorTimePressureRatherThanUnscaledProblemTime()
        {
            Create(); _director.ApplyTimePressure(.5f, 1);
            Tick(0); Tick(1); Tick(1);
            Assert.That(_director.ActiveEvents[0].Problem.ThinkTimeSeconds, Is.EqualTo(6));
            Assert.That(Visible()[0].TimeText, Is.EqualTo("3 mp"));
        }

        [Test]
        public void ThreeConcurrentQuestionsArePreallocatedAndClearOnSessionEnd()
        {
            Create(simultaneous: 3); Tick(0);
            Assert.That(Visible().Length, Is.EqualTo(3));
            Assert.That(_root.GetComponentsInChildren<MathQuestionView>(true).Length, Is.EqualTo(3));
            _director.OnSessionEnded(0); _presenter.RenderPending();
            Assert.That(Visible().Length, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator CombatContinuesWhileQuestionAndCountdownAreVisible()
        {
            Create(); Tick(0); Tick(1); Tick(1);
            var combat = new CombatService(_resources, _bus);
            var enemy = new EnemyDefinition("bot", EnemyType.Basic, 10000, .1f, 1, 1, "bot");
            combat.Initialize(_level, new[] { new WeaponDefinition("basic", 10, 10, 20, 0, TargetingMode.OwnLaneThenAdjacent, "fx", displayNameKey: "weapon.test") },
                new Dictionary<string, EnemyDefinition> { ["bot"] = enemy });
            combat.SpawnEnemy("bot", new LaneIndex(0), 5);
            int shots = 0; _bus.Subscribe<WeaponFired>(_ => shots++);
            for (int i = 0; i < 5; i++)
            {
                yield return null;
                _clock.Advance(.1f); combat.Tick(_clock.GameTime, .1f);
                _director.Tick(_clock.GameTime); _presenter.RenderPending();
            }
            Assert.That(Visible().Length, Is.EqualTo(1));
            Assert.That(shots, Is.GreaterThan(0));
            Assert.That(combat.ActiveEnemies[0].CurrentHealth, Is.LessThan(10000));
        }

        [Test]
        public void SessionEndEventClearsWithoutWaitingForAnotherDirectorTick()
        {
            Create(); Tick(0);
            _bus.Publish(new SessionEnded(default, SessionResult.Victory, default, 0));
            Assert.That(Visible().Length, Is.EqualTo(1));
            _presenter.RenderPending();
            Assert.That(Visible().Length, Is.EqualTo(0));
        }

        [Test]
        public void SelectionClearsQuestionBeforeFeedbackAndPreparationFailureClearsPreview()
        {
            Create(); Tick(0); Tick(1); Tick(1);
            var item = _director.ActiveEvents[0];
            _director.OnPlayerLaneSelected(item.Carriers[0].Lane, 2);
            Assert.That(Visible().Length, Is.EqualTo(1));
            _presenter.RenderPending();
            Assert.That(Visible().Length, Is.EqualTo(0));
            Object.DestroyImmediate(_root);
            Create(ProblemType.ResourceOperationComparison); Tick(0);
            _resources.SetMax(ResourceType.Health, 31, "TEST");
            _director.Tick(1); _presenter.RenderPending();
            Assert.That(_director.ActiveEvents[0].State, Is.EqualTo(MathEventState.Expired));
            Assert.That(Visible().Length, Is.EqualTo(0));
        }

        [Test]
        public void EventHandlersAndQuietDrainAllocateNothingAndDisposeUnsubscribes()
        {
            Create(); Tick(0);
            var item = _director.ActiveEvents[0];
            Action publish = () =>
            {
                _bus.Publish(new MathEventPreviewStarted(item.EventId, item.Type, item.ProblemId, "DO NOT RENDER", item.RewardResource, item.Goal, null, 0));
                _bus.Publish(new MathEventGatesSpawned(item.EventId, 0, item.RewardResource, 0));
                _bus.Publish(new MathEventDecisionWindowOpened(item.EventId, 9, 0));
                _bus.Publish(new MathEventResolved(item.EventId, new LaneIndex(0), true, 0));
                _bus.Publish(new MathEventCompleted(item.EventId, 0, item.RewardResource, 0));
                _bus.Publish(new MathEventExpired(item.EventId, "TEST", 0));
                _bus.Publish(new MathEventPreparationFailed(item.EventId, "TEST", 0));
            };
            publish();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 128; i++) publish();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.EqualTo(0));
            Assert.That(Visible()[0].QuestionText, Is.EqualTo(item.Problem.DisplayExpression));
            _presenter.RenderPending(); _presenter.RenderPending();
            before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 128; i++) _presenter.RenderPending();
            allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.EqualTo(0));
            _presenter.Dispose(); _presenter.Dispose();
            var dirty = typeof(MathQuestionPresenter).GetField("_dirty", BindingFlags.NonPublic | BindingFlags.Instance);
            dirty.SetValue(_presenter, false); publish();
            _presenter.RenderPending();
            Assert.That((bool)dirty.GetValue(_presenter), Is.False);
            Assert.That(Visible().Length, Is.EqualTo(0));
        }

        [TestCase(1920, false)]
        [TestCase(2340, true)]
        public void PortraitAndNotchLayoutKeepsAllContentInHudBandAndReadable(int height, bool notch)
        {
            Create(simultaneous: 3); Tick(0);
            // Deterministic safe-area input without changing the Editor's global Screen size.
            var canvas = _root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
            var rootRect = (RectTransform)_root.transform; rootRect.sizeDelta = new Vector2(1080, height);
            var fitter = _root.GetComponentInChildren<SafeAreaFitter>(); fitter.enabled = false;
            var safe = (RectTransform)fitter.transform;
            safe.anchorMin = new Vector2(0, notch ? 80f / height : 0);
            safe.anchorMax = new Vector2(1, notch ? 1 - 120f / height : 1);
            var band = (RectTransform)safe.Find("QuestionBand");
            Assert.That(band.anchorMin.y, Is.GreaterThanOrEqualTo(.66f));
            Assert.That(band.anchorMax.y, Is.LessThanOrEqualTo(.90f));
            Canvas.ForceUpdateCanvases();
            Visible()[0].SetQuestion("(? + 17) × 3 = 6 × (14 − 3)");
            foreach (var view in Visible())
            {
                foreach (var text in view.GetComponentsInChildren<TMP_Text>())
                {
                    text.ForceMeshUpdate();
                    Assert.That(text.fontSize, Is.GreaterThanOrEqualTo(26));
                    Assert.That(text.isTextOverflowing, Is.False, text.text);
                    Assert.That(text.font, Is.Not.Null);
                }
            }
            Assert.That(_root.GetComponentsInChildren<Selectable>(true).Length, Is.EqualTo(0));
            Assert.That(_root.GetComponentsInChildren<GraphicRaycaster>(true).Length, Is.EqualTo(0));
            foreach (var graphic in _root.GetComponentsInChildren<Graphic>(true)) Assert.That(graphic.raycastTarget, Is.False);
        }

        [TestCase("7 × 8 = ?")]
        [TestCase("83 − 47 = ?")]
        [TestCase("72 ÷ 9 = ?")]
        [TestCase("(? + 17) × 3 = 6 × (14 − 3)")]
        public void MathNotationHasPrebakedGlyphs(string expression)
        {
            Assert.That(TMP_Settings.defaultFontAsset.HasCharacters(expression, out uint[] _, false, false), Is.True);
        }

        [Test]
        public void MissingClockAndWiringFailBeforeSubscribing()
        {
            _root = new GameObject("UnwiredQuestion");
            _presenter = _root.AddComponent<MathQuestionPresenter>();
            Assert.That(() => _presenter.Initialize(new NullMathEventDirector(), new GameEventBus()), Throws.TypeOf<InvalidOperationException>());
            Assert.That(() => _presenter.Initialize(null, new GameEventBus()), Throws.TypeOf<ArgumentNullException>());
        }
        [TestCase(false)]
        [TestCase(true)]
        public void LegacyAmmoEventOrPreparedRewardIsRejectedBeforeDrawing(bool rewardField)
        {
            Create(); _director.Tick(0); _director.Tick(1);
            var item = _director.ActiveEvents[0];
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            if (rewardField)
                typeof(RewardSpec).GetField("<Resource>k__BackingField", flags)
                    .SetValue(item.Problem.Reward, ResourceType.Ammo);
            else
                typeof(ActiveMathEvent).GetField("<RewardResource>k__BackingField", flags).SetValue(item, ResourceType.Ammo);
            Assert.That(() => _presenter.RenderPending(), Throws.TypeOf<ArgumentException>());
            Assert.That(Visible().Length, Is.EqualTo(0));
        }

    }
}
#endif
