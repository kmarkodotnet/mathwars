// SafeAreaFitter.cs
// F1-15 — generic, screen-agnostic helper: shrinks its own RectTransform to
// Screen.safeArea so sibling content never lands under a notch/cutout
// (unity-ui skill: "Safe Area kezelés ... minden teljes képernyős
// elrendezésnél"). Reusable by any full-screen Canvas root (HUD now, menus/
// dashboards later) — contains no HUD-specific or gameplay logic.
//
// Re-applies on Update() only by comparing the cached safe area/screen size
// against the current one (cheap struct comparisons, no allocation) so a
// runtime orientation/resolution change (foldables, split-screen) is picked
// up without doing real work every frame.
using UnityEngine;

namespace MathHorde.Presentation.UI
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform _rectTransform;
        private Rect _lastSafeArea;
        private Vector2Int _lastScreenSize;
        private ScreenOrientation _lastOrientation;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            Apply();
        }

        private void Update()
        {
            if (HasScreenMetricsChanged())
            {
                Apply();
            }
        }

        private bool HasScreenMetricsChanged()
        {
            return Screen.safeArea != _lastSafeArea
                || Screen.width != _lastScreenSize.x
                || Screen.height != _lastScreenSize.y
                || Screen.orientation != _lastOrientation;
        }

        private void Apply()
        {
            Rect safeArea = Screen.safeArea;
            _lastSafeArea = safeArea;
            _lastScreenSize = new Vector2Int(Screen.width, Screen.height);
            _lastOrientation = Screen.orientation;

            if (Screen.width <= 0 || Screen.height <= 0)
            {
                return;
            }

            Vector2 anchorMin = safeArea.position;
            Vector2 anchorMax = safeArea.position + safeArea.size;
            anchorMin.x /= Screen.width;
            anchorMin.y /= Screen.height;
            anchorMax.x /= Screen.width;
            anchorMax.y /= Screen.height;

            _rectTransform.anchorMin = anchorMin;
            _rectTransform.anchorMax = anchorMax;
        }
    }
}
