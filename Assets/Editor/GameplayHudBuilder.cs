// GameplayHudBuilder.cs
// F1-15 — reprodukálható Editor-eszköz az alap GameplayHUD prefabhoz
// (math-horde.md 20. szakasz: ne kézi Inspector-összeállítással).
//
// Elrendezés (math-horde.md 3.2: a HUD soha nem takarhatja el a fő harci
// eseményeket) — MINDEN HUD-tartalom a képernyő felső ~34%-ára szorítva
// (anchor y >= 0.66), a sávok/érkezési zóna/játékos-zóna alatta szabadon
// látható marad:
//   SafeAreaRoot (SafeAreaFitter — jelölőnyúlvány/kijelző-kivágás kezelés)
//     TopBar            [0.90 - 1.00]  Hullám/pálya szöveg (bal) + Szünet gomb (jobb)
//     StatsPanel        [0.74 - 0.90]  Élet / Lőszer / Pajzs sorok (bal)
//     ChallengeLevelArea[0.74 - 0.90]  ÜRES helykitöltő — F4-ig (jobb)
//     MathEventArea     [0.66 - 0.74]  ÜRES helykitöltő — F2-ig (teljes szélesség)
//
// A "ChallengeLevelArea" és "MathEventArea" objektumok szándékosan inaktívak
// és tartalom nélküliek — csak helykitöltő RectTransform-ok a jövőbeli
// MathQuestionPresenter/kihívásszint-sáv számára, hogy ne kelljen utólag
// újratervezni az elrendezést (math-horde.md 12.: a kihívásszint szín+szám+
// ikon hármasa, de ez egy KÉSŐBBI feladat).
//
// Minden szöveg GameplayHUD.Initialize()-ban az ILocalizationService-ből
// töltődik be — itt, az Editor-eszközben, egyetlen magyar literál sincs
// (ADR-007); a textmező-objektumok itt üresen/placeholder nélkül készülnek.
//
// Használat:
//   Editorból: Math Horde/UI/Build Gameplay HUD Prefab
//   Batch-módból:
//     Unity -batchmode -nographics -quit -projectPath game
//           -executeMethod GameplayHudBuilder.BuildHud -logFile <path>
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MathHorde.Presentation.Hud;
using MathHorde.Presentation.UI;

public static class GameplayHudBuilder
{
    private const string PrefabFolder = "Assets/_Project/Prefabs/UI";
    private const string PrefabPath = PrefabFolder + "/GameplayHUD.prefab";

    private static readonly Color PanelBackground = new Color(0.05f, 0.08f, 0.12f, 0.55f);
    private static readonly Color TextColor = Color.white;
    private static readonly Color ButtonColor = new Color(0.16f, 0.22f, 0.30f, 0.85f);
    private static readonly Color IconColor = Color.white;

    [MenuItem("Math Horde/UI/Build Gameplay HUD Prefab")]
    public static GameObject BuildHud()
    {
        EnsureFolder(PrefabFolder);

        var root = new GameObject("GameplayHUD", typeof(RectTransform));
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f); // portrait, math-horde.md 3.2
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        root.AddComponent<GraphicRaycaster>();

        GameObject safeAreaRoot = CreateStretchedChild(root.transform, "SafeAreaRoot", 0f, 0f, 1f, 1f);
        safeAreaRoot.AddComponent<SafeAreaFitter>();

        GameObject topBar = CreateAnchoredChild(safeAreaRoot.transform, "TopBar", 0f, 0.90f, 1f, 1.00f);
        TMP_Text levelText = BuildText(topBar.transform, "LevelText", 0.02f, 0f, 0.32f, 1f, TextAlignmentOptions.MidlineLeft, 38f);
        TMP_Text waveText = BuildText(topBar.transform, "WaveText", 0.34f, 0f, 0.62f, 1f, TextAlignmentOptions.MidlineLeft, 38f);
        BuildPauseButton(topBar.transform, out Button pauseButton, out TMP_Text pauseLabel);

        GameObject statsPanel = CreateAnchoredChild(safeAreaRoot.transform, "StatsPanel", 0f, 0.74f, 0.46f, 0.90f);
        AddPanelBackground(statsPanel);
        BuildStatRow(statsPanel.transform, "HealthRow", 1, out TMP_Text healthLabel, out TMP_Text healthValue);
        GameObject shieldRow = BuildStatRow(statsPanel.transform, "ShieldRow", 0, out TMP_Text shieldLabel, out TMP_Text shieldValue);

        // Reserved, empty layout slots for later phases (left deliberately
        // inactive/content-free — see file header).
        GameObject challengeLevelArea = CreateAnchoredChild(safeAreaRoot.transform, "ChallengeLevelArea_ReservedF4", 0.50f, 0.74f, 0.98f, 0.90f);
        challengeLevelArea.SetActive(false);

