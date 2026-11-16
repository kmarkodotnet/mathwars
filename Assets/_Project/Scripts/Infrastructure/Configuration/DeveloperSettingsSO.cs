// DeveloperSettingsSO.cs
// F1-09 — contract scriptable-object-schema.md 12. DeveloperSettingsSO (F1).
// Build-ben kikapcsolt: EnableDevTools = false release-ben, a tényleges
// #if DEVELOPMENT_BUILD kapu a felhasználó (GameSessionService vagy a bootstrap
// scene) dolga, nem ennek az assetnek — ez csak az adatot hordozza.
//
// ForceNextProblemType implementation note: the contract types it
// "ProblemType? / enum / null" (null = no override), but Unity's built-in
// serializer has no surrogate for System.Nullable<T> on a SerializeField — a
// nullable enum field would silently fail to serialize. Modelled instead as an
// explicit enabled-flag + enum pair, which is semantically identical (disabled
// == null == "use the normal value") and actually round-trips through the
// Inspector. Future devs: don't swap this back to `ProblemType?`.
using System.Collections.Generic;
using MathHorde.Core.Problems;
using UnityEngine;

namespace MathHorde.Infrastructure.Configuration
{
    [CreateAssetMenu(fileName = "DeveloperSettings", menuName = "MathHorde/Developer Settings")]
    public sealed class DeveloperSettingsSO : ScriptableObject, IValidatableConfig
    {
        [SerializeField] private bool _enableDevTools = true;

        [Tooltip("0 = nincs kényszerítés.")]
        [Range(0, 10)]
        [SerializeField] private int _forceChallengeLevel;

        [Tooltip("-1 = nincs kényszerítés.")]
        [Range(-1, 999)]
        [SerializeField] private int _forceAmmo = -1;

        [Tooltip("-1 = nincs kényszerítés.")]
        [Range(-1, 999)]
        [SerializeField] private int _forceHealth = -1;

        [SerializeField] private bool _forceNextProblemTypeEnabled;
        [SerializeField] private ProblemType _forceNextProblemType = ProblemType.AnswerLaneSelection;

        [Tooltip("0 = véletlen.")]
        [SerializeField] private int _forceSeed;

        [SerializeField] private bool _godMode;
        [SerializeField] private bool _infiniteAmmo;

        [Range(0.1f, 5.0f)]
        [SerializeField] private float _timeScale = 1.0f;

        [Range(0, 49)]
        [SerializeField] private int _skipToWaveIndex;

        [SerializeField] private bool _showDebugOverlay;
        [SerializeField] private bool _logAdaptationDecisions = true;

        public bool EnableDevTools => _enableDevTools;
        public int ForceChallengeLevel => _forceChallengeLevel;
        public int ForceAmmo => _forceAmmo;
        public int ForceHealth => _forceHealth;

        public ProblemType? ForceNextProblemType =>
            _forceNextProblemTypeEnabled ? _forceNextProblemType : (ProblemType?)null;

        public int ForceSeed => _forceSeed;
        public bool GodMode => _godMode;
        public bool InfiniteAmmo => _infiniteAmmo;
        public float TimeScale => _timeScale;
        public int SkipToWaveIndex => _skipToWaveIndex;
        public bool ShowDebugOverlay => _showDebugOverlay;
        public bool LogAdaptationDecisions => _logAdaptationDecisions;

        public IReadOnlyList<string> Validate()
        {
            var errors = new List<string>();

            ConfigValidation.RequireRange(errors, nameof(ForceChallengeLevel), _forceChallengeLevel, 0, 10);
            ConfigValidation.RequireRange(errors, nameof(ForceAmmo), _forceAmmo, -1, 999);
            ConfigValidation.RequireRange(errors, nameof(ForceHealth), _forceHealth, -1, 999);
            ConfigValidation.RequireRange(errors, nameof(TimeScale), _timeScale, 0.1f, 5.0f);
            ConfigValidation.RequireRange(errors, nameof(SkipToWaveIndex), _skipToWaveIndex, 0, 49);

            return errors;
        }

        private void OnValidate()
        {
            foreach (string error in Validate())
            {
                Debug.LogError($"{name} ({nameof(DeveloperSettingsSO)}): {error}", this);
            }
        }
    }
}
