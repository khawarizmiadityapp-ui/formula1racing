#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;

/// <summary>
/// Instantiates the procedural Baku-inspired circuit models in the active scene.
/// Copy the entire BakuCircuitPack folder under Assets before using this menu.
/// </summary>
public static class BakuCircuitSetup
{
    private const string RootName = "APEX_BakuCircuit_Generated";
    [MenuItem("APEX V12/Build Baku-Inspired Circuit")]
    public static void BuildCircuit()
    {
        const string modelFolder = "Assets/BakuCircuitPack/Models/";
        string[] required = {
            "Baku_StreetCircuit_Ground", "Baku_StreetCircuit_Water", "Baku_StreetCircuit_Promenade",
            "Baku_StreetCircuit_Road", "Baku_StreetCircuit_Curbs", "Baku_StreetCircuit_Markings",
            "Baku_StreetCircuit_PitLane", "Baku_StreetCircuit_Barriers", "Baku_StreetCircuit_Environment",
            "Baku_StreetCircuit_StartGantry"
        };
        foreach (string asset in required)
        {
            string path = modelFolder + asset + ".obj";
            if (!File.Exists(path))
            {
                EditorUtility.DisplayDialog("Baku Circuit Pack", "Missing model file:\n" + path + "\n\nCopy BakuCircuitPack into Assets and wait for Unity to finish importing.", "OK");
                return;
            }
        }
        GameObject old = GameObject.Find(RootName);
        if (old != null && !EditorUtility.DisplayDialog("Rebuild Baku Circuit", "A generated Baku circuit already exists. Replace it?", "Replace", "Cancel")) return;
        if (old != null) Undo.DestroyObjectImmediate(old);

        GameObject root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Build Baku Circuit");
        AddModel(root.transform, modelFolder, "Ground", "Baku_StreetCircuit_Ground", true, false);
        AddModel(root.transform, modelFolder, "Water", "Baku_StreetCircuit_Water", false, false);
        AddModel(root.transform, modelFolder, "Promenade", "Baku_StreetCircuit_Promenade", true, false);
        AddModel(root.transform, modelFolder, "Road", "Baku_StreetCircuit_Road", true, true);
        AddModel(root.transform, modelFolder, "Curbs", "Baku_StreetCircuit_Curbs", true, false);
        AddModel(root.transform, modelFolder, "RoadMarkings", "Baku_StreetCircuit_Markings", false, false);
        AddModel(root.transform, modelFolder, "PitLane", "Baku_StreetCircuit_PitLane", true, true);
        AddModel(root.transform, modelFolder, "Barriers", "Baku_StreetCircuit_Barriers", true, true);
        AddModel(root.transform, modelFolder, "Environment", "Baku_StreetCircuit_Environment", true, false);
        AddModel(root.transform, modelFolder, "StartFinishGantry", "Baku_StreetCircuit_StartGantry", true, false);
        Selection.activeGameObject = root;
        EditorSceneManagerMarkDirty();
        EditorUtility.DisplayDialog("Baku Circuit Created", "Generated the Baku-inspired circuit at world origin.\n\nCheck the Scene view from above, then place PlayerCar on the main straight near X = -340, Z = -500, with its nose pointing toward +X.\n\nIf you already have another track, disable or remove that track to prevent overlapping roads.", "OK");
    }

