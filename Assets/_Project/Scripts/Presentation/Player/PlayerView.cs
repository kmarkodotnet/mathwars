// PlayerView.cs
// F1-17 — the player's visual Transform-movement between lanes, called out
// explicitly by this task's brief: F1-13's Napló scoped PlayerController to
// pure command-forwarding ("a játékos vizuális (Transform-)mozgatása
// sávváltáskor NEM része ennek a feladatnak — nincs még Player-prefab/view a
// projektben"), deferring it to this task once an actual player prefab and
// scene composition root exist.
//
// Purely cosmetic mapping: LaneIndex -> world X, read from the SAME
// LaneAnchor components ArenaBuilder already laid out (Presentation.Arena),
// not a second, independently-authored coordinate. The decision of WHICH
// lane the player is in is made entirely by ICombatService
// (MovePlayerToLane/PlayerLaneChanged) — this class only reacts to that
// event and animates the matching LaneAnchor.CenterX; it never decides a
// lane itself (unity-csharp skill: "MonoBehaviour-ban TILOS üzleti logika").
//
// Z/height are left exactly as authored on the prefab/scene instance (the
// player's parked depth inside the arena's PlayerZone) — only X changes.
using System;
using System.Collections.Generic;
using UnityEngine;
using MathHorde.Core.Common;
using MathHorde.GameLogic.Events;
using MathHorde.Presentation.Arena;
using MathHorde.Presentation.Events;

namespace MathHorde.Presentation.Player
{
    public sealed class PlayerView : MonoBehaviour
    {
        [Tooltip("Világegység/másodperc — milyen gyorsan siklik át a játékos a célsáv X-pozíciójára.")]
        [SerializeField] private float _laneChangeSpeed = 10f;

        private IGameEventSource _eventSource;
        private float[] _centerXByLane;
        private float _targetX;
        private bool _isInitialized;

        /// <summary>
        /// Call once from the scene's composition root, after the arena's
        /// LaneAnchor components exist and ICombatService.Initialize has
        /// already run (so <paramref name="initialLane"/> is meaningful).
        /// </summary>
        public void Initialize(IGameEventSource eventSource, IReadOnlyList<LaneAnchor> laneAnchors, LaneIndex initialLane)
        {
            _eventSource = eventSource ?? throw new ArgumentNullException(nameof(eventSource));
            if (laneAnchors == null || laneAnchors.Count == 0)
            {
                throw new ArgumentException("Legalább egy LaneAnchor szükséges.", nameof(laneAnchors));
            }

            _centerXByLane = new float[laneAnchors.Count];
            for (int i = 0; i < laneAnchors.Count; i++)
            {
                LaneAnchor anchor = laneAnchors[i];
                _centerXByLane[anchor.LaneIndex] = anchor.CenterX;
            }

            _targetX = ResolveCenterX(initialLane);
            Vector3 position = transform.position;
            position.x = _targetX;
            transform.position = position;

            _eventSource.Subscribe<PlayerLaneChanged>(HandlePlayerLaneChanged);
            _isInitialized = true;
        }

        private void OnDestroy()
        {
            if (!_isInitialized)
            {
                return;
            }

            _eventSource.Unsubscribe<PlayerLaneChanged>(HandlePlayerLaneChanged);
        }

        private void HandlePlayerLaneChanged(PlayerLaneChanged e)
        {
            _targetX = ResolveCenterX(e.To);
        }

        private void Update()
        {
            if (!_isInitialized)
            {
                return;
            }

            Vector3 position = transform.position;
            if (Mathf.Approximately(position.x, _targetX))
            {
                return;
            }

            position.x = Mathf.MoveTowards(position.x, _targetX, _laneChangeSpeed * Time.deltaTime);
            transform.position = position;
        }

        private float ResolveCenterX(LaneIndex lane)
        {
            int index = lane.Value;
            if (index < 0 || index >= _centerXByLane.Length)
            {
                // Defensive clamp, not a silent lane invention: ICombatService
                // already validates LaneIndex against the level's LaneCount
                // before this event can even fire, so this should never
                // trigger in practice — it only prevents an out-of-range
                // array access if the arena's LaneAnchor count and the
                // level's LaneCount were ever misconfigured against each
                // other (a known, flagged sync gap — see this task's Napló).
                index = Mathf.Clamp(index, 0, _centerXByLane.Length - 1);
            }

            return _centerXByLane[index];
        }
    }
}
