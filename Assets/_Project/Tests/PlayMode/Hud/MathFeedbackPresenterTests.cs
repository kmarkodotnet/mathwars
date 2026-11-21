#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
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
using MathHorde.Presentation.Events;
using MathHorde.Presentation.Hud;
using MathHorde.Presentation.Player;
using MathHorde.Presentation.UI;
using MathHorde.Tests.PlayMode.TestSupport;
using Object = UnityEngine.Object;

namespace MathHorde.Tests.PlayMode.Hud
{
    public class MathFeedbackPresenterTests
    {
        private GameObject _root;
        private MathFeedbackPresenter _presenter;
        private MathEventDirector _director;
        private GameEventBus _bus;
        private GameClock _clock;
        private ResourceService _resources;
        private GateResolutionService _resolution;
        private RewardService _rewards;
        private LevelDefinition _level;

        [TearDown]
        public void TearDown() { if (_root != null) Object.DestroyImmediate(_root); }

        private void Create(ProblemType type = ProblemType.ResourceOperationComparison, int lanes = 2,
            GateGoal goal = GateGoal.Maximize, int simultaneous = 1, int scheduled = 1, int? capacity = null)
        {
            _root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/MathFeedbackHUD.prefab"));
            _presenter = _root.GetComponent<MathFeedbackPresenter>();
            _bus = new GameEventBus(); _clock = new GameClock();
            var entries = new MathEventScheduleEntry[scheduled];
            var rewards = new Dictionary<int, RewardSpec>(); var targets = new Dictionary<int, int>();
            var resource = goal == GateGoal.Minimize ? ResourceType.EnemyArmor : ResourceType.Health;
            for (int i = 0; i < scheduled; i++)
            {
                entries[i] = new MathEventScheduleEntry(i < simultaneous ? 0 : 2.1, type, 1, 6, resource, goal);
                rewards[i] = new RewardSpec(resource, type == ProblemType.ResourceOperationComparison ? 0 : 10,
                    type == ProblemType.ResourceOperationComparison);
                if (goal == GateGoal.ReachTarget) targets[i] = 0;
            }
            _level = new LevelDefinition(new LevelId("feedback-test"), new WorldId("w1"), "level.test", lanes, 200,
                null, entries, null, 20, 30, 20, 30, 0, "basic", availableWeaponIds: new[] { "basic" });
            _resources = new ResourceService(_bus, _clock); _resources.Initialize(_level);
            if (resource == ResourceType.EnemyArmor)
            { _resources.SetMax(resource, 30, "LEVEL_INIT"); _resources.ApplyDelta(resource, 20, "LEVEL_INIT"); }
            var profile = new DifficultyProfile(1, 1, 1, 1,
                goal == GateGoal.Minimize ? new[] { BinaryOperator.Subtraction } : new[] { BinaryOperator.Addition, BinaryOperator.Modulo },
                (SkillTag[])Enum.GetValues(typeof(SkillTag)), 0, 100, 1000, 2, 2, false, 0, false, 0, goal == GateGoal.Minimize, 2, 6, 1, false);
            _director = new MathEventDirector(_resources, _bus, new MathEventDirectorSettings(3, 5, 3, 2, 1, rewards, targets));
            _director.Initialize(_level, profile, new MathProblemGenerator(), new DeterministicRandomSource(7));
            Assert.That(_director.PrepareUpcomingEvents(0).IsSuccess, Is.True);
            _resolution = new GateResolutionService(_bus, scheduled);
            _rewards = new RewardService(_resources, _bus, _clock, scheduled);
            _presenter.BindLayout(lanes, capacity ?? scheduled); _presenter.Initialize(_director, _bus);
        }

        private void Tick(float delta)
        {
            _clock.Advance(delta); _director.Tick(_clock.GameTime); _presenter.RenderPending();
        }
        private MathFeedbackView[] Visible() => _root.GetComponentsInChildren<MathFeedbackView>();
        private void Open() { Tick(0); Tick(1); Tick(1); }
        private static GateCarrier Option(ActiveMathEvent item, bool correct)
        {
            for (int i = 0; i < item.Carriers.Count; i++) if (item.Carriers[i].IsCorrect == correct) return item.Carriers[i];
            throw new InvalidOperationException("Requested test option was not generated.");
        }
        private RewardApplication Resolve(ActiveMathEvent item, bool correct, bool complete = true)
        {
            var option = Option(item, correct);
            _director.OnPlayerLaneSelected(option.Lane, _clock.GameTime);
            ResolveCommand command = default;
            for (int i = 0; i < _director.PendingResolutions.Count; i++)
                if (_director.PendingResolutions[i].EventId == item.EventId) command = _director.PendingResolutions[i];
            var result = _resolution.Resolve(in command, item);
            var application = _rewards.ApplyReward(in result);
            if (complete) _director.OnRewardProcessed(in application, _clock.GameTime);
            return application;
        }

