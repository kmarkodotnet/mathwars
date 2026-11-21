using System;
using UnityEngine;
using MathHorde.Core.Common;
using MathHorde.Core.Problems;
using MathHorde.GameLogic;
using MathHorde.GameLogic.Events;
using MathHorde.Infrastructure;
using MathHorde.Presentation.Arena;
using MathHorde.Presentation.Events;
using MathHorde.Presentation.Pooling;

namespace MathHorde.Presentation.Gates
{
    /// <summary>
    /// ADR-015: handlers only mark dirty; the host drains after session.Tick.
    /// Labels never inspect correctness, operation results or live resources.
    /// Preview/timer and post-decision feedback belong to F3-09/F3-11.
    /// </summary>
    public sealed class GatePresenter : MonoBehaviour, IDisposable
    {
        [SerializeField] private GateCarrierView _resourceGatePrefab;
        [SerializeField] private GateCarrierView _answerCarrierPrefab;
        [SerializeField] private Transform _poolRoot;
        [SerializeField] private Camera _camera;
        [SerializeField] private LaneAnchor[] _lanes = Array.Empty<LaneAnchor>();
        [SerializeField] private float _labelHeight = 1.2f;
        // Three simultaneous events, each with at most four options (contract).
        [SerializeField, Min(12)] private int _carrierCapacity = 12;

        private IMathEventDirector _director;
        private IGameEventSource _events;
        private ILocalizationService _localization;
        private ObjectPool<GateCarrierView> _resourcePool, _answerPool;
        private Slot[] _slots;
        private bool _dirty;

        private struct Slot
        {
            public GateCarrierView View;
            public bool Resource;
        }

        public int ActiveCarrierCount => (_resourcePool?.ActiveCount ?? 0) + (_answerPool?.ActiveCount ?? 0);
        public int CreatedCarrierCount => (_resourcePool?.CreatedCount ?? 0) + (_answerPool?.CreatedCount ?? 0);

        public void Initialize(IMathEventDirector director, IGameEventSource events)
        {
            if (_slots != null) throw new InvalidOperationException("GatePresenter is already initialized.");
            if (director == null) throw new ArgumentNullException(nameof(director));
            if (events == null) throw new ArgumentNullException(nameof(events));
            if (_resourceGatePrefab == null || _answerCarrierPrefab == null || _camera == null || _lanes.Length < 2 || _carrierCapacity < 12)
                throw new InvalidOperationException("GatePresenter requires both prefabs, camera, lane anchors and capacity >= 12.");
            for (int i = 0; i < _lanes.Length; i++)
                if (_lanes[i] == null || _lanes[i].LaneIndex != i)
                    throw new InvalidOperationException("GatePresenter lane anchors must be non-null and ordered by lane index.");
            _localization = new ResourcesLocalizationService();
            _slots = new Slot[_carrierCapacity];
            _resourcePool = CreatePool(_resourceGatePrefab);
            _answerPool = CreatePool(_answerCarrierPrefab);
            _director = director;
            _events = events;
            _events.Subscribe<MathEventPreviewStarted>(OnPreview);
            _events.Subscribe<MathEventGatesSpawned>(OnSpawned);
            _events.Subscribe<MathEventDecisionWindowOpened>(OnDecision);
            _events.Subscribe<MathEventResolved>(OnResolved);
            _events.Subscribe<MathEventCompleted>(OnCompleted);
            _events.Subscribe<MathEventExpired>(OnExpired);
            _events.Subscribe<MathEventPreparationFailed>(OnPreparationFailed);
            _dirty = true;
        }

        /// <summary>Call outside the simulation Tick; also follows carrier positions on quiet frames.</summary>
        public void RenderPending()
        {
            if (_director == null) return;
            // Release terminal/removed carriers first, so simultaneous replacement cannot grow a pool.
            for (int s = 0; s < _slots.Length; s++)
            {
                if (_slots[s].View != null && !FindCarrier(_slots[s].View.EventId, _slots[s].View.OptionIndex)) Release(s);
            }
            var active = _director.ActiveEvents;
            for (int e = 0; e < active.Count; e++)
            {
                var mathEvent = active[e];
                MathContentGuard.RequireActive(mathEvent);
                if (!Visible(mathEvent.State)) continue;
                for (int c = 0; c < mathEvent.Carriers.Count; c++)
                {
                    var carrier = mathEvent.Carriers[c];
                    int slot = FindSlot(mathEvent.EventId, carrier.OptionIndex);
                    if (slot < 0) slot = Acquire(mathEvent, carrier);
                    else if (_dirty) Bind(_slots[slot].View, mathEvent, carrier);
                    int lane = carrier.Lane.Value;
                    if (lane >= _lanes.Length) throw new InvalidOperationException("Carrier lane has no visual anchor.");
                    // Carrier.Position.X is a logical lane index, not an arena world coordinate.
                    _slots[slot].View.SetPose(new Vector3(_lanes[lane].CenterX, _labelHeight, carrier.Position.Y), _camera.transform.rotation);
                }
            }
            _dirty = false;
        }

