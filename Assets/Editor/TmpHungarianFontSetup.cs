// TmpHungarianFontSetup.cs
// F1-19 — "TextMeshPro betűtípus és magyar glyph-lefedettség".
//
// Előzmény (unity-ui skill, "TMP Essential Resources — KRITIKUS, HIÁNYZOTT
// ALAPÍTÁS" szakasz, F1-17): a projektben soha nem lett importálva a TMP
// Essential Resources. Ez KÉT, egymástól független hiányt okozott (ez a
// feladat tárta fel a másodikat, a vizsgálat közben):
//   1. Nem volt alapértelmezett betűtípus (TMP_Settings.m_defaultFontAsset
//      üres) — emiatt minden magyar szöveg hiányzó glyph-ekkel jelent volna
//      meg, az ő/ű ékezeteknél biztosan.
//   2. SÚLYOSABB, menet közben felfedezett hiány: a com.unity.ugui csomag
//      saját maga NEM tartalmazza a TMP futásidejű shadereit (pl.
//      "TextMeshPro/Mobile/Distance Field") — azok KIZÁRÓLAG a TMP
//      Essential Resources unitypackage részei. Enélkül MINDEN TMP-szöveg
//      törött anyaggal/color nélkül jelenne meg, FÜGGETLENÜL a
//      betűtípustól — ezt az igazolta, hogy egy saját TMP_FontAsset
//      generálása (TMP_FontAsset.CreateFontAsset) ArgumentNullException-nel
//      bukott a hiányzó Shader.Find("TextMeshPro/Mobile/Distance Field")
//      miatt, -batchmode-ban ÉS anélkül is (tehát nem grafikai
//      eszköz-elérhetőségi, hanem tényleges fájlhiány).
//
// Megoldás, a két felkínált lehetőség közül választva:
//   (A) a teljes "TMP Essential Resources.unitypackage" importálása
//       AssetDatabase.ImportPackage-dzsel — DOKUMENTÁLTAN elvetve: aszinkron,
//       egyetlen -batchmode -quit -executeMethod hívás életciklusában
//       megfigyelten soha nem fejeződött be (lásd EnsureTmpEssentialResourcesImported
//       fejkommentje GameplaySceneBuilder.cs-ben).
//   (B) EZ a megoldás: a csomag TARTALMÁT (shaderek, LiberationSans.ttf, a
//       gyári "LiberationSans SDF.asset" és kísérői, TMP Settings.asset,
//       sprite-asset) közvetlenül, EREDETI GUID-dal verziózzuk be a repóba a
//       kanonikus "Assets/TextMesh Pro/..." útvonalon (ugyanoda, ahova a
//       kézi menüpont is tenné) — ez pontosan az, amit "Import TMP
//       Essential Resources" tenne, csak szinkron fájlmásolással, nem
//       AssetDatabase.ImportPackage-dzsel. A gyári "LiberationSans
//       SDF.asset" viszont csak ASCII karakterkészlettel generált (nem
//       fedi a magyar ő/ű-t sem) — ezért EZ A FÁJL a már működő shaderekre
//       építve, Editor-API-val (TMP_FontAsset.CreateFontAsset +
//       TryAddCharacters) generál egy SAJÁT, projekt-specifikus
//       TMP_FontAsset-et (Assets/_Project/Resources/Fonts/), A TELJES
//       MAGYAR ÉKEZETKÉSZLETTEL előre betöltve, és ezt állítja be
//       alapértelmezettként — a gyári stock asset érintetlen marad
//       (diffelhető az eredeti csomaggal).
//
// A LiberationSans.ttf cmap-táblája ellenőrizve (lásd Napló): tartalmazza a
// teljes magyar ékezetkészletet, az ő/ű (U+0151/U+0171, kettős hosszú
// ékezet) is beleértve — ezek azok a karakterek, amik egy szűkebb
// latin-1-only betűkészletből tipikusan hiányoznak.
using System;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

public static class TmpHungarianFontSetup
{
    public const string SourceFontPath = "Assets/TextMesh Pro/Fonts/LiberationSans.ttf";
    public const string FontAssetPath = "Assets/_Project/Resources/Fonts/LiberationSans SDF.asset";
    private const string TmpSettingsAssetPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

