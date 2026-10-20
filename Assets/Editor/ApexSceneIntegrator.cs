using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
public static class ApexSceneIntegrator
{
    private const string ScenePath = "Assets/Scenes/APEX_V12_Race.unity";
    private const string PrefabPath = "Assets/RacingV12Pack/UnityAssets/RacingV12Pack/Prefabs/PlayerCar_Mercedes_F1.prefab";

    static ApexSceneIntegrator()
    {
        EditorApplication.delayCall += () =>
        {
            if (GameObject.Find("Baku_Waypoint_Manager") == null || 
                GameObject.Find("Baku_AI_Opponents") == null || 
                GameObject.Find("RacingHUD") == null)
            {
                IntegrateAllSystems();
            }
        };
    }

    [MenuItem("APEX V12/Integrate Full Race Experience (HUD, AI, Camera, Track)")]
    public static void IntegrateAllSystems()
    {
        Debug.Log("<color=#00D2BE><b>[APEX V12] Integrating complete racing experience...</b></color>");

        var currentScene = EditorSceneManager.GetActiveScene();
        if (currentScene.path != ScenePath)
        {
            if (File.Exists(ScenePath))
            {
                currentScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
        }

        // 1. Rebuild & Apply Materials to PlayerCar Prefab
        ApexVehicleFixer.RebuildPlayerCarPrefab();
        ApexVehicleFixer.UpdateScenePlayerCar();

        // 2. Build & Apply Track PBR Materials
        BakuTrackMaterialSetup.BuildAndApplyMaterials();

        // 3. Ensure Waypoint Manager & Visual Racing Line
        GameObject wpGo = GameObject.Find("Baku_Waypoint_Manager");
        if (wpGo == null)
        {
            wpGo = new GameObject("Baku_Waypoint_Manager");
            Undo.RegisterCreatedObjectUndo(wpGo, "Create Waypoint Manager");
        }
        var wpMgr = wpGo.GetComponent<BakuWaypointManager>();
        if (wpMgr == null) wpMgr = wpGo.AddComponent<BakuWaypointManager>();
        wpMgr.GenerateSplineWaypoints();
        wpMgr.BuildVisualRacingLine();

        // 4. Ensure AI Opponents Grid Spawner
        GameObject aiGo = GameObject.Find("Baku_AI_Opponents");
        if (aiGo == null)
        {
            aiGo = new GameObject("Baku_AI_Opponents");
            Undo.RegisterCreatedObjectUndo(aiGo, "Create AI Opponents");
        }
        var spawner = aiGo.GetComponent<FormulaAIGridSpawner>();
        if (spawner == null) spawner = aiGo.AddComponent<FormulaAIGridSpawner>();

        GameObject carPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (carPrefab != null)
        {
            var so = new SerializedObject(spawner);
            var prop = so.FindProperty("carPrefab");
            if (prop != null) prop.objectReferenceValue = carPrefab;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // 5. Setup Cockpit Camera as Default
        GameObject camGo = GameObject.FindWithTag("MainCamera");
        if (camGo == null) camGo = GameObject.Find("Main Camera");

        if (camGo != null)
        {
            var cockpitCam = camGo.GetComponent<F1CockpitCamera>();
            if (cockpitCam == null) cockpitCam = camGo.AddComponent<F1CockpitCamera>();
            cockpitCam.enabled = true;
            cockpitCam.SetCameraMode(F1CockpitCamera.CameraMode.Cockpit);

            var chaseCam = camGo.GetComponent<RacingCameraFollow>();
            if (chaseCam != null) chaseCam.enabled = false;
        }

        // 6. Setup Racing HUD Canvas
        GameObject existingHud = GameObject.Find("RacingHUD");
        GameObject dupHud = GameObject.Find("APEX_Racing_HUD");
        if (dupHud != null && existingHud != null)
        {
            Undo.DestroyObjectImmediate(dupHud);
        }
        else if (existingHud == null && dupHud == null)
        {
            var hudGo = new GameObject("RacingHUD");
            Undo.RegisterCreatedObjectUndo(hudGo, "Create Racing HUD Canvas");

            var canvas = hudGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = hudGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            hudGo.AddComponent<GraphicRaycaster>();
            hudGo.AddComponent<RacingHUD>();
        }

        // Ensure EventSystem
        if (Object.FindAnyObjectByType<EventSystem>() == null)
        {
            GameObject esGo = new GameObject("EventSystem");
            Undo.RegisterCreatedObjectUndo(esGo, "Create EventSystem");
            esGo.AddComponent<EventSystem>();
            esGo.AddComponent<StandaloneInputModule>();
        }

        // 7. Ensure Baku City Ground & Boundaries
        GameObject ground = GameObject.Find("Baku_City_Ground");
        if (ground != null)
        {
            ground.transform.position = new Vector3(0f, -0.25f, -250f);
            ground.transform.localScale = new Vector3(180f, 1f, 120f);
        }

        // 8. Ensure Track Spline Builder is deactivated to prevent overlapping tracks
        GameObject splineBuilder = GameObject.Find("Baku_Track_Spline_Builder");
        if (splineBuilder != null) splineBuilder.SetActive(false);

        // 9. Save Scene
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

        Debug.Log("<color=#00D2BE><b>[APEX V12] Complete integration successful! Play Mode ready.</b></color>");
    }
}
