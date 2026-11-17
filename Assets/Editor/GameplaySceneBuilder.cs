// GameplaySceneBuilder.cs
// F1-17 — "Gameplay scene összeállítása Editor-eszközzel (reprodukálható,
// nem kézi)" — the Fázis 1 capstone. math-horde.md 20. szakasza: "a
// szükséges jeleneteket, prefabokat és alapbeállításokat hozd létre
// projektfájlként vagy reprodukálható Editor-eszközzel" — this tool is that
// eszköz. It does three things, each idempotent (re-running it rebuilds the
// same result, never duplicates assets):
//
//   1. BuildDefaultGameplayConfig() — the Phase-1 default EnemyDefinitionSO
//      x5 / WeaponConfigSO / CombatBalanceSO / LevelDefinitionSO data assets
//      that nothing in the project had created yet (F1-09 only built the SO
//      *types* and their own unit tests; no actual gameplay data existed).
//      These reference the ArenaBuilder/EnemyPrefabBuilder prefabs that DO
//      already exist (F1-12/F1-14) rather than duplicating them.
//   2. BuildGameplayScene() — assembles the actual scene: instantiates the
//      Arena/HUD/LevelResultScreen/PlayerAvatar prefabs, adds the
//      Infrastructure adapters (InputAdapter, SceneFlowManagerBehaviour)
//      and the EnemyPresenter + its pool root, and wires every
//      [SerializeField] reference the composition root
//      (GameplaySessionBootstrap) needs — all through SerializedObject/
//      reflection, never by hand in the Inspector.
//   3. AddSceneToBuildSettings() — EditorBuildSettings.scenes so
//      BuildScript's real-scene branch activates (its placeholder-scene
//      fallback is documented to retire automatically once this exists).
//
// Use BuildAll() to run all three (plus the dependency prefabs/catalog if
// they are missing) in one call — this is what -executeMethod should target.
//
// Használat:
//   Editorból: Math Horde/Gameplay/Build Gameplay Scene (Full)
//   Batch-módból:
//     Unity -batchmode -nographics -quit -projectPath game
//           -executeMethod GameplaySceneBuilder.BuildAll -logFile <path>
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using MathHorde.Infrastructure.Configuration;
using MathHorde.Infrastructure.Input;
using MathHorde.Infrastructure.SceneFlow;
using MathHorde.Presentation.Arena;
using MathHorde.Presentation.Bootstrap;
using MathHorde.Presentation.Enemies;
using MathHorde.Presentation.Hud;
using MathHorde.Presentation.Player;
using MathHorde.Presentation.Result;

public static class GameplaySceneBuilder
{
    private const string ConfigFolder = "Assets/_Project/Configuration";
    private const string EnemyConfigFolder = ConfigFolder + "/Enemies";
    private const string WeaponAssetPath = ConfigFolder + "/WeaponStarterBlaster.asset";
    private const string CombatBalanceAssetPath = ConfigFolder + "/CombatBalanceDefault.asset";
    private const string LevelAssetPath = ConfigFolder + "/LevelW1L01.asset";

    private const string ScenesFolder = "Assets/_Project/Scenes";
    private const string ScenePath = ScenesFolder + "/Gameplay.unity";

    private const string ArenaPrefabPath = "Assets/_Project/Prefabs/Arena/ArenaPrefab.prefab";

    [MenuItem("Math Horde/Gameplay/Build Gameplay Scene (Full)")]
    public static void BuildAll()
    {
        EnsureDependencyPrefabsExist();
        BuildDefaultGameplayConfig(out LevelDefinitionSO level);
        BuildGameplayScene(level);
        AddSceneToBuildSettings();

        Debug.Log("[GameplaySceneBuilder] BuildAll finished. scene=" + ScenePath);
    }

