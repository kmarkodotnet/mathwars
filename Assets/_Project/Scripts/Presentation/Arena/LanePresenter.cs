using System;
using System.Collections.Generic;
using UnityEngine;
using MathHorde.GameLogic;
using MathHorde.GameLogic.Events;
using MathHorde.Presentation.Events;

namespace MathHorde.Presentation.Arena
{
    /// <summary>
    /// Cosmetic lane selection and decision-window markers. The host drains after
    /// session.Tick; synchronous handlers never touch Unity objects or allocate.
    /// Lane membership/state come from GameLogic, never answer correctness.
    /// </summary>
    public sealed class LanePresenter : MonoBehaviour, IDisposable
    {
        [SerializeField] private LaneHighlightView[] _views = Array.Empty<LaneHighlightView>();
        private ICombatService _combat;
        private IMathEventDirector _director;
        private IGameEventSource _events;
        private int _laneCount;
        private int _renderedLane = -1, _renderedMask = -1;
        private bool _bound, _initialized, _dirty, _ended;

        /// <summary>Before Initialize: ordered arena anchors and the same configured depth used by the director.</summary>
        public void BindGeometry(IReadOnlyList<LaneAnchor> lanes, float decisionZoneDepth)
        {
            if (_initialized) throw new InvalidOperationException("LanePresenter is already initialized.");
            if (lanes == null) throw new ArgumentNullException(nameof(lanes));
            if (lanes.Count < 2 || lanes.Count > 4 || _views.Length != 4)
                throw new InvalidOperationException("LanePresenter requires 2-4 anchors and four prebuilt views.");
            if (float.IsNaN(decisionZoneDepth) || float.IsInfinity(decisionZoneDepth) || decisionZoneDepth < .5f || decisionZoneDepth > 10f)
                throw new ArgumentOutOfRangeException(nameof(decisionZoneDepth));
            // Validate everything before mutating the prefab or accepting the binding.
            for (int i = 0; i < _views.Length; i++)
            {
                if (_views[i] == null || !_views[i].IsConfigured)
                    throw new InvalidOperationException("LanePresenter has an incomplete view.");
                for (int j = 0; j < i; j++)
                    if (_views[i] == _views[j]) throw new InvalidOperationException("LanePresenter views must be distinct.");
            }
            for (int i = 0; i < lanes.Count; i++)
            {
                var lane = lanes[i];
                if (lane == null || lane.LaneIndex != i || !Finite(lane.CenterX) || !Finite(lane.Width) || !Finite(lane.Length)
                    || lane.Width <= 0 || lane.Length < decisionZoneDepth + .5f)
                    throw new InvalidOperationException("Lane anchors must be ordered, finite and contain the decision zone.");
            }
            _laneCount = lanes.Count;
            for (int i = 0; i < _views.Length; i++)
            {
                _views[i].Clear();
                _views[i].gameObject.SetActive(i < _laneCount);
                if (i < _laneCount) _views[i].Bind(lanes[i], decisionZoneDepth);
            }
            _bound = true;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        public void Initialize(ICombatService combat, IMathEventDirector director, IGameEventSource events)
        {
            if (combat == null) throw new ArgumentNullException(nameof(combat));
            if (director == null) throw new ArgumentNullException(nameof(director));
            if (events == null) throw new ArgumentNullException(nameof(events));
            if (_initialized || !_bound) throw new InvalidOperationException("Bind lane geometry once before initialization.");
            if (combat.PlayerLane.Value >= _laneCount) throw new InvalidOperationException("Player lane has no visual anchor.");
            _combat = combat; _director = director; _events = events; _initialized = true;
            _events.Subscribe<PlayerLaneChanged>(OnLane);
            _events.Subscribe<MathEventDecisionWindowOpened>(OnDecision);
            _events.Subscribe<MathEventResolved>(OnResolved);
            _events.Subscribe<MathEventCompleted>(OnCompleted);
            _events.Subscribe<MathEventExpired>(OnExpired);
            _events.Subscribe<MathEventPreparationFailed>(OnPreparationFailed);
            _events.Subscribe<SessionEnded>(OnSessionEnded);
            _dirty = true;
        }

        public void RenderPending()
        {
            if (_events == null) return;
            int selected = _combat.PlayerLane.Value;
            if (selected >= _laneCount) throw new InvalidOperationException("Player lane has no visual anchor.");
            int decisionMask = 0;
            if (!_ended)
            {
                var active = _director.ActiveEvents;
                for (int e = 0; e < active.Count; e++)
                {
                    var mathEvent = active[e];
                    if (mathEvent.State != MathEventState.AwaitingDecision) continue;
                    for (int c = 0; c < mathEvent.Carriers.Count; c++)
                    {
                        int lane = mathEvent.Carriers[c].Lane.Value;
                        if (lane >= _laneCount) throw new InvalidOperationException("Decision lane has no visual anchor.");
                        decisionMask |= 1 << lane;
                    }
                }
            }
            // A choice can enter Resolving without another lane-change event (the
            // player may already occupy that lane). Always reconcile public state.
            int selectedLane = _ended ? -1 : selected;
            if (_dirty || _renderedLane != selectedLane || _renderedMask != decisionMask)
                for (int i = 0; i < _laneCount; i++)
                    _views[i].SetState(i == selectedLane, (decisionMask & (1 << i)) != 0);
            _renderedLane = selectedLane; _renderedMask = decisionMask;
            _dirty = false;
        }

        public void Dispose()
        {
            if (_events == null) return;
            _events.Unsubscribe<PlayerLaneChanged>(OnLane);
            _events.Unsubscribe<MathEventDecisionWindowOpened>(OnDecision);
            _events.Unsubscribe<MathEventResolved>(OnResolved);
            _events.Unsubscribe<MathEventCompleted>(OnCompleted);
            _events.Unsubscribe<MathEventExpired>(OnExpired);
            _events.Unsubscribe<MathEventPreparationFailed>(OnPreparationFailed);
            _events.Unsubscribe<SessionEnded>(OnSessionEnded);
            for (int i = 0; i < _views.Length; i++) _views[i].Clear();
            _combat = null; _director = null; _events = null;
        }

        private void OnDestroy() => Dispose();
        private void OnLane(PlayerLaneChanged e) => _dirty = true;
        private void OnDecision(MathEventDecisionWindowOpened e) => _dirty = true;
        private void OnResolved(MathEventResolved e) => _dirty = true;
        private void OnCompleted(MathEventCompleted e) => _dirty = true;
        private void OnExpired(MathEventExpired e) => _dirty = true;
        private void OnPreparationFailed(MathEventPreparationFailed e) => _dirty = true;
        private void OnSessionEnded(SessionEnded e) { _ended = true; _dirty = true; }
    }
}
