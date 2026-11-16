// Core contract for resolving player- and parent-facing text (ADR-007: code,
// identifiers and comments are English; every on-screen string is Hungarian
// and lives in a key-based string table, never as a literal in code).
//
// Key convention (documented here so every layer follows the same scheme):
//   "<area>.<item>" in lowerCamelCase segments, e.g.:
//     hud.health, hud.ammo, hud.shield, hud.wave, hud.level,
//     hud.pause, hud.challengeLevel, result.gameOver, result.retry
// Parameterized entries use positional placeholders ("{0}", "{1}", ...) with
// string.Format semantics, e.g. hud.wave = "{0}. hullám".
namespace MathHorde.Core.Common
{
    /// <summary>
    /// Key-based text resolution contract. Implementations must never embed
    /// Hungarian literals in code; they resolve lookup keys against a loaded
    /// string table (see Resources/Strings/hu.json).
    /// </summary>
    public interface ILocalizationService
    {
        /// <summary>
        /// Resolves a key with no parameters. Never throws and never returns
        /// null — falls back to "[[key]]" when the key is missing from the
        /// loaded table, so a missing translation is visible but not fatal.
        /// </summary>
        string Resolve(string key);

        /// <summary>
        /// Resolves a key and substitutes positional parameters into it
        /// (string.Format semantics), e.g. Resolve("hud.wave", 3) → "3. hullám".
        /// Falls back to "[[key]]" when the key is missing or the template
        /// cannot be formatted with the given arguments.
        /// </summary>
        string Resolve(string key, params object[] args);

        /// <summary>
        /// Attempts to resolve a key without triggering missing-key
        /// notification. Returns false (and value = null) when the key is
        /// not present in the loaded table.
        /// </summary>
        bool TryResolve(string key, out string value);
    }
}
