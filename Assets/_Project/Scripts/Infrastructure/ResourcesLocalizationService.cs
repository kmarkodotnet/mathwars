using System.Collections.Generic;
using MathHorde.Core.Common;
using Newtonsoft.Json;
using UnityEngine;

namespace MathHorde.Infrastructure
{
    /// <summary>
    /// Loads the Hungarian string table from Resources/Strings/hu.json and
    /// exposes it through ILocalizationService. The resolution/formatting/
    /// missing-key logic itself is Unity-independent
    /// (MathHorde.Core.Common.DictionaryLocalizationService) — this class is
    /// intentionally thin: it only knows how to read a TextAsset via
    /// Resources, parse it with Newtonsoft.Json, cache the result in memory,
    /// and wire a Debug.LogWarning callback for missing keys. Because it
    /// depends on UnityEngine it is exercised by Unity PlayMode/EditMode
    /// Play-testing rather than the headless tests/MathHorde.Tests.sln
    /// mirror (see game/docs/ARCHITECTURE.md 2.3 for the layering rationale).
    /// </summary>
    public sealed class ResourcesLocalizationService : ILocalizationService
    {
        private const string DefaultResourcePath = "Strings/hu";

        // Process-wide cache keyed by resource path, so repeated construction
        // (e.g. from multiple screens) does not re-hit Resources.Load/JSON
        // parsing — the on-disk table is read at most once per path.
        private static readonly Dictionary<string, IReadOnlyDictionary<string, string>> _cache =
            new Dictionary<string, IReadOnlyDictionary<string, string>>();

        private readonly ILocalizationService _inner;

        public ResourcesLocalizationService(string resourcePath = DefaultResourcePath)
        {
            _inner = new DictionaryLocalizationService(LoadEntries(resourcePath), LogMissingKey);
        }

        public string Resolve(string key) => _inner.Resolve(key);

        public string Resolve(string key, params object[] args) => _inner.Resolve(key, args);

        public bool TryResolve(string key, out string value) => _inner.TryResolve(key, out value);

        private static IReadOnlyDictionary<string, string> LoadEntries(string resourcePath)
        {
            if (_cache.TryGetValue(resourcePath, out var cached))
            {
                return cached;
            }

            var entries = ReadEntriesFromResources(resourcePath);
            _cache[resourcePath] = entries;
            return entries;
        }

        private static IReadOnlyDictionary<string, string> ReadEntriesFromResources(string resourcePath)
        {
            var textAsset = Resources.Load<TextAsset>(resourcePath);
            if (textAsset == null)
            {
                Debug.LogWarning(
                    $"[Localization] String table not found at Resources/{resourcePath}.json — " +
                    "every key will render as its [[key]] placeholder.");
                return new Dictionary<string, string>();
            }

            try
            {
                var parsed = JsonConvert.DeserializeObject<Dictionary<string, string>>(textAsset.text);
                return parsed ?? new Dictionary<string, string>();
            }
            catch (JsonException ex)
            {
                Debug.LogError($"[Localization] Failed to parse Resources/{resourcePath}.json: {ex.Message}");
                return new Dictionary<string, string>();
            }
        }

        private static void LogMissingKey(string key)
        {
            Debug.LogWarning($"[Localization] Missing key: \"{key}\"");
        }
    }
}
