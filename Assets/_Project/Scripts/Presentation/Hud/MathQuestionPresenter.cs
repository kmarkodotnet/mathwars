using System;
using UnityEngine;
using MathHorde.Core.Common;
using MathHorde.Core.Problems;
using MathHorde.GameLogic;
using MathHorde.GameLogic.Events;
using MathHorde.Infrastructure;
using MathHorde.Presentation.Events;

namespace MathHorde.Presentation.Hud
{
    /// <summary>ADR-015: synchronous handlers only mark dirty. Drain after session.Tick.</summary>
    public sealed class MathQuestionPresenter : MonoBehaviour, IDisposable
    {
        [SerializeField] private MathQuestionView[] _rows = Array.Empty<MathQuestionView>();
        private IMathEventDirector _director;
        private IGameEventSource _events;
        private IGameClock _clock;
        private ILocalizationService _localization;
        private Slot[] _slots;
        private bool _dirty, _ended;

        private struct Slot
        {
            public ActiveMathEvent Event;
            public MathEventState State;
            public int Seconds;
        }

        /// <summary>Composition-only dependency; use the session's clock, before Initialize.</summary>
        public void BindClock(IGameClock clock)
        {
            if (_slots != null) throw new InvalidOperationException("Question clock cannot change during a session.");
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public void Initialize(IMathEventDirector director, IGameEventSource events)
        {
            if (_slots != null) throw new InvalidOperationException("MathQuestionPresenter is already initialized.");
            if (director == null) throw new ArgumentNullException(nameof(director));
            if (events == null) throw new ArgumentNullException(nameof(events));
            if (_clock == null || _rows.Length < 3) throw new InvalidOperationException("Question HUD requires the session clock and at least three preallocated rows.");
            for (int i = 0; i < _rows.Length; i++)
            {
                if (_rows[i] == null || !_rows[i].IsWired) throw new InvalidOperationException("Question row is not wired.");
                for (int j = 0; j < i; j++)
                    if (_rows[i] == _rows[j]) throw new InvalidOperationException("Question rows must be distinct.");
            }
            _localization = new ResourcesLocalizationService();
            _slots = new Slot[_rows.Length];
            for (int i = 0; i < _rows.Length; i++) _rows[i].Clear();
            _director = director;
            _events = events;
            events.Subscribe<MathEventPreviewStarted>(OnPreview);
            events.Subscribe<MathEventGatesSpawned>(OnSpawned);
            events.Subscribe<MathEventDecisionWindowOpened>(OnDecision);
            events.Subscribe<MathEventResolved>(OnResolved);
            events.Subscribe<MathEventCompleted>(OnCompleted);
            events.Subscribe<MathEventExpired>(OnExpired);
            events.Subscribe<MathEventPreparationFailed>(OnFailed);
            events.Subscribe<SessionEnded>(OnEnded);
            _dirty = true;
        }

        public void RenderPending()
        {
            if (_director == null) return;
            int row = 0;
            var active = _director.ActiveEvents;
            for (int i = 0; !_ended && i < active.Count; i++)
            {
                var item = active[i];
                MathContentGuard.RequireActive(item);
                if (!Visible(item.State)) continue;
                if (row == _rows.Length) throw new InvalidOperationException("Question HUD preallocated capacity exceeded.");
                bool changed = _slots[row].Event != item || _slots[row].State != item.State;
                if (_dirty || changed)
                {
                    _rows[row].SetQuestion(Question(item));
                    _slots[row] = new Slot { Event = item, State = item.State, Seconds = -1 };
                }
                if (item.State == MathEventState.AwaitingDecision)
                {
                    double remaining = Math.Max(0, item.DeadlineGameTime - _clock.GameTime);
                    int seconds = (int)Math.Ceiling(remaining);
                    if (_slots[row].Seconds != seconds)
                    {
                        _rows[row].SetTime(_localization.Resolve("question.time", seconds),
                            (float)(remaining / Math.Max(double.Epsilon, item.DeadlineGameTime - item.DecisionOpenedAt)));
                        _slots[row].Seconds = seconds;
                    }
                    else _rows[row].SetProgress((float)(remaining / Math.Max(double.Epsilon, item.DeadlineGameTime - item.DecisionOpenedAt)));
                }
                else if (_dirty || changed)
                    _rows[row].SetTime(_localization.Resolve(item.State == MathEventState.Preview ? "question.preview" : "question.approaching"), 1);
                row++;
            }
            for (int i = row; i < _rows.Length; i++)
                if (_slots[i].Event != null) { _rows[i].Clear(); _slots[i] = default; }
            _dirty = false;
        }

        private string Question(ActiveMathEvent item)
        {
            if (item.Type != ProblemType.ResourceOperationComparison) return item.Problem.DisplayExpression;
            string resource = _localization.Resolve("gate.resource." + item.SnapshotResource);
            string goal = item.Goal == GateGoal.ReachTarget
                ? _localization.Resolve("gate.goal.target", item.TargetValue)
                : _localization.Resolve(item.Goal == GateGoal.Maximize ? "gate.goal.maximize" : "gate.goal.minimize");
            return item.State == MathEventState.Preview
                ? _localization.Resolve("question.resourcePreview", resource, goal)
                : _localization.Resolve("gate.snapshot", resource, item.SnapshotValue, goal);
        }

        private static bool Visible(MathEventState state) => state == MathEventState.Preview
            || state == MathEventState.Approaching || state == MathEventState.AwaitingDecision;

        public void Dispose()
        {
            if (_events == null) return;
            _events.Unsubscribe<MathEventPreviewStarted>(OnPreview);
            _events.Unsubscribe<MathEventGatesSpawned>(OnSpawned);
            _events.Unsubscribe<MathEventDecisionWindowOpened>(OnDecision);
            _events.Unsubscribe<MathEventResolved>(OnResolved);
            _events.Unsubscribe<MathEventCompleted>(OnCompleted);
            _events.Unsubscribe<MathEventExpired>(OnExpired);
            _events.Unsubscribe<MathEventPreparationFailed>(OnFailed);
            _events.Unsubscribe<SessionEnded>(OnEnded);
            for (int i = 0; i < _rows.Length; i++) { _rows[i].Clear(); _slots[i] = default; }
            _events = null;
            _director = null;
        }

        private void OnDestroy() => Dispose();
        private void OnPreview(MathEventPreviewStarted e) => _dirty = true;
        private void OnSpawned(MathEventGatesSpawned e) => _dirty = true;
        private void OnDecision(MathEventDecisionWindowOpened e) => _dirty = true;
        private void OnResolved(MathEventResolved e) => _dirty = true;
        private void OnCompleted(MathEventCompleted e) => _dirty = true;
        private void OnExpired(MathEventExpired e) => _dirty = true;
        private void OnFailed(MathEventPreparationFailed e) => _dirty = true;
        private void OnEnded(SessionEnded e) { _ended = true; _dirty = true; }
    }
}
