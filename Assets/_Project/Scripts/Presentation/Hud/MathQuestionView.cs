using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MathHorde.Presentation.Hud
{
    /// <summary>One preallocated question row. It never receives answers or option metadata.</summary>
    public sealed class MathQuestionView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _questionText;
        [SerializeField] private TMP_Text _timeText;
        [SerializeField] private Image _timeFill;

        public string QuestionText => _questionText.text;
        public string TimeText => _timeText.text;
        public float TimeFraction => _timeFill.fillAmount;
        public bool IsWired => _questionText != null && _timeText != null && _timeFill != null;

        public void SetQuestion(string text)
        {
            _questionText.text = text;
            gameObject.SetActive(true);
        }

        public void SetTime(string text, float fraction)
        {
            if (_timeText.text != text) _timeText.text = text;
            SetProgress(fraction);
        }

        public void SetProgress(float fraction) => _timeFill.fillAmount = Mathf.Clamp01(fraction);

        public void Clear()
        {
            _questionText.text = _timeText.text = string.Empty;
            _timeFill.fillAmount = 0;
            gameObject.SetActive(false);
        }
    }
}
