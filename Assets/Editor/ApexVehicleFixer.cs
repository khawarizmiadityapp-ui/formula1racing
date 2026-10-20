using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
public static class ApexVehicleFixer
{
    private const string BaseDir = "Assets/RacingV12Pack/UnityAssets/RacingV12Pack";
    private const string ModelsDir = BaseDir + "/Models";
    private const string MaterialsDir = BaseDir + "/Materials";
    private const string PrefabsDir = BaseDir + "/Prefabs";
    private const string AudioDir = BaseDir + "/Audio";
    private const string PrefabPath = PrefabsDir + "/PlayerCar_Mercedes_F1.prefab";

    static ApexVehicleFixer()
    {
        EditorApplication.delayCall += () =>
        {
            if (!SessionState.GetBool("APEX_V12_VehicleFixed", false))
            {
                SessionState.SetBool("APEX_V12_VehicleFixed", true);
                RebuildPlayerCarPrefab();
                UpdateScenePlayerCar();
            }
        };
    }

    [MenuItem("APEX V12/Update Scene PlayerCar Instance")]
    public static void UpdateScenePlayerCar()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            prefab = RebuildPlayerCarPrefab();
        }

        GameObject existing = GameObject.Find("PlayerCar");
        Vector3 spawnPos = new Vector3(-340f, 0.45f, -500f);
        Quaternion spawnRot = Quaternion.Euler(0f, 90f, 0f);

        if (existing != null)
        {
            spawnPos = existing.transform.position;
            spawnRot = existing.transform.rotation;
            Undo.DestroyObjectImmediate(existing);
        }

        GameObject newCar = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        newCar.name = "PlayerCar";
        newCar.transform.position = spawnPos;
        newCar.transform.rotation = spawnRot;
        Undo.RegisterCreatedObjectUndo(newCar, "Update PlayerCar");

        // Save active scene
        if (EditorSceneManager.GetActiveScene().IsValid())
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        }
        Debug.Log("<color=#00D2BE>[APEX V12] PlayerCar scene instance updated cleanly!</color>");
    }

    [MenuItem("APEX V12/Repair Vehicle and Prefab Materials")]
    public static GameObject RebuildPlayerCarPrefab()
    {
        Debug.Log("<color=#00D2BE><b>[APEX V12] Rebuilding PlayerCar prefab with correct 1:1 materials...</b></color>");

        // Load all required materials
        var mats = LoadMaterialDictionary();

        // Create temporary root for car
        GameObject carRoot = new GameObject("PlayerCar_Mercedes_F1");
        carRoot.tag = "Player";
        carRoot.layer = LayerMask.NameToLayer("Default");

        // Rigidbody
        Rigidbody rb = carRoot.AddComponent<Rigidbody>();
        rb.mass = 780f;
        rb.linearDamping = 0.05f;
        rb.angularDamping = 2.5f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        // Base Colliders (Chassis floor, nose, and sidepods)
        BoxCollider floorCol = carRoot.AddComponent<BoxCollider>();
        floorCol.center = new Vector3(0f, 0.35f, 0f);
        floorCol.size = new Vector3(1.40f, 0.30f, 4.40f);

        BoxCollider frontWingCol = carRoot.AddComponent<BoxCollider>();
        frontWingCol.center = new Vector3(0f, 0.32f, 2.15f);
        frontWingCol.size = new Vector3(1.80f, 0.18f, 0.80f);

        BoxCollider rearWingCol = carRoot.AddComponent<BoxCollider>();
        rearWingCol.center = new Vector3(0f, 0.72f, -1.95f);
        rearWingCol.size = new Vector3(1.50f, 0.35f, 0.70f);

        // Movement & audio components
        FormulaCarArcadeController controller = carRoot.AddComponent<FormulaCarArcadeController>();
        FormulaWheelVisuals wheelVisuals = carRoot.AddComponent<FormulaWheelVisuals>();
        V12EngineAudio engineAudio = carRoot.AddComponent<V12EngineAudio>();
        WireAudioClips(engineAudio);

        // Instantiate Chassis
        GameObject chassisPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelsDir}/Mercedes_AMG_F1_Inspired_Car.obj");
        if (chassisPrefab != null)
        {
            GameObject chassis = Object.Instantiate(chassisPrefab, carRoot.transform);
            chassis.name = "Chassis";
            chassis.transform.localPosition = Vector3.zero;
            chassis.transform.localRotation = Quaternion.identity;
            chassis.transform.localScale = Vector3.one;

            // Apply exact 1:1 material to each child by name
            AssignChildrenMaterials(chassis, mats);
        }

        // Instantiate Livery Details
        GameObject liveryPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelsDir}/Mercedes_F1_Livery_Details.obj");
        if (liveryPrefab != null)
        {
            GameObject livery = Object.Instantiate(liveryPrefab, carRoot.transform);
            livery.name = "LiveryDetails";
            livery.transform.localPosition = Vector3.zero;
            livery.transform.localRotation = Quaternion.identity;
            livery.transform.localScale = Vector3.one;

            // Apply exact 1:1 material to each child by name
            AssignChildrenMaterials(livery, mats);
        }

        // Instantiate Four Wheels
        GameObject wheelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelsDir}/Mercedes_F1_Wheel_Assembly.obj");
        if (wheelPrefab != null)
        {
            PhysicsMaterial tirePhysMat = new PhysicsMaterial("F1_TireMat")
            {
                dynamicFriction = 0.08f,
                staticFriction = 0.12f,
                bounciness = 0.0f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };

            // Front Left (-1.02, 0.40, 1.46)
            GameObject fl = Object.Instantiate(wheelPrefab, carRoot.transform);
            fl.name = "FrontLeft";
            fl.transform.localPosition = new Vector3(-1.02f, 0.40f, 1.46f);
            fl.transform.localRotation = Quaternion.identity;
            fl.transform.localScale = Vector3.one;
            AssignChildrenMaterials(fl, mats);
            AddWheelCollider(fl, tirePhysMat);

            // Front Right (1.02, 0.40, 1.46)
            GameObject fr = Object.Instantiate(wheelPrefab, carRoot.transform);
            fr.name = "FrontRight";
            fr.transform.localPosition = new Vector3(1.02f, 0.40f, 1.46f);
            fr.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            fr.transform.localScale = Vector3.one;
            AssignChildrenMaterials(fr, mats);
            AddWheelCollider(fr, tirePhysMat);

            // Rear Left (-1.02, 0.40, -1.48)
            GameObject rl = Object.Instantiate(wheelPrefab, carRoot.transform);
            rl.name = "RearLeft";
            rl.transform.localPosition = new Vector3(-1.02f, 0.40f, -1.48f);
            rl.transform.localRotation = Quaternion.identity;
            rl.transform.localScale = Vector3.one;
            AssignChildrenMaterials(rl, mats);
            AddWheelCollider(rl, tirePhysMat);

            // Rear Right (1.02, 0.40, -1.48)
            GameObject rr = Object.Instantiate(wheelPrefab, carRoot.transform);
            rr.name = "RearRight";
            rr.transform.localPosition = new Vector3(1.02f, 0.40f, -1.48f);
            rr.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            rr.transform.localScale = Vector3.one;
            AssignChildrenMaterials(rr, mats);
            AddWheelCollider(rr, tirePhysMat);
        }

        // Save cleanly as prefab
        if (!Directory.Exists(PrefabsDir)) Directory.CreateDirectory(PrefabsDir);
        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(carRoot, PrefabPath);
        Object.DestroyImmediate(carRoot);

        Debug.Log($"<color=#00D2BE>[APEX V12] Successfully created PlayerCar prefab at: {PrefabPath}</color>");
        return savedPrefab;
    }

    private static void AssignChildrenMaterials(GameObject root, Dictionary<string, Material> mats)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        foreach (var rend in renderers)
        {
            string childName = rend.gameObject.name.Trim();
            if (mats.TryGetValue(childName, out Material mat))
            {
                rend.sharedMaterials = new Material[] { mat };
            }
            else
            {
                // Fallback by partial matching
                bool matched = false;
                foreach (var pair in mats)
                {
                    if (childName.IndexOf(pair.Key, System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        pair.Key.IndexOf(childName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        rend.sharedMaterials = new Material[] { pair.Value };
                        matched = true;
                        break;
                    }
                }
                if (!matched)
                {
                    Debug.LogWarning($"[APEX V12] No matching material found for child '{childName}' on '{root.name}'");
                }
            }
        }
    }

    private static Dictionary<string, Material> LoadMaterialDictionary()
    {
        var dict = new Dictionary<string, Material>(System.StringComparer.OrdinalIgnoreCase);
        string[] matGuids = AssetDatabase.FindAssets("t:Material", new[] { MaterialsDir });
        foreach (string guid in matGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null)
            {
                dict[mat.name] = mat;
            }
        }
        return dict;
    }

    private static void AddWheelCollider(GameObject wheelGo, PhysicsMaterial mat)
    {
        SphereCollider sc = wheelGo.GetComponent<SphereCollider>();
        if (sc == null) sc = wheelGo.AddComponent<SphereCollider>();
        sc.radius = 0.395f;
        sc.center = Vector3.zero;
        sc.sharedMaterial = mat;
    }

    private static void WireAudioClips(V12EngineAudio audioComp)
    {
        var so = new SerializedObject(audioComp);
        SetClipProp(so, "idleClip", "V12_Engine_Idle_Loop.wav");
        SetClipProp(so, "midClip", "V12_Engine_Mid_Loop.wav");
        SetClipProp(so, "highClip", "V12_Engine_High_Loop.wav");
        SetClipProp(so, "gearShiftClip", "Gear_Shift_Click.wav");
        SetClipProp(so, "turboWastegateClip", "Turbo_Wastegate_Hiss.wav");
        SetClipProp(so, "tireScrubClip", "Tire_Scrub_Squeal.wav");
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetClipProp(SerializedObject so, string propName, string filename)
    {
        SerializedProperty prop = so.FindProperty(propName);
        if (prop != null)
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioDir}/{filename}");
            if (clip != null) prop.objectReferenceValue = clip;
        }
    }
}