    /// <summary>
    /// A magyar ábécé nem-ASCII (ékezetes) betűi, kis- és nagybetűvel. A
    /// hu.json string-tábla ennél szűkebb halmazt használ jelenleg, de ez a
    /// TELJES magyar ékezetkészlet — így a jövőben hu.json-hoz hozzáadott új
    /// kulcsok is eleve le vannak fedve, nem csak a mai állapot.
    /// </summary>
    public const string HungarianAccentedCharacters = "áéíóöőúüűÁÉÍÓÖŐÚÜŰ";

    /// <summary>
    /// A font assetbe előre betöltött teljes karakterkészlet: nyomtatható
    /// ASCII (0x20–0x7E — ez fedi a számokat, írásjeleket és a "{0}" formátum-
    /// helyőrzőket is) + a teljes magyar ékezetkészlet.
    /// </summary>
    public static string BuildRequiredCharacterSet()
    {
        var sb = new StringBuilder();
        for (int c = 0x20; c <= 0x7E; c++)
        {
            sb.Append((char)c);
        }

        sb.Append(HungarianAccentedCharacters);
        sb.Append("×÷−"); // Core expression notation; needed by the question HUD.
        return sb.ToString();
    }

    [MenuItem("Math Horde/UI/Ensure Hungarian TMP Default Font")]
    public static void EnsureAllMenuEntry() => EnsureAll();

    /// <summary>
    /// Idempotens belépési pont: létrehozza (ha hiányzik) a magyar
    /// ékezetkészletet tartalmazó font assetet, és beállítja a TMP Settings
    /// alapértelmezett betűtípusaként. Újrafuttatva nem duplikál semmit.
    /// </summary>
    public static void EnsureAll()
    {
        TMP_FontAsset fontAsset = EnsureFontAsset();
        EnsureDefaultFontAssignment(fontAsset);
        AssetDatabase.SaveAssets();
    }

    public static TMP_FontAsset EnsureFontAsset()
    {
        TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        if (existing != null)
        {
            // Extend the existing asset in place so every prefab keeps its font GUID.
            string required = BuildRequiredCharacterSet();
            if (!existing.HasCharacters(required, out uint[] _, searchFallbacks: false, tryAddCharacter: false))
            {
                existing.atlasPopulationMode = AtlasPopulationMode.Dynamic;
                bool added = existing.TryAddCharacters(required, out string missing);
                existing.atlasPopulationMode = AtlasPopulationMode.Static;
                if (!added) throw new InvalidOperationException("Required TMP glyphs missing: " + missing);
                EditorUtility.SetDirty(existing);
                foreach (var atlas in existing.atlasTextures) EditorUtility.SetDirty(atlas);
                AssetDatabase.SaveAssets();
            }
            return existing;
        }

        Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
        if (sourceFont == null)
        {
            throw new InvalidOperationException(
                $"[TmpHungarianFontSetup] Hiányzik a forrás betűtípus: {SourceFontPath}. " +
                "Ez a LiberationSans.ttf (SIL Open Font License) a TMP Essential Resources " +
                "csomagból kibontva, verziózva a repóban — lásd " +
                "\"Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt\".");
        }

        TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
            sourceFont,
            samplingPointSize: 90,
            atlasPadding: 9,
            renderMode: GlyphRenderMode.SDFAA,
            atlasWidth: 1024,
            atlasHeight: 1024,
            atlasPopulationMode: AtlasPopulationMode.Dynamic,
            enableMultiAtlasSupport: true);

        if (fontAsset == null)
        {
            throw new InvalidOperationException(
                $"[TmpHungarianFontSetup] TMP_FontAsset.CreateFontAsset sikertelen a(z) {SourceFontPath} forrásból.");
        }

        fontAsset.name = "LiberationSans SDF";

        // Előre betöltjük (Dynamic populációs mód mellett, mert
        // TryAddCharacters Static módban elutasítaná a hívást) a teljes
        // szükséges karakterkészletet, hogy a kész .asset a magyar
        // ékezeteket (ő/ű is) tartalmazza.
        string requiredSet = BuildRequiredCharacterSet();
        bool allAdded = fontAsset.TryAddCharacters(requiredSet, out string missingAfterAdd);
        if (!allAdded)
        {
            Debug.LogWarning(
                "[TmpHungarianFontSetup] Nem minden előre megadott karaktert sikerült " +
                $"beégetni a font assetbe (hiányzók: '{missingAfterAdd}').");
        }

