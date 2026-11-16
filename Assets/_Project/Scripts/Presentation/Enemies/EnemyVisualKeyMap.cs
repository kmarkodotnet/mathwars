// EnemyVisualKeyMap.cs
// F1-14 — resolves a GameLogic EnemyDefinition.EnemyId (catalog string key,
// e.g. "basic_bot") to a Presentation-only "visual key" (e.g. "basic") that
// selects which pooled prefab/material set represents it on screen.
//
// Deliberately pure and engine-free: this is the resolution ALGORITHM only
// (dictionary lookup + fallback), kept separate from EnemyVisualCatalogSO
// (the ScriptableObject asset that actually owns prefab references) so the
// mapping logic itself — "does a known id resolve to its key, does an
// unknown id fall back safely" — is unit-testable without any Unity asset
// machinery (math-horde.md 3.1: enemies must be visually distinguishable by
// type; this is what guarantees an unrecognised EnemyId still renders as
// *something* instead of silently producing no visual).
using System;
using System.Collections.Generic;

namespace MathHorde.Presentation.Enemies
{
    public sealed class EnemyVisualKeyMap
    {
        private readonly Dictionary<string, string> _visualKeyByEnemyId;
        private readonly string _fallbackVisualKey;

        public EnemyVisualKeyMap(IReadOnlyDictionary<string, string> visualKeyByEnemyId, string fallbackVisualKey)
        {
            if (visualKeyByEnemyId == null)
            {
                throw new ArgumentNullException(nameof(visualKeyByEnemyId));
            }

            if (string.IsNullOrEmpty(fallbackVisualKey))
            {
                throw new ArgumentException("fallbackVisualKey nem lehet üres.", nameof(fallbackVisualKey));
            }

            _visualKeyByEnemyId = new Dictionary<string, string>(visualKeyByEnemyId, StringComparer.Ordinal);
            _fallbackVisualKey = fallbackVisualKey;
        }

        /// <summary>Every visual key this map can resolve to, including the fallback — used to prewarm every pool up front.</summary>
        public IReadOnlyCollection<string> AllVisualKeys
        {
            get
            {
                var keys = new HashSet<string>(_visualKeyByEnemyId.Values, StringComparer.Ordinal) { _fallbackVisualKey };
                return keys;
            }
        }

        public string Resolve(string enemyId)
        {
            if (!string.IsNullOrEmpty(enemyId) && _visualKeyByEnemyId.TryGetValue(enemyId, out string visualKey))
            {
                return visualKey;
            }

            return _fallbackVisualKey;
        }
    }
}
