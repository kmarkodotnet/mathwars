// TmpHungarianGlyphCoverageTests.cs
// F1-19 — "TextMeshPro betűtípus és magyar glyph-lefedettség".
//
// A projektben korábban soha nem volt beállítva alapértelmezett TMP
// betűtípus (TMP_Settings.m_defaultFontAsset), és a com.unity.ugui csomag
// önmagában nem is tartalmazza a TMP futásidejű shadereit (lásd
// Assets/Editor/TmpHungarianFontSetup.cs fejkommentje) — emiatt minden
// magyar szöveg a HUD-on/eredményképernyőn hiányzó glyph-ekkel (üres
// négyzetekkel) jelenhetett volna meg build/Play módban, a kettős hosszú
// ékezetű ő/ű betűknél biztosan (ezek azok, amik egy szűkebb latin-1-only
// betűkészletből tipikusan hiányoznak).
//
// Ez a teszt NEM mintavételez — a hu.json string-tábla MINDEN jelenlegi
// értékét végigfuttatja a tényleges alapértelmezett font asseten keresztül
// (TMP_Settings.defaultFontAsset.HasCharacters), ugyanazzal a betöltési
// úttal (Resources.Load<TextAsset>("Strings/hu") + Newtonsoft JSON), amit
// ResourcesLocalizationService is használ futásidőben — így a jövőben
// hu.json-hoz hozzáadott új kulcsokra is véd, nem csak a mai állapotra.
//
// UnityEngine (TMPro, Resources) típusokat érint -> PlayMode teszt, nem
// EditMode (lásd unity-testing skill: a tükör .NET solution nem fordulna).
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;

namespace MathHorde.Tests.PlayMode.Common
{
    [TestFixture]
    public class TmpHungarianGlyphCoverageTests
    {
        private const string StringTableResourcePath = "Strings/hu";

        private static Dictionary<string, string> LoadStringTableOrFail()
        {
            TextAsset textAsset = Resources.Load<TextAsset>(StringTableResourcePath);
            Assert.That(textAsset, Is.Not.Null,
                $"Hiányzik a string-tábla: Resources/{StringTableResourcePath}.json.");

            Dictionary<string, string> table = JsonConvert.DeserializeObject<Dictionary<string, string>>(textAsset.text);
            Assert.That(table, Is.Not.Null, "hu.json nem parse-olható Dictionary<string,string>-ként.");
            Assert.That(table.Count, Is.GreaterThan(0), "hu.json üres — nincs mit lefedettséget ellenőrizni.");

            return table;
        }

        private static TMP_FontAsset LoadDefaultFontAssetOrFail()
        {
            TMP_FontAsset fontAsset = TMP_Settings.defaultFontAsset;
            Assert.That(fontAsset, Is.Not.Null,
                "TMP_Settings.defaultFontAsset nincs beállítva — lásd " +
                "Assets/Editor/TmpHungarianFontSetup.cs (F1-19). Futtasd a " +
                "\"Math Horde/UI/Ensure Hungarian TMP Default Font\" menüpontot.");

            return fontAsset;
        }

        [Test]
        public void DefaultFontAsset_IsConfigured_NotEmpty()
        {
            TMP_FontAsset fontAsset = LoadDefaultFontAssetOrFail();

            Assert.That(fontAsset.characterTable.Count, Is.GreaterThan(0),
                "A TMP_Settings.defaultFontAsset karaktertáblája üres — a betűtípus asset " +
                "nincs előre betöltve karakterekkel (lásd TmpHungarianFontSetup.EnsureFontAsset).");
        }

        [Test]
        public void EveryHuJsonValue_RendersWithoutMissingGlyphs()
        {
            TMP_FontAsset fontAsset = LoadDefaultFontAssetOrFail();
            Dictionary<string, string> table = LoadStringTableOrFail();

            var failures = new List<string>();

            foreach (KeyValuePair<string, string> entry in table)
            {
                bool hasAll = fontAsset.HasCharacters(entry.Value, out uint[] missing, searchFallbacks: false, tryAddCharacter: true);
                if (!hasAll)
                {
                    string missingDescription = DescribeMissingCharacters(missing);
                    failures.Add($"\"{entry.Key}\" = \"{entry.Value}\" -> hiányzó glyph(ek): {missingDescription}");
                }
            }

            Assert.That(failures.Count, Is.EqualTo(0),
                "A következő hu.json kulcsok szövege hiányzó glyph-fel renderelődne:\n" +
                string.Join("\n", failures));
        }

        // Konkrétan a magyar kettős hosszú ékezetre (ő/ű) — a feladat ezekre
        // hívja fel a figyelmet: pont ezek azok a karakterek, amik egy
        // szűkebb, latin-1-only betűkészletből tipikusan hiányoznak, míg a
        // sima á/é/í/ó/ú gyakran megvan. A fenti általános teszt már lefedi
        // ezt is (mert a hu.json tartalmaz ilyen szavakat, pl. "Életerő",
        // "Lőszer", "Következő"), de ez a teszt explicit, névvel dokumentált
        // védelmet ad a leginkább elfelejtett karakterekre akkor is, ha egy
        // jövőbeli hu.json-szerkesztés épp nem tartalmazná őket.
        [TestCase('ő')]
        [TestCase('Ő')]
        [TestCase('ű')]
        [TestCase('Ű')]
        [TestCase('á')]
        [TestCase('é')]
        [TestCase('í')]
        [TestCase('ó')]
        [TestCase('ö')]
        [TestCase('ú')]
        [TestCase('ü')]
        public void DefaultFontAsset_HasEveryHungarianAccentedCharacter(char accentedCharacter)
        {
            TMP_FontAsset fontAsset = LoadDefaultFontAssetOrFail();

            bool hasCharacter = fontAsset.HasCharacter(accentedCharacter, searchFallbacks: false, tryAddCharacter: true);

            Assert.That(hasCharacter, Is.True,
                $"Az alapértelmezett betűtípus-asset nem tartalmazza a '{accentedCharacter}' " +
                $"(U+{(int)accentedCharacter:X4}) magyar ékezetes karaktert.");
        }

        private static string DescribeMissingCharacters(uint[] missing)
        {
            if (missing == null || missing.Length == 0)
            {
                return "(ismeretlen)";
            }

            var sb = new StringBuilder();
            for (int i = 0; i < missing.Length; i++)
            {
                if (i > 0)
                {
                    sb.Append(", ");
                }

                uint codePoint = missing[i];
                sb.Append($"U+{codePoint:X4}");
                if (codePoint <= 0x10FFFF)
                {
                    sb.Append($" ('{char.ConvertFromUtf32((int)codePoint)}')");
                }
            }

            return sb.ToString();
        }
    }
}
