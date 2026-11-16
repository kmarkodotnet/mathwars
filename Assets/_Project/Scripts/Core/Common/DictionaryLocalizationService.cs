using System;
using System.Collections.Generic;

namespace MathHorde.Core.Common
{
    /// <summary>
    /// Unity-independent ILocalizationService implementation backed by an
    /// in-memory key-to-text dictionary. Takes no dependency on UnityEngine
    /// or any JSON library, so it is directly testable in the headless
    /// mirror test solution (game/tests/MathHorde.Tests.sln). The Unity-
    /// facing loader that reads Resources/Strings/hu.json lives in the
    /// Infrastructure layer (ResourcesLocalizationService) and simply feeds
    /// the parsed dictionary into this class — see ARCHITECTURE.md 2.3 for
    /// why Core must stay free of UnityEngine and JSON-library references.
    /// </summary>
    public sealed class DictionaryLocalizationService : ILocalizationService
    {
        private readonly IReadOnlyDictionary<string, string> _entries;
        private readonly Action<string> _onMissingKey;

        /// <param name="entries">Resolved key-to-text table (e.g. parsed from hu.json).</param>
        /// <param name="onMissingKey">
        /// Optional callback invoked whenever Resolve() is asked for a key
        /// that is not present. Infrastructure wires this to Debug.LogWarning
        /// so a missing translation is loud in the Editor/device log without
        /// crashing gameplay.
        /// </param>
        public DictionaryLocalizationService(
            IReadOnlyDictionary<string, string> entries,
            Action<string> onMissingKey = null)
        {
            _entries = entries ?? new Dictionary<string, string>();
            _onMissingKey = onMissingKey;
        }

        public bool TryResolve(string key, out string value)
        {
            if (key != null && _entries.TryGetValue(key, out value))
            {
                return true;
            }

            value = null;
            return false;
        }

        public string Resolve(string key)
        {
            if (TryResolve(key, out var value))
            {
                return value;
            }

            _onMissingKey?.Invoke(key);
            return MissingKeyPlaceholder(key);
        }

        public string Resolve(string key, params object[] args)
        {
            if (TryResolve(key, out var template))
            {
                try
                {
                    return string.Format(template, args ?? Array.Empty<object>());
                }
                catch (FormatException)
                {
                    _onMissingKey?.Invoke(key);
                    return MissingKeyPlaceholder(key);
                }
            }

            _onMissingKey?.Invoke(key);
            return MissingKeyPlaceholder(key);
        }

        private static string MissingKeyPlaceholder(string key)
        {
            // Deliberately loud but non-crashing: a missing translation
            // shows up as literal "[[key]]" text on screen instead of
            // throwing or silently showing blank text.
            return "[[" + key + "]]";
        }
    }
}
