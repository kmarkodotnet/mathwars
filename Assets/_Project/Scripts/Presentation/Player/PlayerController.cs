// PlayerController.cs
// F1-13 — "PlayerController + InputAdapter: egyujjas húzás/koppintás →
// sávváltás" (math-horde.md 3.3). Pure forwarding, no business logic
// (unity-csharp skill: "MonoBehaviour-ban TILOS üzleti logika"):
//
//   gesture (from ILaneInputSource) → LaneGestureResolver (Core, pure)
//                                   → ICombatService.MovePlayerToLane
//
// The controller does NOT keep its own "current lane" field — ICombatService
// .PlayerLane is the single source of truth (game-logic.md 3.: "a controller
// ezt állítja, nem a saját állapotát duplikálja"); every gesture reads it
// fresh. Targeting/firing stay fully automatic (CombatService.Tick) and are
// never touched here. Answering a math event by reaching the right lane
// (spec 3.3's "A matematikai válasz kiválasztása a megfelelő sáv elérésével
// történik") needs no extra code in this class: it already happens through
// the same MovePlayerToLane → PlayerLaneChanged path once a later phase
// wires IMathEventDirector.OnPlayerLaneSelected to that event.
using System;
using MathHorde.Core.Common;
using MathHorde.GameLogic;
using MathHorde.Infrastructure.Input;
using UnityEngine;

namespace MathHorde.Presentation.Player
{
    public sealed class PlayerController : MonoBehaviour
    {
        private ICombatService _combatService;
        private ILaneInputSource _inputSource;
        private int _laneCount;
        private bool _isInitialized;

        /// <summary>
        /// Wires the controller to the combat service and the input source.
        /// <paramref name="laneCount"/> comes from the level's arena
        /// geometry (LaneDefinition.LaneCount / the scene's LaneAnchor
        /// count) — never hardcoded here (math-horde.md 3.2: "A feladatok
        /// 2-4 sávot használhatnak"). Call once from the scene's
        /// composition root (F1-17), before any gesture can arrive.
        /// </summary>
        public void Initialize(ICombatService combatService, ILaneInputSource inputSource, int laneCount)
        {
            _combatService = combatService ?? throw new ArgumentNullException(nameof(combatService));
            _inputSource = inputSource ?? throw new ArgumentNullException(nameof(inputSource));
            if (laneCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(laneCount), laneCount, "A sávszám legalább 1.");
            }

            _laneCount = laneCount;

            _inputSource.SwipeDetected += HandleSwipeDetected;
            _inputSource.TapDetected += HandleTapDetected;
            _isInitialized = true;
        }

        private void OnDestroy()
        {
            if (!_isInitialized)
            {
                return;
            }

            _inputSource.SwipeDetected -= HandleSwipeDetected;
            _inputSource.TapDetected -= HandleTapDetected;
        }

        private void HandleSwipeDetected(SwipeDirection direction)
        {
            LaneIndex target = LaneGestureResolver.ResolveSwipe(_combatService.PlayerLane, _laneCount, direction);
            _combatService.MovePlayerToLane(target);
        }

        private void HandleTapDetected(float normalizedX)
        {
            LaneIndex target = LaneGestureResolver.ResolveTap(_laneCount, normalizedX);
            _combatService.MovePlayerToLane(target);
        }
    }
}