    /// <summary>
    /// Builds (or rebuilds-in-place) every prefab this scene depends on, if
    /// missing — ArenaBuilder/EnemyPrefabBuilder/GameplayHudBuilder/
    /// LevelResultScreenBuilder/PlayerPrefabBuilder each already being
    /// idempotent themselves (F1-12/14/15/16, and this task's own
    /// PlayerPrefabBuilder).
    /// </summary>
    [MenuItem("Math Horde/Gameplay/0. Ensure Dependency Prefabs Exist")]
    public static void EnsureDependencyPrefabsExist()
    {
        EnsureTmpEssentialResourcesImported();

        if (AssetDatabase.LoadAssetAtPath<GameObject>(ArenaPrefabPath) == null)
        {
            ArenaBuilder.BuildDefaultArena();
        }

        if (AssetDatabase.LoadAssetAtPath<EnemyView>("Assets/_Project/Prefabs/Enemies/Enemy_Basic.prefab") == null)
        {
            EnemyPrefabBuilder.BuildAllAndCatalog();
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/GameplayHUD.prefab") == null)
        {
            GameplayHudBuilder.BuildHud();
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/LevelResultScreen.prefab") == null)
        {
            LevelResultScreenBuilder.BuildLevelResultScreen();
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player/PlayerAvatar.prefab") == null)
        {
            PlayerPrefabBuilder.BuildPlayerAvatar();
        }

        AssetDatabase.SaveAssets();
    }

    /// <summary>
    /// A genuine, previously-latent project gap this task uncovered (not
    /// specific to the gameplay scene itself): NOTHING in the project had
    /// ever imported TextMeshPro's "TMP Essential Resources" — there was no
    /// Assets/TextMesh Pro/Resources/TMP Settings.asset at all, even though
    /// GameplayHudBuilder/LevelResultScreenBuilder (earlier tasks) already
    /// use TextMeshProUGUI. Every earlier test finished in one or two
    /// frames, before TMP_Settings' own 1-second-delayed "would you like to
    /// import TMP essentials?" check (TMP_Settings.DelayShowPackageImporterWindow)
    /// had a chance to fire. This task's PlayMode integration test is the
    /// first one in the project to keep a TMP-containing scene running for
    /// several real seconds, which is long enough for that timer to elapse
    /// mid-test and log an unhandled "[Error] No graphic device is
    /// available to initialize the view." (it tries to open an EditorWindow
    /// under -nographics) — observed to fail Unity Test Framework's
    /// UnityLogCheckDelegatingCommand regardless of
    /// LogAssert.ignoreFailingMessages (that flag does not cover this
    /// path).
    ///
    /// UPDATE (F1-19): the original fix here (a hand-rolled, minimal
    /// TMP_Settings ScriptableObject at Assets/_Project/Resources/) only
    /// silenced the delayed-prompt symptom — it left m_defaultFontAsset
    /// empty AND, as F1-19 discovered, the project was ALSO entirely
    /// missing TMP's runtime shaders (com.unity.ugui does not ship them;
    /// they are exclusively part of the TMP Essential Resources package),
    /// so every TMP text would have rendered with a broken/missing
    /// material regardless of font. F1-19 replaced the hand-rolled
    /// TMP_Settings.asset with the REAL package contents (shaders, the
    /// stock LiberationSans SDF + TTF + TMP Settings.asset, same GUIDs),
    /// versioned directly at the canonical "Assets/TextMesh Pro/..." path
    /// (see that task's Napló / TmpHungarianFontSetup.cs header comment for
    /// why direct versioning was chosen over AssetDatabase.ImportPackage).
    /// Only the Hungarian-glyph-coverage default-font override still needs
    /// an idempotent Editor step, which is what remains here.
    /// </summary>
    private static void EnsureTmpEssentialResourcesImported()
    {
        // A kanonikus TMP Essential Resources fa (shaderek, gyári
        // LiberationSans SDF/.ttf, TMP Settings.asset) a repóban közvetlenül
        // verziózva van (Assets/TextMesh Pro/...) — nincs mit "importálni"
        // futásidőben. Ami idempotens Editor-lépést igényel, az a magyar
        // ékezetkészlettel (ő/ű is) ellátott SAJÁT alapértelmezett
        // betűtípus biztosítása — lásd TmpHungarianFontSetup.EnsureAll().
        TmpHungarianFontSetup.EnsureAll();
    }

    /// <summary>
    /// Builds the Phase-1 default gameplay data assets (EnemyDefinitionSO x5,
    /// WeaponConfigSO, CombatBalanceSO, LevelDefinitionSO) if they do not
    /// already exist, and returns the level asset BuildGameplayScene needs.
    /// Re-running this does not duplicate assets — each Get-or-create step
    /// loads the existing asset at its fixed path first.
    /// </summary>
    [MenuItem("Math Horde/Gameplay/1. Build Default Gameplay Config")]
    public static void BuildDefaultGameplayConfigMenuEntry() => BuildDefaultGameplayConfig(out _);

    public static void BuildDefaultGameplayConfig(out LevelDefinitionSO level)
    {
        EnsureFolder(EnemyConfigFolder);

        var basic = GetOrCreateEnemy("basic_bot", "Enemy_Basic", MathHorde.Core.Levels.EnemyType.Basic,
            maxHealth: 10, movementSpeed: 2.0f, contactDamage: 10, rewardValue: 1, spawnGroupSize: 1);
        var fast = GetOrCreateEnemy("fast_bot", "Enemy_Fast", MathHorde.Core.Levels.EnemyType.Fast,
            maxHealth: 8, movementSpeed: 4.0f, contactDamage: 8, rewardValue: 1, spawnGroupSize: 1);
        var armored = GetOrCreateEnemy("armored_bot", "Enemy_Armored", MathHorde.Core.Levels.EnemyType.Armored,
            maxHealth: 30, movementSpeed: 1.0f, contactDamage: 15, rewardValue: 2, spawnGroupSize: 1);
        var boss = GetOrCreateEnemy("boss_bot", "Enemy_Boss", MathHorde.Core.Levels.EnemyType.Boss,
            maxHealth: 150, movementSpeed: 1.0f, contactDamage: 25, rewardValue: 10, spawnGroupSize: 1);
        var swarmlet = GetOrCreateEnemy("swarmlet_bot", "Enemy_Swarmlet", MathHorde.Core.Levels.EnemyType.Swarmlet,
            maxHealth: 1, movementSpeed: 3.0f, contactDamage: 5, rewardValue: 0, spawnGroupSize: 4);

        WeaponConfigSO weapon = GetOrCreateWeapon();
        CombatBalanceSO combatBalance = GetOrCreateCombatBalance();
        GameObject arenaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArenaPrefabPath);

        level = GetOrCreateLevel(weapon, arenaPrefab, basic, fast, armored);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static EnemyDefinitionSO GetOrCreateEnemy(
        string id, string prefabFileName, MathHorde.Core.Levels.EnemyType type,
        int maxHealth, float movementSpeed, int contactDamage, int rewardValue, int spawnGroupSize)
    {
        string path = $"{EnemyConfigFolder}/{id}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<EnemyDefinitionSO>(path);
        if (existing != null)
        {
            return existing;
        }

        var enemy = ScriptableObject.CreateInstance<EnemyDefinitionSO>();
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Project/Prefabs/Enemies/{prefabFileName}.prefab");

        SetPrivateField(enemy, "_id", id);
        SetPrivateField(enemy, "_displayNameKey", "enemy." + id);
        SetPrivateField(enemy, "_type", type);
        SetPrivateField(enemy, "_maxHealth", maxHealth);
        SetPrivateField(enemy, "_movementSpeed", movementSpeed);
        SetPrivateField(enemy, "_contactDamage", contactDamage);
        SetPrivateField(enemy, "_rewardValue", rewardValue);
        SetPrivateField(enemy, "_spawnGroupSize", spawnGroupSize);
        SetPrivateField(enemy, "_prefab", prefab);

        AssetDatabase.CreateAsset(enemy, path);
        EditorUtility.SetDirty(enemy);
        return enemy;
    }

    private static WeaponConfigSO GetOrCreateWeapon()
    {
        var existing = AssetDatabase.LoadAssetAtPath<WeaponConfigSO>(WeaponAssetPath);
        if (existing != null)
        {
            return existing;
        }

        var weapon = ScriptableObject.CreateInstance<WeaponConfigSO>();
        SetPrivateField(weapon, "_id", "starter_blaster");
        SetPrivateField(weapon, "_displayNameKey", "weapon.starter");
        // Hitscan (ProjectileSpeed == 0, game-logic.md 3.3): no projectile
        // prefab needed, and damage resolves the instant the shot fires —
        // simplest, fastest-to-observe wiring for the Phase-1 vertical slice.
        SetPrivateField(weapon, "_projectileSpeed", 0f);

        AssetDatabase.CreateAsset(weapon, WeaponAssetPath);
        EditorUtility.SetDirty(weapon);
        return weapon;
    }

    private static CombatBalanceSO GetOrCreateCombatBalance()
    {
        var existing = AssetDatabase.LoadAssetAtPath<CombatBalanceSO>(CombatBalanceAssetPath);
        if (existing != null)
        {
            return existing;
        }

        // Every field's own default is already neutral (1.0x multipliers) —
        // nothing to override.
        var combatBalance = ScriptableObject.CreateInstance<CombatBalanceSO>();
        AssetDatabase.CreateAsset(combatBalance, CombatBalanceAssetPath);
        EditorUtility.SetDirty(combatBalance);
        return combatBalance;
    }

    private static LevelDefinitionSO GetOrCreateLevel(
        WeaponConfigSO weapon, GameObject environmentPrefab,
        EnemyDefinitionSO basic, EnemyDefinitionSO fast, EnemyDefinitionSO armored)
    {
        var existing = AssetDatabase.LoadAssetAtPath<LevelDefinitionSO>(LevelAssetPath);
        if (existing != null)
        {
            return existing;
        }

        var level = ScriptableObject.CreateInstance<LevelDefinitionSO>();
        SetPrivateField(level, "_id", "w1-l01");
        SetPrivateField(level, "_displayNameKey", "level.w1l01.name");
        SetPrivateField(level, "_laneCount", 3); // egyezik az ArenaConfigSO alap LaneCount-jával (lásd Napló)
        SetPrivateField(level, "_targetDurationSeconds", 150f);
        SetPrivateField(level, "_startingAmmo", 60);
        SetPrivateField(level, "_maxAmmo", 200);
        SetPrivateField(level, "_startingHealth", 100);
        SetPrivateField(level, "_maxHealth", 100);
        SetPrivateField(level, "_startingShield", 0);
        SetPrivateField(level, "_maxShield", 0);
        SetPrivateField(level, "_weapon", weapon);
        SetPrivateField(level, "_environmentPrefab", environmentPrefab);

        var wave0Spawn = NewWaveSpawnEntry(basic, count: 10, interval: 1.2f);
        var wave0 = NewWaveDefinitionEntry(waveIndex: 0, startGameTime: 0f, duration: 60f, wave0Spawn);

        var wave1SpawnFast = NewWaveSpawnEntry(fast, count: 8, interval: 1.0f);
        var wave1SpawnArmored = NewWaveSpawnEntry(armored, count: 4, interval: 2.0f);
        var wave1 = NewWaveDefinitionEntry(waveIndex: 1, startGameTime: 60f, duration: 60f, wave1SpawnFast, wave1SpawnArmored);

        SetPrivateField(level, "_waves", new List<LevelDefinitionSO.WaveDefinitionEntry> { wave0, wave1 });

        AssetDatabase.CreateAsset(level, LevelAssetPath);
        EditorUtility.SetDirty(level);
        return level;
    }

    private static LevelDefinitionSO.WaveSpawnEntry NewWaveSpawnEntry(EnemyDefinitionSO enemy, int count, float interval)
    {
        var spawn = new LevelDefinitionSO.WaveSpawnEntry();
        SetPrivateField(spawn, "_enemy", enemy);
        SetPrivateField(spawn, "_count", count);
        SetPrivateField(spawn, "_spawnIntervalSeconds", interval);
        SetPrivateField(spawn, "_allowedLanes", new List<int>()); // üres = bármelyik sáv
        return spawn;
    }

    private static LevelDefinitionSO.WaveDefinitionEntry NewWaveDefinitionEntry(
        int waveIndex, float startGameTime, float duration, params LevelDefinitionSO.WaveSpawnEntry[] spawns)
    {
        var wave = new LevelDefinitionSO.WaveDefinitionEntry();
        SetPrivateField(wave, "_waveIndex", waveIndex);
        SetPrivateField(wave, "_startGameTime", startGameTime);
        SetPrivateField(wave, "_durationSeconds", duration);
        SetPrivateField(wave, "_spawns", new List<LevelDefinitionSO.WaveSpawnEntry>(spawns));
        return wave;
    }

    /// <summary>
    /// Assembles the actual Gameplay.unity scene: instantiates every
    /// dependency prefab, adds the Infrastructure adapters, and wires every
    /// GameplaySessionBootstrap [SerializeField] reference.
    /// </summary>
    [MenuItem("Math Horde/Gameplay/2. Build Gameplay Scene")]
    public static void BuildGameplaySceneMenuEntry()
    {
        BuildDefaultGameplayConfig(out LevelDefinitionSO level);
        BuildGameplayScene(level);
    }

    public static void BuildGameplayScene(LevelDefinitionSO level)
    {
        EnsureFolder(ScenesFolder);

        // DefaultGameObjects (not EmptyScene): seeds the scene with Unity's
        // normal default Lighting Settings/Skybox material in RenderSettings
        // (an EmptyScene has none, which left the batch-mode preview render
        // completely blank — see this task's Napló). The default "Main
        // Camera"/"Directional Light" GameObjects it also creates are
        // removed immediately below — ArenaBuilder's own ArenaCamera/
        // SunLight (already part of the Arena prefab) are this scene's only
        // camera/light, so there is no duplicate "MainCamera"-tagged object.
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        RemoveDefaultSceneObject(scene, "Main Camera");
        RemoveDefaultSceneObject(scene, "Directional Light");

        // BUG OBSERVED AND FIXED (see this task's Napló): `level` was
        // obtained by the CALLER (BuildDefaultGameplayConfig) BEFORE
        // EditorSceneManager.NewScene(...) ran above. Every other asset
        // reference this method uses is (re-)loaded via AssetDatabase AFTER
        // that NewScene call and serialized fine; `level` was the only one
        // obtained before it, and NewScene(..., Single) was observed to
        // turn that specific in-memory ScriptableObject reference into a
        // Unity "fake-null" Object — it still passed `!= null` checks
        // upstream, but SerializedObject.objectReferenceValue silently
        // wrote {fileID: 0} for it, and NOTHING else, into the saved scene.
        // Re-resolving it fresh from disk here (same pattern already used
        // for every other asset below) fixes it.
        level = AssetDatabase.LoadAssetAtPath<LevelDefinitionSO>(LevelAssetPath);

        GameObject arenaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArenaPrefabPath);
        GameObject arenaInstance = (GameObject)PrefabUtility.InstantiatePrefab(arenaPrefab);
        arenaInstance.name = "Arena";

        GameObject hudPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/GameplayHUD.prefab");
        GameObject hudInstance = (GameObject)PrefabUtility.InstantiatePrefab(hudPrefab);
        hudInstance.name = "GameplayHUD";

        GameObject resultPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/LevelResultScreen.prefab");
        GameObject resultInstance = (GameObject)PrefabUtility.InstantiatePrefab(resultPrefab);
        resultInstance.name = "LevelResultScreen";

        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player/PlayerAvatar.prefab");
        GameObject playerInstance = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
        playerInstance.name = "Player";
        PositionPlayerInPlayerZone(playerInstance.transform);

        var enemyPresenterGo = new GameObject("EnemyPresenter");
        var enemyPool = new GameObject("EnemyPool");
        enemyPool.transform.SetParent(enemyPresenterGo.transform, false);
        EnemyPresenter enemyPresenter = enemyPresenterGo.AddComponent<EnemyPresenter>();
        EnemyVisualCatalogSO catalog = EnemyPrefabBuilder.GetOrCreateCatalog();
        var presenterSo = new SerializedObject(enemyPresenter);
        presenterSo.FindProperty("_catalog").objectReferenceValue = catalog;
        presenterSo.FindProperty("_poolRoot").objectReferenceValue = enemyPool.transform;
        presenterSo.ApplyModifiedPropertiesWithoutUndo();

        var inputGo = new GameObject("InputAdapter");
        InputAdapter inputAdapter = inputGo.AddComponent<InputAdapter>();

        var sceneFlowGo = new GameObject("SceneFlow");
        SceneFlowManagerBehaviour sceneFlow = sceneFlowGo.AddComponent<SceneFlowManagerBehaviour>();
        // _sceneBindings marad üresen (lásd GameplaySessionBootstrap fejkommentje):
        // Fázis 1-ben nincs MainMenu/WorldSelection scene, egy AppState-átmenet
        // sem tölthet be mást, mint ezt a jelenetet.

        var bootstrapGo = new GameObject("GameSessionBootstrap");
        GameplaySessionBootstrap bootstrap = bootstrapGo.AddComponent<GameplaySessionBootstrap>();

        EnemyDefinitionSO[] enemyAssets = LoadAllEnemyAssets();
        WeaponConfigSO weapon = AssetDatabase.LoadAssetAtPath<WeaponConfigSO>(WeaponAssetPath);
        CombatBalanceSO combatBalance = AssetDatabase.LoadAssetAtPath<CombatBalanceSO>(CombatBalanceAssetPath);

        var bootstrapSo = new SerializedObject(bootstrap);
        AssignArray(bootstrapSo, "_enemyAssets", enemyAssets);
        AssignArray(bootstrapSo, "_weaponAssets", new WeaponConfigSO[] { weapon });
        bootstrapSo.FindProperty("_levelAsset").objectReferenceValue = level;
        bootstrapSo.FindProperty("_combatBalance").objectReferenceValue = combatBalance;
        bootstrapSo.FindProperty("_arenaRoot").objectReferenceValue = arenaInstance.transform;
        bootstrapSo.FindProperty("_hud").objectReferenceValue = hudInstance.GetComponent<GameplayHUD>();
        bootstrapSo.FindProperty("_resultScreen").objectReferenceValue = resultInstance.GetComponent<LevelResultScreen>();
        bootstrapSo.FindProperty("_enemyPresenter").objectReferenceValue = enemyPresenter;
        bootstrapSo.FindProperty("_playerController").objectReferenceValue = playerInstance.GetComponent<PlayerController>();
        bootstrapSo.FindProperty("_playerView").objectReferenceValue = playerInstance.GetComponent<PlayerView>();
        bootstrapSo.FindProperty("_inputAdapter").objectReferenceValue = inputAdapter;
        bootstrapSo.FindProperty("_sceneFlow").objectReferenceValue = sceneFlow;
        bootstrapSo.ApplyModifiedPropertiesWithoutUndo();

        bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
        if (!saved)
        {
            throw new InvalidOperationException($"[GameplaySceneBuilder] Nem sikerült elmenteni a scene-t: {ScenePath}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Render the preview in the SAME call the geometry was just created
        // in (not a later, separate -executeMethod invocation that has to
        // EditorSceneManager.OpenScene(...) the saved result back in) — see
        // this task's Napló for why a re-opened scene rendered blank here.
        Camera previewCamera = arenaInstance.GetComponentInChildren<Camera>(true);
        if (previewCamera != null)
        {
            RenderCameraToPreviewPng(previewCamera, "GameplayScenePreview.png");
        }

        Debug.Log($"[GameplaySceneBuilder] BuildGameplayScene finished. scene={ScenePath}");
    }

    private static void RemoveDefaultSceneObject(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == name)
            {
                UnityEngine.Object.DestroyImmediate(root);
                return;
            }
        }
    }

    private static void PositionPlayerInPlayerZone(Transform playerTransform)
    {
        ArenaConfigSO config = ArenaBuilder.GetOrCreateDefaultConfig();
        float arenaLength = config.ArenaLength;
        float playerDepth = arenaLength * config.PlayerZoneDepthRatio;
        float playerCenterZ = playerDepth / 2f;

        Vector3 position = playerTransform.position;
        position.x = 0f; // lane 0 kezdeti X-ét a PlayerView.Initialize állítja be futásidőben
        position.z = playerCenterZ;
        playerTransform.position = position;
    }

    private static EnemyDefinitionSO[] LoadAllEnemyAssets()
    {
        string[] guids = AssetDatabase.FindAssets("t:EnemyDefinitionSO", new[] { EnemyConfigFolder });
        var result = new EnemyDefinitionSO[guids.Length];
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            result[i] = AssetDatabase.LoadAssetAtPath<EnemyDefinitionSO>(path);
        }

        return result;
    }

    private static void AssignArray<T>(SerializedObject so, string propertyName, T[] values) where T : UnityEngine.Object
    {
        SerializedProperty prop = so.FindProperty(propertyName);
        prop.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
        {
            prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }

    /// <summary>
    /// Adds Gameplay.unity to EditorBuildSettings.scenes (BuildScript's
    /// placeholder-scene fallback then stops activating — see that file's
    /// header comment).
    /// </summary>
    [MenuItem("Math Horde/Gameplay/3. Add Scene To Build Settings")]
    public static void AddSceneToBuildSettings()
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        scenes.RemoveAll(s => s.path == ScenePath);
        scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();

        Debug.Log($"[GameplaySceneBuilder] AddSceneToBuildSettings finished. totalScenes={scenes.Count}");
    }

    /// <summary>
    /// Opens the already-built Gameplay.unity and renders its ArenaCamera to
    /// a PNG — same batch-render color caveat as ArenaBuilder.RenderPreviewPng
    /// (unity-ui skill "Vizuális ellenőrzés batch-módban"): geometry/layout
    /// verification only, NOT a color/material judgment.
    ///
    /// KNOWN LIMITATION (observed, documented — not assumed): re-opening the
    /// saved scene in a fresh -executeMethod invocation and calling
    /// Camera.Render() immediately produced a BLANK (flat gray, no
    /// geometry at all) PNG on this machine, even with the scene's lighting/
    /// skybox otherwise correct and the exact same render code that works
    /// for ArenaBuilder.RenderPreviewPng (which renders within the Editor's
    /// already-open/warm scene, never calling OpenScene first). The likely
    /// cause is the render pipeline not being primed yet for a camera whose
    /// scene was only just synchronously loaded in the same call — a known
    /// class of Editor-batchmode timing gotcha, not a product defect. The
    /// reliable path is BuildGameplayScene's own preview render (same
    /// process, same call, camera already live since the scene was
    /// assembled) — use that output. This method is kept for ad-hoc re-
    /// rendering convenience only; treat a blank result from THIS entry
    /// point as inconclusive, not as evidence of a broken scene.
    /// </summary>
    [MenuItem("Math Horde/Gameplay/4. Render Scene Preview PNG (Batch-safe, geometry only)")]
    public static void RenderGameplayScenePreviewPng()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        Camera cam = Camera.main;
        if (cam == null)
        {
            Debug.LogError("[GameplaySceneBuilder] RenderGameplayScenePreviewPng: nem található MainCamera a Gameplay scene-ben.");
            return;
        }

        RenderCameraToPreviewPng(cam, "GameplayScenePreview.png");
    }

    private static void RenderCameraToPreviewPng(Camera cam, string fileName)
    {
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
        UnityEngine.Object.DestroyImmediate(tex);

        string fullOutputDir = Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, "Builds");
        Directory.CreateDirectory(fullOutputDir);
        string fullOutputPath = Path.Combine(fullOutputDir, fileName);
        File.WriteAllBytes(fullOutputPath, png);

        Debug.Log($"[GameplaySceneBuilder] RenderCameraToPreviewPng finished. path={fullOutputPath}, bytes={png.Length}");
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
        {
            throw new ArgumentException($"Field '{fieldName}' not found on {target.GetType()}.", nameof(fieldName));
        }

        field.SetValue(target, value);
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
