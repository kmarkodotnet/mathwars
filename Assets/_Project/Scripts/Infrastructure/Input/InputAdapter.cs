// InputAdapter.cs
// F1-13 — one-finger touch → lane-swipe/tap gesture adapter (math-horde.md
// 3.3). Uses the Unity Input System's Pointer device (covers touchscreen AND
// mouse, so the gesture is exercisable in the Editor with a mouse) — never
// the legacy UnityEngine.Input class and never OnMouseDown, per the
// unity-ui skill ("swipe/tap bemenet az Input System-en keresztül").
//
// This MonoBehaviour is deliberately thin: Update() only tracks the press/
// release position of a single pointer. ALL of the actual decision logic —
// "is this a tap or a swipe, and which direction" — lives in the
// Unity-independent MathHorde.Core.Common.LaneGestureResolver, so the only
// untested-by-EditMode part of this class is the one-line Pointer polling
// itself. ApplyGesture is public (not private) specifically so a PlayMode
// test can drive the classification with plain Vector2 values, without
// needing to simulate a real Input System device.
using UnityEngine;
using UnityEngine.InputSystem;
using MathHorde.Core.Common;

namespace MathHorde.Infrastructure.Input
{
    public sealed class InputAdapter : MonoBehaviour, ILaneInputSource
    {
        [Tooltip("Egy húzás ennyi pixelnél rövidebb elmozdulása koppintásnak számít, nem sávváltásnak.")]
        [SerializeField] private float _minSwipeDistancePixels = 60f;

        public event System.Action<SwipeDirection> SwipeDetected;
        public event System.Action<float> TapDetected;

        private bool _isTracking;
        private Vector2 _pressPosition;
        private System.Func<Vector2, bool> _blocksGameplayInput;
        public void SetUiInputBlocker(System.Func<Vector2, bool> blocksGameplayInput) => _blocksGameplayInput = blocksGameplayInput;

        private void Update()
        {
            Pointer pointer = Pointer.current;
            if (pointer == null)
            {
                return;
            }

            if (pointer.press.wasPressedThisFrame)
            {
                if (_blocksGameplayInput != null && _blocksGameplayInput(pointer.position.ReadValue()))
                {
                    _isTracking = false;
                    return;
                }
                _isTracking = true;
                _pressPosition = pointer.position.ReadValue();
                return;
            }

            if (_isTracking && pointer.press.wasReleasedThisFrame)
            {
                _isTracking = false;
                ApplyGesture(_pressPosition, pointer.position.ReadValue(), Screen.width);
            }
        }

        /// <summary>
        /// Classifies a completed press→release gesture and raises exactly
        /// one of SwipeDetected/TapDetected (or neither, for an ambiguous
        /// gesture — e.g. a vertical drag that is neither a tap nor a
        /// lane-swipe). Public so tests and any future input-replay tooling
        /// can call it directly with known positions.
        /// </summary>
        public void ApplyGesture(Vector2 pressPosition, Vector2 releasePosition, float screenWidth)
        {
            Vector2 delta = releasePosition - pressPosition;

            if (Mathf.Abs(delta.x) < _minSwipeDistancePixels && Mathf.Abs(delta.y) < _minSwipeDistancePixels)
            {
                float normalizedX = screenWidth > 0f ? Mathf.Clamp01(releasePosition.x / screenWidth) : 0f;
                TapDetected?.Invoke(normalizedX);
                return;
            }

            SwipeDirection? swipe = LaneGestureResolver.ClassifyDrag(delta.x, delta.y, _minSwipeDistancePixels);
            if (swipe.HasValue)
            {
                SwipeDetected?.Invoke(swipe.Value);
            }
        }
    }
}
