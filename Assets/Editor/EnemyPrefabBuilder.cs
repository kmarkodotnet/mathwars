// EnemyPrefabBuilder.cs
// F1-14 — reprodukálható Editor-eszköz 5 vizuálisan jól elkülöníthető
// ellenség-archetípushoz (math-horde.md 3.1: "vizuálisan jól elkülöníthető
// típusok", 20. szakasz: ne kézi Inspector-összeállítással; 23. szakasz:
// semmi vér/brutalitás/realisztikus katonai látvány).
//
// Archetípusok (Presentation-oldali "vizuális kulcs", EnemyVisualCatalogSO-n
// keresztül kötve a GameLogic EnemyId katalóguskulcsához):
//   basic    - kerekded, barátságos "robotocska" (Capsule törzs + kocka fej)
//   fast     - lapos, előredőlő, "uszonnyal" jelzett sebesség-forma
//   armored  - szögletes, páncéllemezekkel vastagított test
//   boss     - nagy méretű, gömb-mag + "szarv" formák, de nem véres/katonai
//   swarmlet - apró, egyszerű gömb — olcsó, ha sokat kell belőle megjeleníteni
//
// Minden primitív geometria a "Visual" gyermek alatt él, a logikai gyökér
// (EnemyView) alatt — ArenaBuilder.cs mintáját követve — így a geometria
// később grafikai assetekre cserélhető a presenter módosítása nélkül.
//
// Használat:
//   Editorból: Math Horde/Enemies/Build All Enemy Prefabs (Default Catalog)
//   Batch-módból:
//     Unity -batchmode -nographics -quit -projectPath game
//           -executeMethod EnemyPrefabBuilder.BuildAllAndCatalog -logFile <path>
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using MathHorde.Presentation.Enemies;

public static class EnemyPrefabBuilder
{
    private const string PrefabFolder = "Assets/_Project/Prefabs/Enemies";
    private const string MaterialFolder = "Assets/_Project/Materials/Enemies";
    private const string CatalogFolder = "Assets/_Project/Configuration";
    private const string CatalogPath = CatalogFolder + "/EnemyVisualCatalog.asset";

    // Gyermekbarát, élénk paletta (math-horde.md 3.1/23.) — szándékosan NINCS
    // bennük vörös/vér-asszociációs tónus; a "boss" egy meleg bordó-lila, nem
    // skarlátvörös.
    private static readonly Color BasicColor = new Color(0.30f, 0.62f, 0.95f);
    private static readonly Color BasicAccent = new Color(0.95f, 0.95f, 0.98f);
    private static readonly Color FastColor = new Color(0.99f, 0.74f, 0.18f);
    private static readonly Color ArmoredColor = new Color(0.58f, 0.60f, 0.64f);
    private static readonly Color ArmoredPlateColor = new Color(0.38f, 0.40f, 0.45f);
    private static readonly Color BossColor = new Color(0.55f, 0.22f, 0.45f);
    private static readonly Color BossAccent = new Color(0.90f, 0.70f, 0.25f);
    private static readonly Color SwarmletColor = new Color(0.35f, 0.88f, 0.45f);

    private struct ArchetypeResult
    {
        public string VisualKey;
        public string DefaultEnemyId;
        public EnemyView Prefab;
    }

    [MenuItem("Math Horde/Enemies/Build All Enemy Prefabs (Default Catalog)")]
    public static void BuildAllAndCatalog()
    {
        EnsureFolder(PrefabFolder);
        EnsureFolder(MaterialFolder);
        EnsureFolder(CatalogFolder);

        var results = new[]
        {
            BuildBasic(),
            BuildFast(),
            BuildArmored(),
            BuildBoss(),
            BuildSwarmlet(),
        };

        EnemyVisualCatalogSO catalog = GetOrCreateCatalog();
        var entries = new EnemyVisualCatalogSO.Entry[results.Length];
        for (int i = 0; i < results.Length; i++)
        {
            entries[i] = new EnemyVisualCatalogSO.Entry
            {
                EnemyId = results[i].DefaultEnemyId,
                VisualKey = results[i].VisualKey,
                Prefab = results[i].Prefab,
                PrewarmCount = results[i].VisualKey == "swarmlet" ? 12 : 6,
            };
        }

        var so = new SerializedObject(catalog);
        SerializedProperty entriesProp = so.FindProperty("_entries");
        entriesProp.arraySize = entries.Length;
        for (int i = 0; i < entries.Length; i++)
        {
            SerializedProperty element = entriesProp.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("EnemyId").stringValue = entries[i].EnemyId;
            element.FindPropertyRelative("VisualKey").stringValue = entries[i].VisualKey;
            element.FindPropertyRelative("Prefab").objectReferenceValue = entries[i].Prefab;
            element.FindPropertyRelative("PrewarmCount").intValue = entries[i].PrewarmCount;
        }

        SerializedProperty fallbackProp = so.FindProperty("_fallback");
        fallbackProp.FindPropertyRelative("EnemyId").stringValue = "";
        fallbackProp.FindPropertyRelative("VisualKey").stringValue = "basic";
        fallbackProp.FindPropertyRelative("Prefab").objectReferenceValue = results[0].Prefab;
        fallbackProp.FindPropertyRelative("PrewarmCount").intValue = 6;

        so.ApplyModifiedPropertiesWithoutUndo();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[EnemyPrefabBuilder] BuildAllAndCatalog finished. catalog={CatalogPath}, prefabCount={results.Length}");
    }