        [Test]
        public void PreviewApproachDecisionAndUnappliedResolutionNeverRevealFeedback()
        {
            Create(); Tick(0);
            Assert.That(Visible(), Is.Empty);
            Assert.That(_director.ActiveEvents[0].ResourceGate.HasValue, Is.False);
            Tick(1); Assert.That(Visible(), Is.Empty);
            Tick(1); Assert.That(Visible(), Is.Empty);
            var item = _director.ActiveEvents[0];
            var application = Resolve(item, true, false);
            _presenter.RenderPending();
            Assert.That(item.State, Is.EqualTo(MathEventState.Resolving));
            Assert.That(Visible(), Is.Empty, "No applied-delta claim before completion.");
            _director.OnRewardProcessed(in application, _clock.GameTime);
            Assert.That(Visible(), Is.Empty, "The synchronous callback may not touch UI.");
            _presenter.RenderPending(); Assert.That(Visible().Length, Is.EqualTo(1));
        }

        [TestCase(true, GateGoal.Maximize)]
        [TestCase(false, GateGoal.Maximize)]
        [TestCase(true, GateGoal.Minimize)]
        [TestCase(false, GateGoal.Minimize)]
        [TestCase(true, GateGoal.ReachTarget)]
        public void ResourceDecisionShowsBothRawResultsBestChoiceSelectionAndActualConsequence(bool correct, GateGoal goal)
        {
            Create(goal: goal); Open();
            var item = _director.ActiveEvents[0]; var binding = item.ResourceGate.Value;
            var chosen = Option(item, correct);
            _resources.ApplyDelta(item.RewardResource, -7, "COMBAT");
            var application = Resolve(item, correct);
            Assert.That(Visible(), Is.Empty);
            _presenter.RenderPending();
            var view = Visible()[0];
            for (int i = 0; i < binding.OptionCount; i++)
            {
                var option = binding.Template.Options[i];
                Assert.That(view.Result(option.Lane.Value), Is.EqualTo(option.DisplayLabel + " = " + binding.GetValue(i).ToDisplayString()));
                Assert.That(view.Marker(option.Lane.Value).Contains("Jobb választás"), Is.EqualTo(i == binding.CorrectOptionIndex));
                Assert.That(view.Marker(option.Lane.Value).Contains("Választottad"), Is.EqualTo(option.Lane == chosen.Lane));
            }
            Assert.That(binding.SnapshotValue, Is.EqualTo(20));
            Assert.That(view.Summary, Does.StartWith(correct ? "Helyes döntés!" : "Próbáld újra!"));
            if (correct)
            {
                Assert.That(view.Summary, Does.Contain(application.AppliedDelta.ToString("+0;-0;0")));
                Assert.That(_resources.GetCurrent(item.RewardResource), Is.EqualTo(13 + application.AppliedDelta));
                if (goal == GateGoal.Minimize) Assert.That(application.AppliedDelta, Is.LessThan(0));
            }
            else { Assert.That(view.Summary, Does.Contain("Nincs jutalom.")); Assert.That(application.AppliedDelta, Is.Zero); }
            Tick(.5f); Assert.That(Visible().Length, Is.EqualTo(1));
            Tick(.5f); Assert.That(Visible(), Is.Empty, "Director's configured feedback lifetime owns removal.");
            foreach (var row in _root.GetComponentsInChildren<MathFeedbackView>(true))
            { Assert.That(row.Summary, Is.Empty); Assert.That(row.Result(0), Is.Empty); Assert.That(row.Marker(1), Is.Empty); }
        }

        [Test]
        public void CorrectAtCapacityStillShowsRawResultAndZeroAppliedDelta()
        {
            Create(); Open();
            var item = _director.ActiveEvents[0]; var chosen = Option(item, true);
            var binding = item.ResourceGate.Value;
            Assert.That(binding.GetValue(chosen.OptionIndex).Value, Is.GreaterThan(binding.SnapshotValue));
            _resources.ApplyDelta(ResourceType.Health, 100, "COMBAT_REFUND");
            var application = Resolve(item, true); _presenter.RenderPending();
            Assert.That(application.RequestedDelta, Is.GreaterThan(0)); Assert.That(application.AppliedDelta, Is.Zero);
            Assert.That(Visible()[0].Summary, Is.EqualTo("Helyes döntés! Életerő: 0"));
            Assert.That(Visible()[0].Result(chosen.Lane.Value), Does.EndWith(binding.GetValue(chosen.OptionIndex).ToDisplayString()));
            Assert.That(_resources.GetCurrent(ResourceType.Health), Is.EqualTo(30));
        }