        GameObject mathEventArea = CreateAnchoredChild(safeAreaRoot.transform, "MathEventArea_ReservedF2", 0f, 0.66f, 1f, 0.74f);
        mathEventArea.SetActive(false);

        var hud = root.AddComponent<GameplayHUD>();
        var so = new SerializedObject(hud);
        so.FindProperty("_healthLabelText").objectReferenceValue = healthLabel;
        so.FindProperty("_healthValueText").objectReferenceValue = healthValue;
        so.FindProperty("_shieldGroup").objectReferenceValue = shieldRow;
        so.FindProperty("_shieldLabelText").objectReferenceValue = shieldLabel;
        so.FindProperty("_shieldValueText").objectReferenceValue = shieldValue;
        so.FindProperty("_waveText").objectReferenceValue = waveText;
        so.FindProperty("_levelText").objectReferenceValue = levelText;
        so.FindProperty("_pauseButton").objectReferenceValue = pauseButton;
        so.FindProperty("_pauseButtonText").objectReferenceValue = pauseLabel;
        so.ApplyModifiedPropertiesWithoutUndo();

        EnsureFolder(PrefabFolder);
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[GameplayHudBuilder] BuildHud finished. prefab={PrefabPath}");
        return prefab;
    }

    private static GameObject BuildPauseButton(Transform parent, out Button button, out TMP_Text label)
    {
        // Érintésbarát célterület (unity-ui skill: min. ~48dp) — a top bar
        // jobb szélén, négyzetes, nagy tap-területtel.
        GameObject go = CreateAnchoredChild(parent, "PauseButton", 0.78f, 0.08f, 0.98f, 0.92f);
        var image = go.AddComponent<Image>();
        image.color = ButtonColor;
        button = go.AddComponent<Button>();
        button.targetGraphic = image;

        GameObject iconGroup = CreateStretchedChild(go.transform, "Icon", 0.30f, 0.42f, 0.70f, 0.86f);
        BuildPauseBar(iconGroup.transform, -0.28f);
        BuildPauseBar(iconGroup.transform, 0.28f);

        label = BuildText(go.transform, "Label", 0.05f, 0.04f, 0.95f, 0.36f, TextAlignmentOptions.Center, 26f);

        return go;
    }

    private static void BuildPauseBar(Transform parent, float centerX)
    {
        var bar = new GameObject("Bar", typeof(RectTransform));
        bar.transform.SetParent(parent, false);
        var rect = (RectTransform)bar.transform;
        rect.anchorMin = new Vector2(0.5f + centerX - 0.14f, 0f);
        rect.anchorMax = new Vector2(0.5f + centerX + 0.14f, 1f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        var image = bar.AddComponent<Image>();
        image.color = IconColor;
    }

    private static GameObject BuildStatRow(Transform parent, string name, int rowFromBottom, out TMP_Text label, out TMP_Text value)
    {
        float rowHeight = 1f / 3f;
        float yMin = rowFromBottom * rowHeight;
        float yMax = yMin + rowHeight;

        GameObject row = CreateAnchoredChild(parent, name, 0f, yMin, 1f, yMax);
        label = BuildText(row.transform, "Label", 0.06f, 0f, 0.58f, 1f, TextAlignmentOptions.MidlineLeft, 30f);
        value = BuildText(row.transform, "Value", 0.58f, 0f, 0.94f, 1f, TextAlignmentOptions.MidlineRight, 30f);
        return row;
    }

    private static void AddPanelBackground(GameObject panel)
    {
        var image = panel.AddComponent<Image>();
        image.color = PanelBackground;
    }

    private static TMP_Text BuildText(Transform parent, string name, float anchorMinX, float anchorMinY, float anchorMaxX, float anchorMaxY, TextAlignmentOptions alignment, float fontSize)
    {
        GameObject go = CreateAnchoredChild(parent, name, anchorMinX, anchorMinY, anchorMaxX, anchorMaxY);
        TMP_Text text = go.AddComponent<TextMeshProUGUI>();
        text.alignment = alignment;
        text.fontSize = fontSize;
        text.color = TextColor;
        text.enableAutoSizing = false;
        text.text = string.Empty; // GameplayHUD.Initialize() fills every string from ILocalizationService (ADR-007).
        return text;
    }

    private static GameObject CreateStretchedChild(Transform parent, string name, float anchorMinX, float anchorMinY, float anchorMaxX, float anchorMaxY)
    {
        return CreateAnchoredChild(parent, name, anchorMinX, anchorMinY, anchorMaxX, anchorMaxY);
    }

    private static GameObject CreateAnchoredChild(Transform parent, string name, float anchorMinX, float anchorMinY, float anchorMaxX, float anchorMaxY)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(anchorMinX, anchorMinY);
        rect.anchorMax = new Vector2(anchorMaxX, anchorMaxY);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
        return go;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        string folderName = Path.GetFileName(path);

        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }

        AssetDatabase.CreateFolder(string.IsNullOrEmpty(parent) ? "Assets" : parent, folderName);
    }
}
