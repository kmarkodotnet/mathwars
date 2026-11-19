using UnityEngine;

namespace MathHorde.Presentation.Arena
{
    /// <summary>Ground-level outlines: two long rails + chevron for selection, a short box for a decision window.</summary>
    public sealed class LaneHighlightView : MonoBehaviour
    {
        [SerializeField] private GameObject _selected;
        [SerializeField] private GameObject _decision;
        [SerializeField] private Transform _leftRail, _rightRail, _pointer;
        [SerializeField] private float _surfaceHeight = .09f;
        public int LaneIndex { get; private set; }
        public bool IsSelected => _selected.activeSelf;
        public bool IsDecisionLane => _decision.activeSelf;
        public bool IsConfigured => _selected != null && _decision != null && _leftRail != null && _rightRail != null && _pointer != null;

        public void Bind(LaneAnchor anchor, float decisionZoneDepth)
        {
            LaneIndex = anchor.LaneIndex;
            // Anchors expose world X like PlayerView/GatePresenter; parent pose does not redefine that coordinate.
            transform.SetPositionAndRotation(new Vector3(anchor.CenterX, _surfaceHeight, 0), Quaternion.identity);
            _leftRail.localPosition = new Vector3(-anchor.Width * .46f, 0, anchor.Length * .5f);
            _rightRail.localPosition = new Vector3(anchor.Width * .46f, 0, anchor.Length * .5f);
            _leftRail.localScale = _rightRail.localScale = new Vector3(.055f, .012f, anchor.Length);
            _pointer.localPosition = new Vector3(0, .015f, .65f);
            _pointer.localScale = new Vector3(anchor.Width * .35f, 1, 1);
            _decision.transform.localPosition = new Vector3(0, .025f, decisionZoneDepth);
            _decision.transform.localScale = new Vector3(anchor.Width * .90f, 1, 1);
        }

        public void SetState(bool selected, bool decision)
        {
            if (_selected.activeSelf != selected) _selected.SetActive(selected);
            if (_decision.activeSelf != decision) _decision.SetActive(decision);
        }

        public void Clear() => SetState(false, false);
    }
}