        [TestCase(2, true)]
        [TestCase(3, false)]
        [TestCase(4, true)]
        [TestCase(4, false)]
        public void AnswerLanesShowAllOfferedValuesAndCorrectAnswerOnlyAfterDecision(int lanes, bool correct)
        {
            Create(ProblemType.AnswerLaneSelection, lanes); Open();
            Assert.That(Visible(), Is.Empty);
            var item = _director.ActiveEvents[0]; Resolve(item, correct); _presenter.RenderPending();
            foreach (var option in item.Problem.Options)
            {
                Assert.That(Visible()[0].Result(option.Lane.Value), Is.EqualTo(option.Value.ToDisplayString()));
                Assert.That(Visible()[0].Marker(option.Lane.Value).Contains("Helyes válasz"), Is.EqualTo(option.IsCorrect));
            }
            Assert.That(Visible()[0].Summary, Is.EqualTo(correct ? "Helyes döntés! Életerő: +10" : "Próbáld újra! Nincs jutalom."));
        }

        [Test]
        public void CorrectChoiceAtLiveCapacityShowsZeroAppliedDeltaWithoutClampingMathematicalResults()
        {
            Create(); Open(); var item = _director.ActiveEvents[0];
            _resources.ApplyDelta(ResourceType.Health, 100, "COMBAT_REFUND");
            var applied = Resolve(item, true); _presenter.RenderPending();
            Assert.That(applied.RequestedDelta, Is.GreaterThan(0)); Assert.That(applied.AppliedDelta, Is.Zero);
            Assert.That(Visible()[0].Summary, Is.EqualTo("Helyes döntés! Életerő: 0"));
            var binding = item.ResourceGate.Value;
            for (int i = 0; i < binding.OptionCount; i++)
                Assert.That(Visible()[0].Result(binding.Template.Options[i].Lane.Value),
                    Does.EndWith(" = " + binding.GetValue(i).ToDisplayString()));
            for (int i = 0; i < 8; i++) _presenter.RenderPending();
            Assert.That(_resources.GetCurrent(ResourceType.Health), Is.EqualTo(30));
            Assert.That(_rewards.AppliedRewardEventIds.Count, Is.EqualTo(1));
        }

        [Test]
        public void ConcurrentCompletionsRemainDistinctAndLaterTerminalEventsUsePrewarmedViews()
        {
            Create(simultaneous: 3, scheduled: 4); Open();
            int prewarmed = _root.GetComponentsInChildren<MathFeedbackView>(true).Length;
            Assert.That(prewarmed, Is.EqualTo(4));
            // One physical selection resolves all currently open events, as in production.
            var first = _director.ActiveEvents[0]; var selected = Option(first, true).Lane;
            _director.OnPlayerLaneSelected(selected, _clock.GameTime);
            for (int i = 0; i < _director.PendingResolutions.Count; i++)
            {
                var command = _director.PendingResolutions[i]; ActiveMathEvent item = null;
                foreach (var active in _director.ActiveEvents) if (active.EventId == command.EventId) item = active;
                var resolution = _resolution.Resolve(in command, item); var applied = _rewards.ApplyReward(in resolution);
                _director.OnRewardProcessed(in applied, _clock.GameTime);
            }
            _director.ClearPendingResolutions(); _presenter.RenderPending();
            Assert.That(Visible().Length, Is.EqualTo(3));
            Tick(.1f); Tick(1); Tick(1);
            Assert.That(Visible(), Is.Empty);
            var later = _director.ActiveEvents[0]; Resolve(later, false); _presenter.RenderPending();
            Assert.That(Visible().Length, Is.EqualTo(1));
            Assert.That(_root.GetComponentsInChildren<MathFeedbackView>(true).Length, Is.EqualTo(prewarmed));
        }

        [Test]
        public void TimeoutAndPreparationFailureDoNotBecomeChildIncorrectFeedback()
        {
            Create(); Open(); Tick(6);
            var item = _director.ActiveEvents[0]; var command = _director.PendingResolutions[0];
            var result = _resolution.Resolve(in command, item); var applied = _rewards.ApplyReward(in result);
            _director.OnRewardProcessed(in applied, _clock.GameTime); _presenter.RenderPending();
            Assert.That(item.State, Is.EqualTo(MathEventState.Expired)); Assert.That(Visible(), Is.Empty);
            Object.DestroyImmediate(_root);
            Create(); Tick(0); _resources.SetMax(ResourceType.Health, 31, "TEST"); Tick(1);
            Assert.That(_director.ActiveEvents[0].State, Is.EqualTo(MathEventState.Expired)); Assert.That(Visible(), Is.Empty);
        }

