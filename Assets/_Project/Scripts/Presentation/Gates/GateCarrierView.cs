using UnityEngine;
using TMPro;
using MathHorde.GameLogic;
using MathHorde.Core.Problems;

namespace MathHorde.Presentation.Gates
{
    /// <summary>Visual math carrier, deliberately separate from EnemyView and combat targets.</summary>
    [DisallowMultipleComponent]
    public sealed class GateCarrierView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _optionText;
        [SerializeField] private TMP_Text _contextText;
        [SerializeField] private GameObject[] _goalIcons = System.Array.Empty<GameObject>();

        public MathEventId EventId { get; private set; }
        public int OptionIndex { get; private set; } = -1;
        public string OptionText => _optionText.text;
        public string ContextText => _contextText.text;

        public void Bind(MathEventId eventId, int optionIndex, string option, string context, GateGoal? goal)
        {
            EventId = eventId;
            OptionIndex = optionIndex;
            _optionText.text = option;
            _contextText.text = context;
            for (int i = 0; i < _goalIcons.Length; i++) _goalIcons[i].SetActive(goal.HasValue && (int)goal.Value == i);
            gameObject.SetActive(true);
        }

        public void SetPose(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
        }

        public void Unbind()
        {
            EventId = default;
            OptionIndex = -1;
            _optionText.text = string.Empty;
            _contextText.text = string.Empty;
            for (int i = 0; i < _goalIcons.Length; i++) _goalIcons[i].SetActive(false);
            gameObject.SetActive(false);
        }
    }
}
