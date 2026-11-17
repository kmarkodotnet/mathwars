// LevelResultScreenBuilder.cs
// F1-16 — reprodukálható Editor-eszköz a minimál LevelResultScreen
// prefabhoz (math-horde.md 20. szakasz: ne kézi Inspector-összeállítással),
// a GameplayHudBuilder (F1-15) mintáját követve.
//
// Elrendezés — ELLENTÉTBEN a GameplayHUD-dal, EZ a képernyő szándékosan
// eltakarhatja a harci területet: a GameOver/LevelComplete állapot a harci
// szimuláció TELJES végét jelenti (ARCHITECTURE.md 4., AppStateMachine),
// tehát a 3.2. "a HUD nem takarhatja el a harcot" szabály itt nem
// alkalmazandó — ez egy modális záróképernyő, nem a mindig-látható HUD.
//
//   LevelResultScreen (Canvas, ScreenSpaceOverlay, ScaleWithScreenSize)
//     SafeAreaRoot (SafeAreaFitter)
//       ResultPanel (kezdetben INAKTÍV — a LevelResultScreen.Initialize()
//                    dönt a láthatóságról az IAppStateMachine.Current alapján)
//         Backdrop    [0.00-1.00 teljes]  sötét áttekintő háttér
//         Card        [0.08-0.92 x, 0.30-0.70 y]  középre rendezett panel
//           HeadlineText        [felül]
//           EncouragementText   [középen]
//           ButtonRow           [alul]
//             PrimaryButton   (bal fél)
//             SecondaryButton (jobb fél)
//
// Minden szöveg LevelResultScreen.Initialize()-ban (LevelResultModel-en át)
// az ILocalizationService-ből töltődik be — itt, az Editor-eszközben,
// egyetlen magyar literál sincs (ADR-007); a textmező-objektumok üresen
// készülnek.
//
// Használat:
//   Editorból: Math Horde/UI/Build Level Result Screen Prefab
//   Batch-módból:
//     Unity -batchmode -nographics -quit -projectPath game
//           -executeMethod LevelResultScreenBuilder.BuildLevelResultScreen -logFile <path>
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MathHorde.Presentation.Result;
using MathHorde.Presentation.UI;

public static class LevelResultScreenBuilder
{
    private const string PrefabFolder = "Assets/_Project/Prefabs/UI";
    private const string PrefabPath = PrefabFolder + "/LevelResultScreen.prefab";

    private static readonly Color BackdropColor = new Color(0.02f, 0.03f, 0.05f, 0.72f);
    private static readonly Color CardBackground = new Color(0.08f, 0.11f, 0.16f, 0.95f);
    private static readonly Color TextColor = Color.white;
    private static readonly Color PrimaryButtonColor = new Color(0.16f, 0.45f, 0.24f, 0.95f);
    private static readonly Color SecondaryButtonColor = new Color(0.20f, 0.24f, 0.30f, 0.95f);

    [MenuItem("Math Horde/UI/Build Level Result Screen Prefab")]
    public static GameObject BuildLevelResultScreen()
    {
        EnsureFolder(PrefabFolder);

        var root = new GameObject("LevelResultScreen", typeof(RectTransform));
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f); // portrait, math-horde.md 3.2
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        root.AddComponent<GraphicRaycaster>();

        GameObject safeAreaRoot = CreateAnchoredChild(root.transform, "SafeAreaRoot", 0f, 0f, 1f, 1f);
        safeAreaRoot.AddComponent<SafeAreaFitter>();

        GameObject resultPanel = CreateAnchoredChild(safeAreaRoot.transform, "ResultPanel", 0f, 0f, 1f, 1f);

        GameObject backdrop = CreateAnchoredChild(resultPanel.transform, "Backdrop", 0f, 0f, 1f, 1f);
        var backdropImage = backdrop.AddComponent<Image>();
        backdropImage.color = BackdropColor;

        GameObject card = CreateAnchoredChild(resultPanel.transform, "Card", 0.08f, 0.30f, 0.92f, 0.70f);
        var cardImage = card.AddComponent<Image>();
        cardImage.color = CardBackground;

        TMP_Text headlineText = BuildText(card.transform, "HeadlineText", 0.06f, 0.70f, 0.94f, 0.92f, TextAlignmentOptions.Center, 52f);
        TMP_Text encouragementText = BuildText(card.transform, "EncouragementText", 0.06f, 0.40f, 0.94f, 0.68f, TextAlignmentOptions.Center, 34f);

        GameObject buttonRow = CreateAnchoredChild(card.transform, "ButtonRow", 0.06f, 0.08f, 0.94f, 0.32f);
        BuildButton(buttonRow.transform, "PrimaryButton", 0f, 0.52f, PrimaryButtonColor, out Button primaryButton, out TMP_Text primaryLabel);
        BuildButton(buttonRow.transform, "SecondaryButton", 0.48f, 1f, SecondaryButtonColor, out Button secondaryButton, out TMP_Text secondaryLabel);

        // Rejtve indul — a LevelResultScreen.Initialize() az
        // IAppStateMachine.Current (és a Transitioned esemény) alapján
        // dönt a láthatóságról, a prefab önmagában soha nem jelenik meg
        // Gameplay/MainMenu/stb. állapotban.
        resultPanel.SetActive(false);

        var screen = root.AddComponent<LevelResultScreen>();
        var so = new SerializedObject(screen);
        so.FindProperty("_panelRoot").objectReferenceValue = resultPanel;
        so.FindProperty("_headlineText").objectReferenceValue = headlineText;
        so.FindProperty("_encouragementText").objectReferenceValue = encouragementText;
        so.FindProperty("_primaryButton").objectReferenceValue = primaryButton;
        so.FindProperty("_primaryButtonText").objectReferenceValue = primaryLabel;
        so.FindProperty("_secondaryButton").objectReferenceValue = secondaryButton;
        so.FindProperty("_secondaryButtonText").objectReferenceValue = secondaryLabel;
        so.ApplyModifiedPropertiesWithoutUndo();

        EnsureFolder(PrefabFolder);
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[LevelResultScreenBuilder] BuildLevelResultScreen finished. prefab={PrefabPath}");
        return prefab;
    }

    private static void BuildButton(Transform parent, string name, float anchorMinX, float anchorMaxX, Color color, out Button button, out TMP_Text label)
    {
        // Érintésbarát célterület (unity-ui skill: min. ~48dp) — a gomb a
        // ButtonRow felének szélességét és teljes magasságát foglalja el.
        GameObject go = CreateAnchoredChild(parent, name, anchorMinX, 0f, anchorMaxX, 1f);
        var image = go.AddComponent<Image>();
        image.color = color;
        button = go.AddComponent<Button>();
        button.targetGraphic = image;

        label = BuildText(go.transform, "Label", 0.04f, 0.1f, 0.96f, 0.9f, TextAlignmentOptions.Center, 32f);
    }

    private static TMP_Text BuildText(Transform parent, string name, float anchorMinX, float anchorMinY, float anchorMaxX, float anchorMaxY, TextAlignmentOptions alignment, float fontSize)
    {
        GameObject go = CreateAnchoredChild(parent, name, anchorMinX, anchorMinY, anchorMaxX, anchorMaxY);
        TMP_Text text = go.AddComponent<TextMeshProUGUI>();
        text.alignment = alignment;
        text.fontSize = fontSize;
        text.color = TextColor;
        text.enableAutoSizing = false;
        text.text = string.Empty; // LevelResultScreen.Initialize() fills every string via LevelResultModel/ILocalizationService (ADR-007).
        return text;
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