        private static bool Visible(MathEventState state) => state == MathEventState.Approaching || state == MathEventState.AwaitingDecision;

        private bool FindCarrier(MathEventId id, int option)
        {
            var events = _director.ActiveEvents;
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].EventId != id || !Visible(events[i].State)) continue;
                for (int c = 0; c < events[i].Carriers.Count; c++)
                    if (events[i].Carriers[c].OptionIndex == option) return true;
            }
            return false;
        }

        private int FindSlot(MathEventId id, int option)
        {
            for (int i = 0; i < _slots.Length; i++)
                if (_slots[i].View != null && _slots[i].View.EventId == id && _slots[i].View.OptionIndex == option) return i;
            return -1;
        }

        private int Acquire(ActiveMathEvent mathEvent, GateCarrier carrier)
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i].View != null) continue;
                bool resource = mathEvent.Type == ProblemType.ResourceOperationComparison;
                var pool = resource ? _resourcePool : _answerPool;
                if (pool.FreeCount == 0) throw new InvalidOperationException("Gate carrier prewarm capacity exhausted.");
                var view = pool.Get();
                _slots[i] = new Slot { View = view, Resource = resource };
                Bind(view, mathEvent, carrier);
                return i;
            }
            throw new InvalidOperationException("Too many simultaneous gate carriers for the configured capacity.");
        }

        private void Bind(GateCarrierView view, ActiveMathEvent mathEvent, GateCarrier carrier)
        {
            string context = string.Empty;
            if (mathEvent.Type == ProblemType.ResourceOperationComparison)
            {
                string goal = mathEvent.Goal == GateGoal.ReachTarget
                    ? _localization.Resolve("gate.goal.target", mathEvent.TargetValue)
                    : _localization.Resolve(mathEvent.Goal == GateGoal.Maximize ? "gate.goal.maximize" : "gate.goal.minimize");
                context = _localization.Resolve("gate.snapshot", _localization.Resolve("gate.resource." + mathEvent.SnapshotResource), mathEvent.SnapshotValue, goal);
            }
            // DisplayLabel is already the contract's operation/answer label. Do not evaluate it here.
            view.Bind(mathEvent.EventId, carrier.OptionIndex, carrier.DisplayLabel, context,
                mathEvent.Type == ProblemType.ResourceOperationComparison ? mathEvent.Goal : (GateGoal?)null);
        }

        private ObjectPool<GateCarrierView> CreatePool(GateCarrierView prefab)
        {
            return new ObjectPool<GateCarrierView>(() =>
            {
                var view = Instantiate(prefab, _poolRoot != null ? _poolRoot : transform);
                view.Unbind();
                return view;
            }, onRelease: view => view.Unbind(), prewarmCount: _carrierCapacity);
        }

        private void Release(int slot)
        {
            var item = _slots[slot];
            (item.Resource ? _resourcePool : _answerPool).Release(item.View);
            _slots[slot] = default;
        }

        public void Dispose()
        {
            if (_events == null) return;
            _events.Unsubscribe<MathEventPreviewStarted>(OnPreview);
            _events.Unsubscribe<MathEventGatesSpawned>(OnSpawned);
            _events.Unsubscribe<MathEventDecisionWindowOpened>(OnDecision);
            _events.Unsubscribe<MathEventResolved>(OnResolved);
            _events.Unsubscribe<MathEventCompleted>(OnCompleted);
            _events.Unsubscribe<MathEventExpired>(OnExpired);
            _events.Unsubscribe<MathEventPreparationFailed>(OnPreparationFailed);
            for (int i = 0; i < _slots.Length; i++) if (_slots[i].View != null) Release(i);
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
        private void OnPreparationFailed(MathEventPreparationFailed e) => _dirty = true;
    }
}