    public static EnemyVisualCatalogSO GetOrCreateCatalog()
    {
        EnsureFolder(CatalogFolder);
        var catalog = AssetDatabase.LoadAssetAtPath<EnemyVisualCatalogSO>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<EnemyVisualCatalogSO>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }

        return catalog;
    }

    // ---- basic ----
    private static ArchetypeResult BuildBasic()
    {
        GameObject root = CreateRoot("Enemy_Basic");
        GameObject visual = CreateVisualRoot(root.transform);

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        body.transform.SetParent(visual.transform, false);
        body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        body.transform.localScale = new Vector3(0.75f, 0.9f, 0.75f);
        ApplyMaterial(body, "Mat_Enemy_Basic_Body", BasicColor);
        RemoveCollider(body);

        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Cube);
        head.name = "Head";
        head.transform.SetParent(visual.transform, false);
        head.transform.localPosition = new Vector3(0f, 1.75f, 0f);
        head.transform.localScale = new Vector3(0.55f, 0.45f, 0.55f);
        ApplyMaterial(head, "Mat_Enemy_Basic_Head", BasicColor);
        RemoveCollider(head);

        GameObject eyeL = BuildEye("EyeL", visual.transform, new Vector3(-0.14f, 1.8f, 0.26f));
        GameObject eyeR = BuildEye("EyeR", visual.transform, new Vector3(0.14f, 1.8f, 0.26f));

        var view = root.AddComponent<EnemyView>();
        AssignFlashRenderers(view, body, head, eyeL, eyeR);

        return new ArchetypeResult
        {
            VisualKey = "basic",
            DefaultEnemyId = "basic_bot",
            Prefab = SaveEnemyPrefab(root, "Enemy_Basic"),
        };
    }

    private static GameObject BuildEye(string name, Transform parent, Vector3 localPos)
    {
        GameObject eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        eye.name = name;
        eye.transform.SetParent(parent, false);
        eye.transform.localPosition = localPos;
        eye.transform.localScale = Vector3.one * 0.12f;
        ApplyMaterial(eye, "Mat_Enemy_Basic_Eye", BasicAccent, unlit: true);
        RemoveCollider(eye);
        return eye;
    }

    // ---- fast ----
    private static ArchetypeResult BuildFast()
    {
        GameObject root = CreateRoot("Enemy_Fast");
        GameObject visual = CreateVisualRoot(root.transform);
        visual.transform.localRotation = Quaternion.Euler(18f, 0f, 0f);

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        body.name = "Body";
        body.transform.SetParent(visual.transform, false);
        body.transform.localPosition = new Vector3(0f, 0.6f, 0f);
        body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        body.transform.localScale = new Vector3(0.5f, 0.95f, 0.5f);
        ApplyMaterial(body, "Mat_Enemy_Fast_Body", FastColor);
        RemoveCollider(body);

        GameObject fin = GameObject.CreatePrimitive(PrimitiveType.Cube);
        fin.name = "Fin";
        fin.transform.SetParent(visual.transform, false);
        fin.transform.localPosition = new Vector3(0f, 0.75f, -0.55f);
        fin.transform.localScale = new Vector3(0.08f, 0.5f, 0.45f);
        ApplyMaterial(fin, "Mat_Enemy_Fast_Fin", FastColor, unlit: true);
        RemoveCollider(fin);

        var view = root.AddComponent<EnemyView>();
        AssignFlashRenderers(view, body, fin);

        return new ArchetypeResult
        {
            VisualKey = "fast",
            DefaultEnemyId = "fast_bot",
            Prefab = SaveEnemyPrefab(root, "Enemy_Fast"),
        };
    }

    // ---- armored ----
    private static ArchetypeResult BuildArmored()
    {
        GameObject root = CreateRoot("Enemy_Armored");
        GameObject visual = CreateVisualRoot(root.transform);

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Body";
        body.transform.SetParent(visual.transform, false);
        body.transform.localPosition = new Vector3(0f, 0.95f, 0f);
        body.transform.localScale = new Vector3(1.05f, 1.3f, 0.95f);
        ApplyMaterial(body, "Mat_Enemy_Armored_Body", ArmoredColor);
        RemoveCollider(body);

        GameObject plateFront = GameObject.CreatePrimitive(PrimitiveType.Cube);
        plateFront.name = "PlateFront";
        plateFront.transform.SetParent(visual.transform, false);
        plateFront.transform.localPosition = new Vector3(0f, 1.0f, 0.42f);
        plateFront.transform.localScale = new Vector3(1.15f, 0.9f, 0.18f);
        ApplyMaterial(plateFront, "Mat_Enemy_Armored_Plate", ArmoredPlateColor);
        RemoveCollider(plateFront);

        var view = root.AddComponent<EnemyView>();
        AssignFlashRenderers(view, body, plateFront);

        return new ArchetypeResult
        {
            VisualKey = "armored",
            DefaultEnemyId = "armored_bot",
            Prefab = SaveEnemyPrefab(root, "Enemy_Armored"),
        };
    }

    // ---- boss ----
    private static ArchetypeResult BuildBoss()
    {
        GameObject root = CreateRoot("Enemy_Boss");
        GameObject visual = CreateVisualRoot(root.transform);
        visual.transform.localScale = Vector3.one * 2.0f;

        GameObject core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        core.name = "Core";
        core.transform.SetParent(visual.transform, false);
        core.transform.localPosition = new Vector3(0f, 1.0f, 0f);
        core.transform.localScale = new Vector3(0.9f, 0.9f, 0.9f);
        ApplyMaterial(core, "Mat_Enemy_Boss_Core", BossColor);
        RemoveCollider(core);

        GameObject shoulders = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shoulders.name = "Shoulders";
        shoulders.transform.SetParent(visual.transform, false);
        shoulders.transform.localPosition = new Vector3(0f, 1.55f, 0f);
        shoulders.transform.localScale = new Vector3(1.3f, 0.35f, 0.75f);
        ApplyMaterial(shoulders, "Mat_Enemy_Boss_Shoulders", BossColor);
        RemoveCollider(shoulders);

        GameObject hornL = BuildHorn("HornL", visual.transform, new Vector3(-0.3f, 1.85f, 0f));
        GameObject hornR = BuildHorn("HornR", visual.transform, new Vector3(0.3f, 1.85f, 0f));

        var view = root.AddComponent<EnemyView>();
        AssignFlashRenderers(view, core, shoulders, hornL, hornR);

        return new ArchetypeResult
        {
            VisualKey = "boss",
            DefaultEnemyId = "boss_bot",
            Prefab = SaveEnemyPrefab(root, "Enemy_Boss"),
        };
    }

    private static GameObject BuildHorn(string name, Transform parent, Vector3 localPos)
    {
        GameObject horn = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        horn.name = name;
        horn.transform.SetParent(parent, false);
        horn.transform.localPosition = localPos;
        horn.transform.localScale = new Vector3(0.12f, 0.3f, 0.12f);
        ApplyMaterial(horn, "Mat_Enemy_Boss_Horn", BossAccent, unlit: true);
        RemoveCollider(horn);
        return horn;
    }

    // ---- swarmlet ----
    private static ArchetypeResult BuildSwarmlet()
    {
        GameObject root = CreateRoot("Enemy_Swarmlet");
        GameObject visual = CreateVisualRoot(root.transform);

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        body.name = "Body";
        body.transform.SetParent(visual.transform, false);
        body.transform.localPosition = new Vector3(0f, 0.35f, 0f);
        body.transform.localScale = Vector3.one * 0.4f;
        ApplyMaterial(body, "Mat_Enemy_Swarmlet_Body", SwarmletColor, unlit: true);
        RemoveCollider(body);

        var view = root.AddComponent<EnemyView>();
        AssignFlashRenderers(view, body);

        return new ArchetypeResult
        {
            VisualKey = "swarmlet",
            DefaultEnemyId = "swarmlet_bot",
            Prefab = SaveEnemyPrefab(root, "Enemy_Swarmlet"),
        };
    }

    // ---- shared helpers ----
    private static GameObject CreateRoot(string name)
    {
        return new GameObject(name);
    }

    private static GameObject CreateVisualRoot(Transform parent)
    {
        var visual = new GameObject("Visual");
        visual.transform.SetParent(parent, false);
        return visual;
    }

    private static void RemoveCollider(GameObject go)
    {
        Collider collider = go.GetComponent<Collider>();
        if (collider != null)
        {
            Object.DestroyImmediate(collider);
        }
    }

    private static void ApplyMaterial(GameObject go, string materialName, Color color, bool unlit = false)
    {
        Shader shader = unlit ? ResolveUnlitShader() : ResolveLitShader();
        Material mat = CreateOrUpdateMaterial($"{MaterialFolder}/{materialName}.mat", shader, color);
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }

    private static void AssignFlashRenderers(EnemyView view, params GameObject[] renderedObjects)
    {
        var renderers = new Renderer[renderedObjects.Length];
        for (int i = 0; i < renderedObjects.Length; i++)
        {
            renderers[i] = renderedObjects[i].GetComponent<Renderer>();
        }

        var so = new SerializedObject(view);
        SerializedProperty prop = so.FindProperty("_flashRenderers");
        prop.arraySize = renderers.Length;
        for (int i = 0; i < renderers.Length; i++)
        {
            prop.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
        }

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static EnemyView SaveEnemyPrefab(GameObject root, string fileName)
    {
        string path = $"{PrefabFolder}/{fileName}.prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab.GetComponent<EnemyView>();
    }

    private static Shader ResolveLitShader()
    {
        return Shader.Find("Universal Render Pipeline/Lit")
            ?? Shader.Find("Universal Render Pipeline/Simple Lit")
            ?? Shader.Find("Standard");
    }

    private static Shader ResolveUnlitShader()
    {
        return Shader.Find("Universal Render Pipeline/Unlit")
            ?? Shader.Find("Unlit/Color");
    }

    private static Material CreateOrUpdateMaterial(string path, Shader shader, Color color)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        else if (mat.shader != shader)
        {
            mat.shader = shader;
        }

        if (mat.HasProperty("_BaseColor"))
        {
            mat.SetColor("_BaseColor", color);
        }
        else if (mat.HasProperty("_Color"))
        {
            mat.SetColor("_Color", color);
        }

        EditorUtility.SetDirty(mat);
        return mat;
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

    // ---- preview (same batch-render color caveat as ArenaBuilder.RenderPreviewPng — see
    // .claude/skills/unity-ui/SKILL.md "Vizuális ellenőrzés batch-módban": geometry/layout
    // only, NOT a color/material judgment) ----
    [MenuItem("Math Horde/Enemies/Render Preview PNG (Batch-safe, geometry only)")]
    public static void RenderPreviewPng()
    {
        EnemyVisualCatalogSO catalog = GetOrCreateCatalog();
        if (catalog.Entries.Count == 0)
        {
            BuildAllAndCatalog();
            catalog = GetOrCreateCatalog();
        }

        var camGo = new GameObject("PreviewCamera");
        Camera cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.1f, 0.1f, 0.12f);
        cam.fieldOfView = 45f;
        camGo.transform.position = new Vector3(0f, 3.2f, -8f);
        camGo.transform.LookAt(new Vector3(0f, 1f, 0f));

        var instances = new System.Collections.Generic.List<GameObject>();
        float spacing = 2.4f;
        float startX = -(catalog.Entries.Count - 1) * spacing / 2f;
        for (int i = 0; i < catalog.Entries.Count; i++)
        {
            EnemyVisualCatalogSO.Entry entry = catalog.Entries[i];
            if (entry.Prefab == null)
            {
                continue;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(entry.Prefab.gameObject);
            instance.transform.position = new Vector3(startX + i * spacing, 0f, 0f);
            instances.Add(instance);
        }

        try
        {
            const int width = 1280;
            const int height = 720;
            var rt = new RenderTexture(width, height, 24);
            cam.targetTexture = rt;
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture.active = rt;

            cam.Render();

            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();

            RenderTexture.active = previousActive;
            cam.targetTexture = null;
            rt.Release();

            byte[] png = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);

            string fullOutputDir = Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, "Builds");
            Directory.CreateDirectory(fullOutputDir);
            string fullOutputPath = Path.Combine(fullOutputDir, "EnemyPreview.png");
            File.WriteAllBytes(fullOutputPath, png);

            Debug.Log($"[EnemyPrefabBuilder] RenderPreviewPng finished. path={fullOutputPath}, bytes={png.Length}");
        }
        finally
        {
            foreach (GameObject instance in instances)
            {
                Object.DestroyImmediate(instance);
            }

            Object.DestroyImmediate(camGo);
        }
    }

    public static void BuildAllCatalogAndRenderPreview()
    {
        BuildAllAndCatalog();
        RenderPreviewPng();
    }
}