        [Test]
        public void SessionEndAndDisposeClearFeedbackAndUnsubscribe()
        {
            Create(); Open(); Resolve(_director.ActiveEvents[0], true); _presenter.RenderPending();
            _bus.Publish(new SessionEnded(default, SessionResult.Victory, default, _clock.GameTime));
            Assert.That(Visible().Length, Is.EqualTo(1)); _presenter.RenderPending(); Assert.That(Visible(), Is.Empty);
            _presenter.Dispose(); _presenter.Dispose();
            _bus.Publish(new MathEventCompleted(MathEventId.Create(9, 5), 99, ResourceType.Health, 3));
            _presenter.RenderPending(); Assert.That(Visible(), Is.Empty);
            Assert.That(() => _presenter.Initialize(_director, _bus), Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void SynchronousHandlersAndUnchangedDrainAllocateNothingAndDuplicatesDoNotChangeFeedback()
        {
            Create(); Open(); var item = _director.ActiveEvents[0]; Resolve(item, true); _presenter.RenderPending();
            string expected = Visible()[0].Summary;
            Action publish = () =>
            {
                _bus.Publish(new MathEventResolved(item.EventId, new LaneIndex(0), false, 2));
                _bus.Publish(new MathEventCompleted(item.EventId, 999, ResourceType.Health, 2));
            };
            publish();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 128; i++) publish();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero); _presenter.RenderPending();
            Assert.That(Visible()[0].Summary, Is.EqualTo(expected));
            before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 128; i++) _presenter.RenderPending();
            allocated = GC.GetAllocatedBytesForCurrentThread() - before; Assert.That(allocated, Is.Zero);
        }

        [Test]
        public void CancellationHandlersAlsoOnlyWritePreallocatedRecordsAndDestroyUnsubscribesAllEvents()
        {
            Create(); Open(); var item = _director.ActiveEvents[0];
            Action publish = () =>
            {
                _bus.Publish(new MathEventExpired(item.EventId, "DEADLINE", 2));
                _bus.Publish(new MathEventPreparationFailed(item.EventId, "TEST", 2));
            };
            publish(); long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 128; i++) publish();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before; Assert.That(allocated, Is.Zero);
            Object.DestroyImmediate(_root);
            var handlers = (Dictionary<Type, Delegate>)typeof(GameEventBus).GetField("_handlersByEventType",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(_bus);
            Assert.That(handlers, Is.Empty);
        }

        [Test]
        public void MissingLayoutOrInsufficientStartupReservationFailVisibly()
        {
            _root = new GameObject("MissingFeedback"); _presenter = _root.AddComponent<MathFeedbackPresenter>();
            Assert.That(() => _presenter.Initialize(new NullMathEventDirector(), new GameEventBus()), Throws.TypeOf<InvalidOperationException>());
            Assert.That(() => _presenter.BindLayout(1, 1), Throws.TypeOf<ArgumentOutOfRangeException>());
            Object.DestroyImmediate(_root);
            Create(capacity: 0); Open(); Resolve(_director.ActiveEvents[0], true);
            Assert.That(() => _presenter.RenderPending(), Throws.TypeOf<InvalidOperationException>());
        }

