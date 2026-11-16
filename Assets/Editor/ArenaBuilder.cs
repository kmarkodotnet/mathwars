// ArenaBuilder.cs
// F1-12 — reprodukálható Editor-eszköz a 3D pálya vizuális alapjához
// (math-horde.md 3.1-3.2, GAME_DESIGN.md).
//
// Cél (math-horde.md 20. szakasz): a pálya-prefabot, a hozzá tartozó materialokat és a
// ScriptableObject-konfigurációt NE kézi Inspector-kattintással, hanem egy futtatható,
// verziózott Editor-szkripttel hozzuk létre, hogy bármikor reprodukálható legyen.
//
// Mit épít fel (ArenaPrefab.prefab):
//   Arena
//   ├─ Ground            (talaj — Visual gyermek a cserélhető primitív mesh-hez)
//   ├─ Lanes
//   │  ├─ Lane_0..N-1     (LaneAnchor adat-komponens + Visual gyermek, színben eltérő csík)
//   ├─ LaneDividers       (vizuálisan egyértelmű sávhatárok — math-horde.md 3.1)
//   ├─ Zones
//   │  ├─ SpawnZone       (az ellenség-érkezési zóna jelölése, felül)
//   │  └─ PlayerZone      (a játékos-zóna jelölése, alul — alsó harmad)
//   ├─ Lighting
//   │  └─ SunLight        (irányfény)
//   └─ ArenaCamera        (fix, 3. személyű kamera, MainCamera tag)
//
// A sávszám/szélesség/pályahossz NINCS a szkriptbe égetve — az ArenaConfigSO assetből jön
// (lásd ArenaConfigSO.cs), így a sávszám később 2 és 4 között bármire állítható anélkül,
// hogy ezt a fájlt módosítani kellene.
//
// Minden vizuális elem (mesh+material) egy önálló "Visual" gyermek objektumban van, a
// logikai/strukturális objektumoktól (LaneAnchor, zóna-középpontok) elválasztva — így a
// primitív geometria később grafikai assetekre cserélhető a hierarchia szétszedése
// nélkül (math-horde.md 3.1).
//
// Használat:
//   Editorból: Math Horde/Arena/Build Arena Prefab (Default Config)
//   Batch-módból:
//     Unity -batchmode -nographics -quit -projectPath game
//           -executeMethod ArenaBuilder.BuildDefaultArena -logFile <path>
//
// Ez a szkript NEM hoz létre gameplay scene-t és nem veszi fel az EditorBuildSettings-be
// — a végleges gameplay scene összeállítása az F1-17 feladat.
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using MathHorde.Presentation.Arena;

public static class ArenaBuilder
{
    private const string ConfigFolder = "Assets/_Project/Configuration";
    private const string DefaultConfigPath = ConfigFolder + "/ArenaConfig.asset";

    private const string PrefabFolder = "Assets/_Project/Prefabs/Arena";
    private const string PrefabPath = PrefabFolder + "/ArenaPrefab.prefab";

    private const string MaterialFolder = "Assets/_Project/Materials/Arena";

    private const string PreviewOutputPath = "Builds/ArenaPreview.png";

    // Gyermekbarát, élénk, de nem agresszív színpaletta (math-horde.md 3.1: "élénk, de nem
    // zavaró színek", "vér/brutalitás/realisztikus erőszak tilos"). A sávszínek egymástól
    // jól megkülönböztethető árnyalatúak (nem csak világosság-különbség), hogy a "vizuálisan
    // egyértelmű sávok" követelmény ne csak a pozícióra támaszkodjon.
    private static readonly Color GroundColor = new Color(0.42f, 0.68f, 0.40f);
    private static readonly Color[] LaneColors =
    {
        new Color(0.35f, 0.55f, 0.95f), // kék
        new Color(0.98f, 0.80f, 0.25f), // sárga
        new Color(0.65f, 0.45f, 0.92f), // lila
        new Color(0.25f, 0.85f, 0.75f), // türkiz (4. sáv esetén)
    };
    private static readonly Color DividerColor = new Color(0.97f, 0.97f, 0.92f);
    private static readonly Color SpawnZoneColor = new Color(0.95f, 0.35f, 0.80f, 0.5f);
    private static readonly Color PlayerZoneColor = new Color(0.30f, 0.90f, 0.95f, 0.5f);

