using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using MathHorde.Presentation.Hud;
using MathHorde.Presentation.UI;

/// <summary>Feedback occupies the bottom of each lane; the middle combat view stays open.</summary>
public static class MathFeedbackHudBuilder
{
    public const string PrefabPath = "Assets/_Project/Prefabs/UI/MathFeedbackHUD.prefab";

    [MenuItem("Math Horde/UI/Build Math Feedback HUD")]
    public static void Build()
    {
        TmpHungarianFontSetup.EnsureAll();
        var root = new GameObject("MathFeedbackHUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920); scaler.matchWidthOrHeight = .5f;
        var safe = Child(root.transform, "SafeAreaRoot", 0, 0, 1, 1);
        safe.gameObject.AddComponent<SafeAreaFitter>();
        var band = Child(safe, "FeedbackBand", .02f, .01f, .98f, .23f);
        var row = Child(band, "FeedbackTemplate", 0, 0, 1, 1);
        Panel(Child(row, "SummaryBackground", .01f, .73f, .99f, 1));
        var summary = Text(row, "Summary", .01f, .73f, .99f, 1, 32, 26);
        var view = row.gameObject.AddComponent<MathFeedbackView>();
        var so = new SerializedObject(view);
        so.FindProperty("_summary").objectReferenceValue = summary;
        var results = so.FindProperty("_results"); results.arraySize = 4;
        var markers = so.FindProperty("_markers"); markers.arraySize = 4;
        var lanes = so.FindProperty("_lanes"); lanes.arraySize = 4;
        for (int i = 0; i < 4; i++)
        {
            var lane = Child(row, "Lane" + i, i / 4f, 0, (i + 1) / 4f, .72f);
            Panel(lane);
            var result = Text(lane, "Result", .02f, .60f, .98f, 1, 34, 26);
            var marker = Text(lane, "Marker", .02f, 0, .98f, .60f, 28, 24);
            lanes.GetArrayElementAtIndex(i).objectReferenceValue = lane;
            results.GetArrayElementAtIndex(i).objectReferenceValue = result;
            markers.GetArrayElementAtIndex(i).objectReferenceValue = marker;
        }
        so.ApplyModifiedPropertiesWithoutUndo(); view.Clear();
        var presenter = root.AddComponent<MathFeedbackPresenter>();
        var ps = new SerializedObject(presenter);
        ps.FindProperty("_template").objectReferenceValue = view; ps.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root); AssetDatabase.SaveAssets();
        Debug.Log("[MathFeedbackHudBuilder] Saved " + PrefabPath);
    }

    private static RectTransform Child(Transform parent, string name, float x0, float y0, float x1, float y1)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(x0, y0); rect.anchorMax = new Vector2(x1, y1);
        rect.offsetMin = rect.offsetMax = Vector2.zero; return rect;
    }

    private static TMP_Text Text(Transform parent, string name, float x0, float y0, float x1, float y1, float size, float min)
    {
        var text = Child(parent, name, x0, y0, x1, y1).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset; text.text = string.Empty;
        text.fontSize = text.fontSizeMax = size; text.fontSizeMin = min; text.enableAutoSizing = true;
        text.textWrappingMode = TextWrappingModes.Normal; text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white; text.raycastTarget = false; return text;
    }

    private static void Panel(RectTransform rect)
    {
        var image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(.025f, .04f, .07f, .94f); image.raycastTarget = false;
    }
}
