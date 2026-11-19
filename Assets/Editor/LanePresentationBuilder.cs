using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using MathHorde.Presentation.Arena;

/// <summary>Four prebuilt, non-interactive floor marker views; no runtime instantiation or material copies.</summary>
public static class LanePresentationBuilder
{
    public const string PrefabPath = "Assets/_Project/Prefabs/Arena/LanePresentation.prefab";
    private const string MaterialFolder = "Assets/_Project/Materials/Arena";

    [MenuItem("Math Horde/UI/Build Lane Presentation")]
    public static void Build()
    {
        if (!AssetDatabase.IsValidFolder("Assets/_Project/Prefabs/Arena")) AssetDatabase.CreateFolder("Assets/_Project/Prefabs", "Arena");
        var selected = Material("Mat_SelectedLane", new Color(.25f, .95f, 1f));
        var decision = Material("Mat_DecisionZone", new Color(1f, .75f, .15f));
        var root = Child(null, "LanePresentation");
        var presenter = root.AddComponent<LanePresenter>();
        var so = new SerializedObject(presenter);
        var views = so.FindProperty("_views");
        views.arraySize = 4;
        for (int i = 0; i < 4; i++)
        {
            var lane = Child(root.transform, "LaneHighlight_" + i);
            var view = lane.AddComponent<LaneHighlightView>();
            var rails = Child(lane.transform, "SelectedRailsAndChevron");
            var left = Strip(rails.transform, "LeftRail", Vector3.zero, Vector3.one, selected);
            var right = Strip(rails.transform, "RightRail", Vector3.zero, Vector3.one, selected);
            var pointer = Child(rails.transform, "Chevron");
            var a = Strip(pointer.transform, "LeftArm", new Vector3(-.22f, 0, 0), new Vector3(.08f, .012f, .65f), selected);
            var b = Strip(pointer.transform, "RightArm", new Vector3(.22f, 0, 0), new Vector3(.08f, .012f, .65f), selected);
            a.localRotation = Quaternion.Euler(0, -45, 0); b.localRotation = Quaternion.Euler(0, 45, 0);
            var zone = Child(lane.transform, "DecisionWindowOutline");
            Strip(zone.transform, "NearEdge", new Vector3(0, 0, -.4f), new Vector3(1, .012f, .08f), decision);
            Strip(zone.transform, "FarEdge", new Vector3(0, 0, .4f), new Vector3(1, .012f, .08f), decision);
            Strip(zone.transform, "LeftEdge", new Vector3(-.5f, 0, 0), new Vector3(.04f, .012f, .8f), decision);
            Strip(zone.transform, "RightEdge", new Vector3(.5f, 0, 0), new Vector3(.04f, .012f, .8f), decision);
            var v = new SerializedObject(view);
            v.FindProperty("_selected").objectReferenceValue = rails;
            v.FindProperty("_decision").objectReferenceValue = zone;
            v.FindProperty("_leftRail").objectReferenceValue = left;
            v.FindProperty("_rightRail").objectReferenceValue = right;
            v.FindProperty("_pointer").objectReferenceValue = pointer.transform;
            v.ApplyModifiedPropertiesWithoutUndo();
            rails.SetActive(false); zone.SetActive(false);
            views.GetArrayElementAtIndex(i).objectReferenceValue = view;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        Debug.Log("[LanePresentationBuilder] Saved " + PrefabPath);
    }

    private static GameObject Child(Transform parent, string name)
    {
        var child = new GameObject(name);
        child.layer = LayerMask.NameToLayer("Ignore Raycast");
        child.transform.SetParent(parent, false);
        return child;
    }

    private static Transform Strip(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name; go.layer = LayerMask.NameToLayer("Ignore Raycast");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position; go.transform.localScale = scale;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        var renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return go.transform;
    }

    private static Material Material(string name, Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) throw new System.InvalidOperationException("URP Unlit shader is required.");
        var path = MaterialFolder + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
        material.shader = shader;
        material.SetColor("_BaseColor", color);
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return material;
    }
}