        // KRITIKUS, megfigyelt viselkedés (lásd Napló): Dynamic populációs
        // mód mellett a TMP a glyph-/karaktertáblát és az atlas-textúrát
        // NEM perzisztálja a lemezre írt .asset-ben — a memóriában
        // TryAddCharacters után helyesen feltöltött táblák (113/113,
        // 1024×1024 textúra) AssetDatabase.SaveAssets()+Refresh() UTÁN is
        // helyesnek látszottak a C#-objektumon, de a lemezre írt YAML
        // mégis üres táblákat és 1×1 textúrát tartalmazott (feltehetően a
        // "Dynamic = ne szállíts előre sütött adatot" tervezési elv miatt,
        // lásd m_ClearDynamicDataOnBuild). A javítás: Static-ra váltjuk a
        // módot, MIUTÁN a TryAddCharacters hívás már megtörtént — Static
        // módban a TMP a meglévő táblákat véglegesnek/perzisztálandónak
        // tekinti, és a lemezre írt eredmény ellenőrizve (lásd Napló: a
        // kész .asset YAML-ja 113/113 bejegyzést és 1024×1024 textúrát
        // tartalmaz). Következmény: futásidőben a HasCharacters(...,
        // tryAddCharacter: true) hívás a MÁR BEÉGETETT karakterekre
        // működik, de egy valóban ÚJ (itt nem szereplő) karaktert NEM tud
        // dinamikusan pótolni — ha egy jövőbeli hu.json-kulcs ilyet
        // igényel, ezt a font assetet újra kell generálni (törölni +
        // EnsureAllMenuEntry), amit a PlayMode teszt ennek megfelelően
        // hiányzó glyph-ként jelez, nem csendes sikerrel.
        fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;

        EnsureFolder("Assets/_Project/Resources/Fonts");
        AssetDatabase.CreateAsset(fontAsset, FontAssetPath);
        AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
        AssetDatabase.AddObjectToAsset(fontAsset.atlasTextures[0], fontAsset);
        EditorUtility.SetDirty(fontAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"[TmpHungarianFontSetup] Font asset létrehozva: {FontAssetPath} " +
            $"(characterCount={fontAsset.characterTable.Count}, atlas={fontAsset.atlasTextures[0].width}x{fontAsset.atlasTextures[0].height})");

        return fontAsset;
    }

    public static void EnsureDefaultFontAssignment(TMP_FontAsset fontAsset)
    {
        var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettingsAssetPath);
        if (settings == null)
        {
            throw new InvalidOperationException(
                $"[TmpHungarianFontSetup] Nincs TMP Settings asset itt: {TmpSettingsAssetPath}. " +
                "Ez a fájl a TMP Essential Resources csomag kanonikus TMP Settings.asset-je, " +
                "verziózva a repóban (Assets/TextMesh Pro/Resources/) — ha hiányzik, a " +
                "csomag nincs teljesen betöltve (lásd e fájl fejkommentje).");
        }

        var so = new SerializedObject(settings);
        SerializedProperty defaultFontProp = so.FindProperty("m_defaultFontAsset");
        if (defaultFontProp == null)
        {
            throw new InvalidOperationException(
                "[TmpHungarianFontSetup] A TMP_Settings 'm_defaultFontAsset' mezője nem " +
                "található — TMP API-változás? (lásd TMP_Settings.cs forrás).");
        }

        if (defaultFontProp.objectReferenceValue == (UnityEngine.Object)fontAsset)
        {
            return;
        }

        defaultFontProp.objectReferenceValue = fontAsset;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(settings);

        Debug.Log($"[TmpHungarianFontSetup] TMP_Settings.m_defaultFontAsset beállítva: {FontAssetPath}");
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
        string folderName = System.IO.Path.GetFileName(path);

        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }

        AssetDatabase.CreateFolder(string.IsNullOrEmpty(parent) ? "Assets" : parent, folderName);
    }
}