    private static void AddModel(Transform parent, string folder, string childName, string assetName, bool addCollider, bool roadCollider)
    {
        string path = folder + assetName + ".obj";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogError("Could not load imported OBJ model: " + path);
            return;
        }
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        if (instance == null) instance = Object.Instantiate(prefab);
        Undo.RegisterCreatedObjectUndo(instance, "Add " + childName);
        instance.name = childName;
        instance.transform.SetParent(parent, false);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;
        if (addCollider) AddStaticMeshColliders(instance, roadCollider);
    }

    private static void AddStaticMeshColliders(GameObject root, bool isRoad)
    {
        MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
        foreach (MeshFilter filter in filters)
        {
            if (filter.sharedMesh == null) continue;
            MeshCollider existing = filter.GetComponent<MeshCollider>();
            if (existing == null) existing = Undo.AddComponent<MeshCollider>(filter.gameObject);
            existing.sharedMesh = null;
            existing.sharedMesh = filter.sharedMesh;
            existing.convex = false;
            // All generated circuit objects are static; never add a Rigidbody to these mesh colliders.
        }
    }

    private static void EditorSceneManagerMarkDirty()
    {
        if (UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().IsValid())
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
    }

    [MenuItem("APEX V12/Activate New Baku Circuit Pack (Street Track)")]
    public static void ActivateNewBakuCircuit()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        GameObject newCircuit = GameObject.Find("APEX_BakuCircuit_Generated");
        if (newCircuit == null)
        {
            foreach (var rootGo in scene.GetRootGameObjects())
            {
                if (rootGo.name.Contains("APEX_BakuCircuit"))
                {
                    newCircuit = rootGo;
                    newCircuit.name = "APEX_BakuCircuit_Generated";
                    break;
                }
            }
        }

        if (newCircuit != null)
        {
            Undo.RecordObject(newCircuit, "Activate New Baku Circuit");
            newCircuit.SetActive(true);
            RefreshCircuitMeshes();
        }
        else
        {
            BuildCircuit();
            newCircuit = GameObject.Find("APEX_BakuCircuit_Generated");
        }

        // Deactivate spline builder to prevent overlapping
        GameObject splineBuilder = GameObject.Find("Baku_Track_Spline_Builder");
        if (splineBuilder == null)
        {
            foreach (var rootGo in scene.GetRootGameObjects())
            {
                if (rootGo.name.Contains("Spline_Builder"))
                {
                    splineBuilder = rootGo;
                    break;
                }
            }
        }
        if (splineBuilder != null)
        {
            Undo.RecordObject(splineBuilder, "Deactivate Spline Builder");
            splineBuilder.SetActive(false);
        }

        // Position PlayerCar at New Baku Circuit Start Grid
        GameObject playerCar = GameObject.Find("PlayerCar");
        if (playerCar != null)
        {
            Undo.RecordObject(playerCar.transform, "Position PlayerCar New Circuit");
            playerCar.transform.position = new Vector3(-340f, 0.45f, -500f);
            playerCar.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

            var rb = playerCar.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            var ctrl = playerCar.GetComponent<FormulaCarArcadeController>();
            if (ctrl != null)
            {
                ctrl.SetSpawnPoint(new Vector3(-340f, 0.45f, -500f), new Vector3(0f, 90f, 0f));
            }
        }

        // Position Main Camera behind car
        var cam = Camera.main;
        if (cam != null)
        {
            Undo.RecordObject(cam.transform, "Position Camera New Circuit");
            cam.transform.position = new Vector3(-345.6f, 2.65f, -500f);
            cam.transform.rotation = Quaternion.Euler(8f, 90f, 0f);
        }

        EditorSceneManagerMarkDirty();
        Debug.Log("<color=#00FF99><b>[APEX V12] Switched to New Baku Circuit Pack (Street Track at Z=-500). Car placed at Start Grid facing +X!</b></color>");
    }

    [MenuItem("APEX V12/Activate Baku Spline Track (Castle Corner Track)")]
    public static void ActivateSplineCircuit()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        GameObject newCircuit = GameObject.Find("APEX_BakuCircuit_Generated");
        if (newCircuit != null)
        {
            Undo.RecordObject(newCircuit, "Deactivate New Baku Circuit");
            newCircuit.SetActive(false);
        }

        GameObject splineBuilder = null;
        foreach (var rootGo in scene.GetRootGameObjects())
        {
            if (rootGo.name.Contains("Spline_Builder"))
            {
                splineBuilder = rootGo;
                break;
            }
        }

        if (splineBuilder != null)
        {
            Undo.RecordObject(splineBuilder, "Activate Spline Builder");
            splineBuilder.SetActive(true);
            var builderComp = splineBuilder.GetComponent<BakuTrackSplineBuilder>();
            if (builderComp != null)
            {
                builderComp.PositionPlayerCarAtPole();
            }
        }

        EditorSceneManagerMarkDirty();
        Debug.Log("<color=#00D4FF><b>[APEX V12] Switched to Baku Spline Track (Castle Corner at Z=-200). Car placed at Pole Position!</b></color>");
    }

    [MenuItem("APEX V12/Refresh Circuit Meshes & Colliders (Clean Turn 1)")]
    public static void RefreshCircuitMeshes()
    {
        AssetDatabase.ImportAsset("Assets/BakuCircuitPack/Models/Baku_StreetCircuit_Barriers.obj", ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset("Assets/BakuCircuitPack/Models/Baku_StreetCircuit_Curbs.obj", ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset("Assets/BakuCircuitPack/Models/Baku_StreetCircuit_Markings.obj", ImportAssetOptions.ForceUpdate);

        GameObject circuit = GameObject.Find("APEX_BakuCircuit_Generated");
        if (circuit != null)
        {
            const string modelFolder = "Assets/BakuCircuitPack/Models/";
            string[] childrenToRefresh = { "Barriers", "Curbs", "RoadMarkings" };
            string[] assetNames = { "Baku_StreetCircuit_Barriers", "Baku_StreetCircuit_Curbs", "Baku_StreetCircuit_Markings" };

            for (int i = 0; i < childrenToRefresh.Length; i++)
            {
                Transform t = circuit.transform.Find(childrenToRefresh[i]);
                if (t != null)
                {
                    string path = modelFolder + assetNames[i] + ".obj";
                    GameObject imported = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (imported != null)
                    {
                        MeshFilter srcMf = imported.GetComponentInChildren<MeshFilter>(true);
                        MeshFilter dstMf = t.GetComponentInChildren<MeshFilter>(true);
                        if (srcMf != null && dstMf != null && srcMf.sharedMesh != null)
                        {
                            dstMf.sharedMesh = srcMf.sharedMesh;
                            MeshCollider mc = t.GetComponentInChildren<MeshCollider>(true);
                            if (mc != null)
                            {
                                mc.sharedMesh = null;
                                mc.sharedMesh = srcMf.sharedMesh;
                            }
                        }
                    }
                }
            }
            EditorSceneManagerMarkDirty();
            Debug.Log("<color=#00FF99><b>[APEX V12] Successfully reloaded and refreshed Barriers, Curbs, and Markings meshes in scene! Turn 1 is now wide open.</b></color>");
        }
    }
}
#endif