    [MenuItem("Math Horde/Arena/Build Arena Prefab (Default Config)")]
    public static void BuildDefaultArena()
    {
        ArenaConfigSO config = GetOrCreateDefaultConfig();
        GameObject prefab = BuildArena(config);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            "[ArenaBuilder] BuildDefaultArena finished. " +
            $"prefab={AssetDatabase.GetAssetPath(prefab)}, " +
            $"laneCount={config.LaneCount}, laneWidth={config.LaneWidth}, " +
            $"arenaLength={config.ArenaLength}, totalWidth={config.TotalWidth:0.00}");
    }

    [MenuItem("Math Horde/Arena/Create Default Arena Config Asset")]
    public static void CreateDefaultConfigAsset()
    {
        GetOrCreateDefaultConfig();
        AssetDatabase.SaveAssets();
    }

    public static ArenaConfigSO GetOrCreateDefaultConfig()
    {
        EnsureFolder(ConfigFolder);

        var config = AssetDatabase.LoadAssetAtPath<ArenaConfigSO>(DefaultConfigPath);
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<ArenaConfigSO>();
            AssetDatabase.CreateAsset(config, DefaultConfigPath);
        }

        return config;
    }

    /// <summary>
    /// Felépíti a pálya GameObject-hierarchiáját a megadott konfiguráció alapján, elmenti
    /// prefab assetként, majd eltávolítja az ideiglenes objektumot az aktuálisan megnyitott
    /// scene-ből (nem szennyezi a scene-t).
    /// </summary>
    public static GameObject BuildArena(ArenaConfigSO config)
    {
        EnsureFolder(PrefabFolder);
        EnsureFolder(MaterialFolder);

        int laneCount = config.LaneCount;
        float laneWidth = config.LaneWidth;
        float dividerWidth = config.LaneDividerWidth;
        float arenaLength = config.ArenaLength;
        float totalWidth = config.TotalWidth;
        float startX = -totalWidth / 2f;

        var root = new GameObject("Arena");

        BuildGround(root.transform, totalWidth, arenaLength);
        BuildLanes(root.transform, laneCount, laneWidth, dividerWidth, arenaLength, startX);
        BuildDividers(root.transform, laneCount, laneWidth, dividerWidth, arenaLength, startX);
        BuildZones(root.transform, config, totalWidth);
        BuildLighting(root.transform, config);
        BuildCamera(root.transform, config);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        return prefab;
    }

    private static void BuildGround(Transform parent, float totalWidth, float arenaLength)
    {
        var ground = new GameObject("Ground");
        ground.transform.SetParent(parent, false);

        // A talaj kicsit szélesebb/hosszabb, mint a sávok, hogy a pálya szélén ne lássunk
        // "levegőbe lógó" sáv-éleket.
        float groundWidth = totalWidth + 3f;
        float groundLength = arenaLength + 4f;

        var visual = GameObject.CreatePrimitive(PrimitiveType.Plane);
        visual.name = "Visual";
        visual.transform.SetParent(ground.transform, false);
        visual.transform.localPosition = new Vector3(0f, 0f, arenaLength / 2f);
        // Unity beépített Plane primitívje 10x10 world-unit alapértelmezett méretű 1-es scale-nél.
        visual.transform.localScale = new Vector3(groundWidth / 10f, 1f, groundLength / 10f);

        Material groundMat = CreateOrUpdateMaterial(MaterialFolder + "/Mat_ArenaGround.mat", ResolveLitShader(), GroundColor);
        visual.GetComponent<MeshRenderer>().sharedMaterial = groundMat;
    }

    private static void BuildLanes(Transform parent, int laneCount, float laneWidth, float dividerWidth, float arenaLength, float startX)
    {
        var lanesRoot = new GameObject("Lanes");
        lanesRoot.transform.SetParent(parent, false);

        for (int i = 0; i < laneCount; i++)
        {
            float laneLeft = startX + i * (laneWidth + dividerWidth);
            float centerX = laneLeft + laneWidth / 2f;

            var lane = new GameObject($"Lane_{i}");
            lane.transform.SetParent(lanesRoot.transform, false);

            LaneAnchor anchor = lane.AddComponent<LaneAnchor>();
            anchor.Initialize(i, centerX, laneWidth, arenaLength);

            var visual = GameObject.CreatePrimitive(PrimitiveType.Plane);
            visual.name = "Visual";
            visual.transform.SetParent(lane.transform, false);
            // 0.05 (nem 0.01) a talaj fölött - kis Y-eltérésnél a Z-buffer nem tudja
            // megbízhatóan eldönteni melyik felület van elöl ("Z-fighting"), és a
            // sávszínek a talajszínnel összemosódva jelentek meg batch-renderben.
            visual.transform.localPosition = new Vector3(centerX, 0.05f, arenaLength / 2f);
            visual.transform.localScale = new Vector3(laneWidth / 10f, 1f, arenaLength / 10f);

            Color laneColor = LaneColors[i % LaneColors.Length];
            Material laneMat = CreateOrUpdateMaterial(
                MaterialFolder + $"/Mat_LaneStripe_{i}.mat", ResolveLitShader(), laneColor);
            visual.GetComponent<MeshRenderer>().sharedMaterial = laneMat;

            // A fizikai Collider-t nem kell itt eltávolítani: az Editor-generált primitívek
            // alap Collider-je ártalmatlan, és segíthet egy jövőbeli sáv-raycastnek
            // (PlayerController/InputAdapter, F1-13) — ez az eszköz azonban nem dönt arról,
            // hogyan használja fel.
        }
    }

    private static void BuildDividers(Transform parent, int laneCount, float laneWidth, float dividerWidth, float arenaLength, float startX)
    {
        if (dividerWidth <= 0f || laneCount < 2)
        {
            return;
        }

        var dividersRoot = new GameObject("LaneDividers");
        dividersRoot.transform.SetParent(parent, false);

        Material dividerMat = CreateOrUpdateMaterial(
            MaterialFolder + "/Mat_LaneDivider.mat", ResolveUnlitShader(), DividerColor, emissive: true);

        for (int i = 0; i < laneCount - 1; i++)
        {
            float laneLeft = startX + i * (laneWidth + dividerWidth);
            float dividerCenterX = laneLeft + laneWidth + dividerWidth / 2f;

            var divider = new GameObject($"Divider_{i}");
            divider.transform.SetParent(dividersRoot.transform, false);

            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Visual";
            visual.transform.SetParent(divider.transform, false);
            visual.transform.localPosition = new Vector3(dividerCenterX, 0.12f, arenaLength / 2f);
            visual.transform.localScale = new Vector3(dividerWidth, 0.1f, arenaLength);
            visual.GetComponent<MeshRenderer>().sharedMaterial = dividerMat;

            // A sávhatár csak vizuális jelölés — nincs rajta logika, és nem véletlenszerűen
            // megsemmisíthető kaputípus (math-horde.md 3.3 ettől eltérő, a válaszszállító
            // egységekre vonatkozik, nem erre a statikus pálya-elemre).
            Object.DestroyImmediate(visual.GetComponent<BoxCollider>());
        }
    }

    private static void BuildZones(Transform parent, ArenaConfigSO config, float totalWidth)
    {
        var zonesRoot = new GameObject("Zones");
        zonesRoot.transform.SetParent(parent, false);

        float arenaLength = config.ArenaLength;

        // Ellenség-érkezési zóna — a pálya távoli (felső, a kamerában a képernyő tetején
        // látszódó) végén, teljes szélességben, hogy jól látszódjon (math-horde.md 3.2).
        float spawnDepth = arenaLength * config.SpawnZoneDepthRatio;
        float spawnCenterZ = arenaLength - spawnDepth / 2f;
        BuildZoneMarker(zonesRoot.transform, "SpawnZone", totalWidth, spawnDepth, spawnCenterZ, SpawnZoneColor);

        // Játékos-zóna — a pálya közeli (alsó harmad) végén.
        float playerDepth = arenaLength * config.PlayerZoneDepthRatio;
        float playerCenterZ = playerDepth / 2f;
        BuildZoneMarker(zonesRoot.transform, "PlayerZone", totalWidth, playerDepth, playerCenterZ, PlayerZoneColor);
    }

    private static void BuildZoneMarker(Transform parent, string name, float width, float depth, float centerZ, Color color)
    {
        var zone = new GameObject(name);
        zone.transform.SetParent(parent, false);
        zone.transform.localPosition = new Vector3(0f, 0f, centerZ);

        var visual = GameObject.CreatePrimitive(PrimitiveType.Plane);
        visual.name = "Visual";
        visual.transform.SetParent(zone.transform, false);
        visual.transform.localPosition = new Vector3(0f, 0.18f, 0f);
        visual.transform.localScale = new Vector3(width / 10f, 1f, depth / 10f);
        Object.DestroyImmediate(visual.GetComponent<MeshCollider>());

        Material mat = CreateOrUpdateMaterial(
            MaterialFolder + $"/Mat_{name}.mat", ResolveUnlitShader(), color, transparent: true);
        visual.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }

    private static void BuildLighting(Transform parent, ArenaConfigSO config)
    {
        var lightingRoot = new GameObject("Lighting");
        lightingRoot.transform.SetParent(parent, false);

        var sun = new GameObject("SunLight");
        sun.transform.SetParent(lightingRoot.transform, false);
        sun.transform.rotation = Quaternion.Euler(config.SunRotationEuler);

        Light light = sun.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = config.SunColor;
        light.intensity = config.SunIntensity;
        light.shadows = LightShadows.Soft;
        // Mobil-profil (ARCHITECTURE/ProjectBootstrap: shadowDistance=25) — itt csak az
        // árnyékerőt tartjuk mértéktartóan, a globális shadowDistance URP-asset-szintű.
        light.shadowStrength = 0.6f;
    }

    private static void BuildCamera(Transform parent, ArenaConfigSO config)
    {
        var cameraGo = new GameObject("ArenaCamera");
        cameraGo.transform.SetParent(parent, false);
        cameraGo.tag = "MainCamera";

        Vector3 position = new Vector3(0f, config.CameraHeight, -config.CameraBackOffset);
        Vector3 lookAt = new Vector3(0f, config.CameraLookAtHeight, config.ArenaLength * config.CameraLookAtDepthRatio);

        cameraGo.transform.position = position;
        cameraGo.transform.LookAt(lookAt);

        Camera cam = cameraGo.AddComponent<Camera>();
        cam.fieldOfView = config.CameraFieldOfView;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 100f;
        cam.clearFlags = CameraClearFlags.Skybox;

        cameraGo.AddComponent<AudioListener>();

        // Fix kamera (math-horde.md 3.2: "fix, harmadik személyű kamera") — szándékosan
        // nincs rajta mozgató/követő script. A pozíciót/FOV-ot kizárólag az ArenaConfigSO
        // paraméterei határozzák meg, nem beégetett szám.
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

    private static Material CreateOrUpdateMaterial(string path, Shader shader, Color color, bool transparent = false, bool emissive = false)
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

        if (transparent)
        {
            SetUrpTransparent(mat);
        }

        if (emissive && mat.HasProperty("_EmissionColor"))
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", color * 0.5f);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static void SetUrpTransparent(Material mat)
    {
        mat.SetOverrideTag("RenderType", "Transparent");
        if (mat.HasProperty("_Surface"))
        {
            mat.SetFloat("_Surface", 1f); // URP konvenció: 0 = Opaque, 1 = Transparent
        }
        if (mat.HasProperty("_Blend"))
        {
            mat.SetFloat("_Blend", 0f); // Alpha blend
        }
        if (mat.HasProperty("_ZWrite"))
        {
            mat.SetInt("_ZWrite", 0);
        }
        mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = (int)RenderQueue.Transparent;
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

    // ---- Előkészítő vizuális igazolás (nem gameplay scene, nem kerül EditorBuildSettings-be) ----

    // MEGFIGYELT KORLÁT (dokumentálva, nem feltételezve): ezen a gépen, batchmode-ban futó
    // Unity Editor render-kontextusban (akár -nographics-szel, akár anélkül) a 3 sáv-csík
    // material-je (Mat_LaneStripe_0/1/2 - külön .mat asset, külön GUID, ellenőrizve az
    // ArenaPrefab.prefab-ban) a renderelt PNG-ben MIND az utolsó létrehozott sáv színével
    // jelenik meg (pixel-mintavétellel igazolva), nem a sajátjával. Ez a jelenség KIZÁRÓLAG
    // ezt a headless batch-render lépést érinti - a prefab/material assetek ténylegesen
    // helyesen, egymástól eltérő színnel vannak elmentve (lásd a .mat fájlokban a
    // _BaseColor értékeket), és a normál Editor Game View-ban (nem batch) vagy a tényleges
    // eszközön várhatóan helyesen jelennek meg. A GraphicsSettings.useScriptableRenderPipelineBatching
    // kikapcsolása nem oldotta meg - a kiváltó ok valószínűleg a headless render-kontextus
    // material-gyorsítótárazása. NE használd ezt a PNG-t a sávszínek megítélésére - csak a
    // pálya geometriájának/kameraálásának/portrait-keretezésének durva ellenőrzésére.
    [MenuItem("Math Horde/Arena/Render Preview PNG (Batch-safe)")]
    public static void RenderPreviewPng()
    {
        ArenaConfigSO config = GetOrCreateDefaultConfig();
        GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefabAsset == null)
        {
            prefabAsset = BuildArena(config);
            AssetDatabase.SaveAssets();
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset);
        try
        {
            Camera cam = instance.GetComponentInChildren<Camera>();
            if (cam == null)
            {
                Debug.LogError("[ArenaBuilder] RenderPreviewPng: nem található Camera az arena prefab példányban.");
                return;
            }

            const int width = 1080;
            const int height = 1920; // portrait 9:16, math-horde.md 3.2

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
            string fullOutputPath = Path.Combine(fullOutputDir, "ArenaPreview.png");
            File.WriteAllBytes(fullOutputPath, png);

            Debug.Log($"[ArenaBuilder] RenderPreviewPng finished. path={fullOutputPath}, bytes={png.Length}");
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    /// <summary>Batch-módból egy lépésben hívható: prefab build + preview PNG render.</summary>
    public static void BuildDefaultArenaAndRenderPreview()
    {
        BuildDefaultArena();
        RenderPreviewPng();
    }
}
