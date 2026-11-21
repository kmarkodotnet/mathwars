using System;
using MathHorde.Core.Common;
using MathHorde.Core.Problems;
using MathHorde.Core.Resources;
using MathHorde.GameLogic;
using MathHorde.GameLogic.Events;
using MathHorde.Infrastructure;
using MathHorde.Presentation.Events;
using UnityEngine;

namespace MathHorde.Presentation.Hud
{
    /// <summary>Read-only decision feedback. Call RenderPending only after session.Tick returns.</summary>
    public sealed class MathFeedbackPresenter : MonoBehaviour, IDisposable
    {
        [SerializeField] private MathFeedbackView _template;
        private IMathEventDirector _director;
        private IGameEventSource _events;
        private ILocalizationService _localization;
        private Record[] _records;
        private MathFeedbackView[] _views;
        private int _laneCount, _eventCapacity;
        private bool _ended, _overflow;

        private struct Record
        {
            public bool Used, Resolved, Completed, Cancelled, Dirty, Visible;
            public MathEventResolved Resolution;
            public MathEventCompleted Completion;
            public MathEventId Id;
        }

        /// <summary>Startup only: reserve the full level schedule, including overlapping terminal events.</summary>
        public void BindLayout(int laneCount, int eventCapacity)
        {
            if (_records != null) throw new InvalidOperationException("Feedback layout cannot change during a session.");
            if (laneCount < 2 || laneCount > 4) throw new ArgumentOutOfRangeException(nameof(laneCount));
            if (eventCapacity < 0) throw new ArgumentOutOfRangeException(nameof(eventCapacity));
            _laneCount = laneCount; _eventCapacity = eventCapacity;
        }

        public void Initialize(IMathEventDirector director, IGameEventSource events)
        {
            if (_records != null) throw new InvalidOperationException("MathFeedbackPresenter is already initialized.");
            if (director == null) throw new ArgumentNullException(nameof(director));
            if (events == null) throw new ArgumentNullException(nameof(events));
            if (_laneCount == 0 || _template == null || !_template.IsWired)
                throw new InvalidOperationException("Feedback requires a bound layout and a wired view template.");
            _localization = new ResourcesLocalizationService();
            _records = new Record[_eventCapacity];
            _views = new MathFeedbackView[_eventCapacity];
            _template.Clear();
            // Prewarm before the first Tick. Never instantiate, destroy or grow a pool in combat.
            for (int i = 0; i < _eventCapacity; i++)
            {
                _views[i] = i == 0 ? _template : Instantiate(_template, _template.transform.parent);
                _views[i].BindLanes(_laneCount); _views[i].Clear();
            }
            _director = director; _events = events;
            events.Subscribe<MathEventResolved>(OnResolved);
            events.Subscribe<MathEventCompleted>(OnCompleted);
            events.Subscribe<MathEventExpired>(OnExpired);
            events.Subscribe<MathEventPreparationFailed>(OnFailed);
            events.Subscribe<SessionEnded>(OnEnded);
        }

        public void RenderPending()
        {
            if (_director == null) return;
            if (_overflow) throw new InvalidOperationException("Feedback capacity exhausted; reserve the full level schedule before Tick.");
            int count = 0;
            for (int i = 0; i < _records.Length; i++)
            {
                ref var record = ref _records[i];
                var item = record.Used && !_ended ? Find(record.Id) : null;
                if (item != null) MathContentGuard.RequireActive(item);
                if (record.Completed) ResourceRules.RequireActive(record.Completion.Resource);
                bool visible = record.Resolved && record.Completed && !record.Cancelled
                    && item != null && item.State == MathEventState.Completed;
                if (visible)
                {
                    if (record.Dirty || !record.Visible) Render(_views[i], item, in record);
                    count++;
                }
                else if (record.Visible) _views[i].Clear();
                record.Visible = visible; record.Dirty = false;
            }
            int row = 0;
            for (int i = 0; i < _records.Length; i++)
                if (_records[i].Visible) _views[i].Place(row++, count);
        }

