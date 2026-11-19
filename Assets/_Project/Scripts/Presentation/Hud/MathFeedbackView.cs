using TMPro;
using UnityEngine;

namespace MathHorde.Presentation.Hud
{
    /// <summary>One prewarmed feedback row; all text is supplied by the presenter.</summary>
    public sealed class MathFeedbackView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _summary;
        [SerializeField] private TMP_Text[] _results;
        [SerializeField] private TMP_Text[] _markers;
        [SerializeField] private RectTransform[] _lanes;
        private int _rowIndex = -1, _rowCount = -1;

        public string Summary => _summary.text;
        public bool IsWired => _summary != null && _results != null && _markers != null && _lanes != null
            && _results.Length == 4 && _markers.Length == 4 && _lanes.Length == 4
            && ValidLanes();

        private bool ValidLanes()
        {
            for (int i = 0; i < 4; i++)
                if (_results[i] == null || _markers[i] == null || _lanes[i] == null) return false;
            return true;
        }

        public void BindLanes(int count)
        {
            for (int i = 0; i < _lanes.Length; i++)
            {
                _lanes[i].anchorMin = new Vector2(i / (float)count, 0);
                _lanes[i].anchorMax = new Vector2((i + 1) / (float)count, .72f);
                _lanes[i].offsetMin = new Vector2(4, 2); _lanes[i].offsetMax = new Vector2(-4, -2);
                _lanes[i].gameObject.SetActive(i < count);
            }
        }

        public string Result(int lane) => _results[lane].text;
        public string Marker(int lane) => _markers[lane].text;

        public void SetSummary(string text) { _summary.text = text; gameObject.SetActive(true); }

        public void SetLane(int lane, string result, string marker, bool best, bool selected)
        {
            _lanes[lane].gameObject.SetActive(!string.IsNullOrEmpty(result));
            _results[lane].text = result;
            _markers[lane].text = marker;
            // Text also names both states; color is never the only signal.
            _markers[lane].color = best ? new Color(.4f, 1, .7f) : selected ? new Color(1, .8f, .35f) : Color.white;
        }

        public void Place(int index, int count)
        {
            if (_rowIndex == index && _rowCount == count) return;
            _rowIndex = index; _rowCount = count;
            var rect = (RectTransform)transform;
            rect.anchorMin = new Vector2(0, 1 - (index + 1f) / count);
            rect.anchorMax = new Vector2(1, 1 - index / (float)count);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        public void Clear()
        {
            _summary.text = string.Empty;
            for (int i = 0; i < 4; i++) { _results[i].text = _markers[i].text = string.Empty; }
            gameObject.SetActive(false);
        }
    }
}