        [UnityTest]
        public IEnumerator CombatAndLaneInputContinueThroughoutFeedbackWithoutPausing()
        {
            Create(); Open(); Resolve(_director.ActiveEvents[0], true); _presenter.RenderPending();
            var combat = new CombatService(_resources, _bus);
            var enemy = new EnemyDefinition("bot", EnemyType.Basic, 10000, .1f, 1, 1, "bot");
            combat.Initialize(_level, new[] { new WeaponDefinition("basic", 10, 20, 20, 0, TargetingMode.OwnLaneThenAdjacent, "fx", displayNameKey: "weapon.test") },
                new Dictionary<string, EnemyDefinition> { ["bot"] = enemy });
            combat.SpawnEnemy("bot", new LaneIndex(0), 5);
            int shots = 0; _bus.Subscribe<WeaponFired>(_ => shots++);
            int health = _resources.GetCurrent(ResourceType.Health); double start = _clock.GameTime;
            var input = new FakeLaneInputSource(); var controller = _root.AddComponent<PlayerController>();
            controller.Initialize(combat, input, 2);
            input.RaiseTap(.9f); Assert.That(combat.PlayerLane.Value, Is.EqualTo(1));
            input.RaiseSwipe(SwipeDirection.Left); Assert.That(combat.PlayerLane.Value, Is.EqualTo(0));
            while (_clock.GameTime - start < .3)
            {
                yield return null;
                _clock.Advance(Time.deltaTime); combat.Tick(_clock.GameTime, Time.deltaTime);
                _director.Tick(_clock.GameTime); _presenter.RenderPending();
                Assert.That(_clock.IsPaused, Is.False); Assert.That(Visible().Length, Is.EqualTo(1));
            }
            Assert.That(shots, Is.GreaterThan(0));
            Assert.That(_resources.GetCurrent(ResourceType.Health), Is.EqualTo(health));
            Assert.That(combat.ActiveEnemies[0].CurrentHealth, Is.LessThan(10000));
            Assert.That(combat.ActiveEnemies[0].Position.Y, Is.LessThan(5));
        }

        [TestCase(1920, false)]
        [TestCase(2340, true)]
        public void PortraitNotchLayoutKeepsThreeRowsReadableAtLaneBottomAndPassesInputThrough(int height, bool notch)
        {
            Create(lanes: 4, simultaneous: 3, scheduled: 3); Open();
            _director.OnPlayerLaneSelected(new LaneIndex(0), _clock.GameTime);
            foreach (var command in _director.PendingResolutions)
            {
                ActiveMathEvent item = null;
                foreach (var active in _director.ActiveEvents) if (active.EventId == command.EventId) item = active;
                var resolution = _resolution.Resolve(in command, item); var applied = _rewards.ApplyReward(in resolution);
                _director.OnRewardProcessed(in applied, _clock.GameTime);
            }
            _presenter.RenderPending();
            _root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            ((RectTransform)_root.transform).sizeDelta = new Vector2(1080, height);
            var fitter = _root.GetComponentInChildren<SafeAreaFitter>(); fitter.enabled = false;
            var safe = (RectTransform)fitter.transform;
            safe.anchorMin = new Vector2(0, notch ? 80f / height : 0); safe.anchorMax = new Vector2(1, notch ? 1 - 120f / height : 1);
            var band = (RectTransform)safe.Find("FeedbackBand");
            Assert.That(band.anchorMin.y, Is.GreaterThanOrEqualTo(0)); Assert.That(band.anchorMax.y, Is.LessThanOrEqualTo(.23f));
            Assert.That(Visible().Length, Is.EqualTo(3)); Canvas.ForceUpdateCanvases();
            foreach (var view in Visible())
            {
                foreach (var text in view.GetComponentsInChildren<TMP_Text>())
                {
                    text.ForceMeshUpdate(); Assert.That(text.fontSize, Is.GreaterThanOrEqualTo(24));
                    Assert.That(text.isTextOverflowing, Is.False, text.text); Assert.That(text.font, Is.Not.Null);
                }
                var row = (RectTransform)view.transform;
                for (int i = 0; i < 4; i++)
                {
                    var lane = (RectTransform)row.Find("Lane" + i);
                    Assert.That(lane.anchorMin.x, Is.EqualTo(i / 4f)); Assert.That(lane.anchorMax.x, Is.EqualTo((i + 1) / 4f));
                }
            }
            Assert.That(_root.GetComponentsInChildren<Selectable>(true), Is.Empty);
            Assert.That(_root.GetComponentsInChildren<GraphicRaycaster>(true), Is.Empty);
            foreach (var graphic in _root.GetComponentsInChildren<Graphic>(true)) Assert.That(graphic.raycastTarget, Is.False);
        }
        [TestCase(false)]
        [TestCase(true)]
        public void LegacyAmmoEventOrPreparedRewardIsRejectedBeforeDrawing(bool rewardField)
        {
            Create(); Open(); Resolve(_director.ActiveEvents[0], true);
            var item = _director.ActiveEvents[0];
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            if (rewardField)
                typeof(RewardSpec).GetField("<Resource>k__BackingField", flags)
                    .SetValue(item.ResourceGate.Value.Prepared.Reward, ResourceType.Ammo);
            else
                typeof(ActiveMathEvent).GetField("<RewardResource>k__BackingField", flags).SetValue(item, ResourceType.Ammo);
            Assert.That(() => _presenter.RenderPending(), Throws.TypeOf<ArgumentException>());
            Assert.That(Visible().Length, Is.EqualTo(0));
        }

    }
}
#endif