        private ActiveMathEvent Find(MathEventId id)
        {
            var active = _director.ActiveEvents;
            for (int i = 0; i < active.Count; i++) if (active[i].EventId == id) return active[i];
            return null;
        }

        private void Render(MathFeedbackView view, ActiveMathEvent item, in Record record)
        {
            bool gate = item.Type == ProblemType.ResourceOperationComparison;
            string consequence = record.Resolution.WasCorrect
                ? _localization.Resolve("feedback.applied", _localization.Resolve("gate.resource." + record.Completion.Resource),
                    record.Completion.AppliedDelta.ToString("+0;-0;0", System.Globalization.CultureInfo.InvariantCulture))
                : _localization.Resolve("feedback.noReward");
            view.SetSummary(_localization.Resolve(record.Resolution.WasCorrect ? "feedback.correct" : "feedback.incorrect", consequence));
            for (int lane = 0; lane < _laneCount; lane++) view.SetLane(lane, string.Empty, string.Empty, false, false);
            for (int i = 0; i < item.Carriers.Count; i++)
            {
                var carrier = item.Carriers[i];
                bool selected = carrier.Lane == record.Resolution.SelectedLane;
                // Completed is the only route into this method. Never read results/correctness before a decision.
                string result = gate
                    ? _localization.Resolve("feedback.operation", carrier.DisplayLabel, item.ResourceGate.Value.GetValue(carrier.OptionIndex).ToDisplayString())
                    : item.Problem.Options[carrier.OptionIndex].Value.ToDisplayString();
                string marker = carrier.IsCorrect
                    ? _localization.Resolve(gate ? "feedback.best" : "feedback.answer") : string.Empty;
                if (selected) marker = _localization.Resolve("feedback.selected", marker);
                view.SetLane(carrier.Lane.Value, result, marker, carrier.IsCorrect, selected);
            }
        }

        // The synchronous handlers only copy structs into the startup reservation. No strings/UI/allocation.
        private int Reserve(MathEventId id)
        {
            if (_ended) return -1;
            for (int i = 0; i < _records.Length; i++) if (_records[i].Used && _records[i].Id == id) return i;
            for (int i = 0; i < _records.Length; i++)
                if (!_records[i].Used) { _records[i].Used = true; _records[i].Id = id; return i; }
            _overflow = true; return -1;
        }

        private void OnResolved(MathEventResolved e)
        {
            int i = Reserve(e.EventId); if (i < 0 || _records[i].Resolved) return;
            _records[i].Resolution = e; _records[i].Resolved = _records[i].Dirty = true;
        }
        private void OnCompleted(MathEventCompleted e)
        {
            int i = Reserve(e.EventId); if (i < 0 || _records[i].Completed) return;
            _records[i].Completion = e; _records[i].Completed = _records[i].Dirty = true;
        }
        private void Cancel(MathEventId id)
        {
            int i = Reserve(id); if (i >= 0) _records[i].Cancelled = true;
        }
        private void OnExpired(MathEventExpired e) => Cancel(e.EventId);
        private void OnFailed(MathEventPreparationFailed e) => Cancel(e.EventId);
        private void OnEnded(SessionEnded e) => _ended = true;

        public void Dispose()
        {
            if (_events == null) return;
            _events.Unsubscribe<MathEventResolved>(OnResolved);
            _events.Unsubscribe<MathEventCompleted>(OnCompleted);
            _events.Unsubscribe<MathEventExpired>(OnExpired);
            _events.Unsubscribe<MathEventPreparationFailed>(OnFailed);
            _events.Unsubscribe<SessionEnded>(OnEnded);
            for (int i = 0; i < _views.Length; i++) _views[i].Clear();
            _ended = true; _events = null; _director = null;
        }
        private void OnDestroy() => Dispose();
    }
}
