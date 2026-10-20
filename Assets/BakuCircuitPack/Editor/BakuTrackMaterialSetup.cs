using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
public static class BakuTrackMaterialSetup
{
    private const string MaterialsDir = "Assets/BakuCircuitPack/Materials";
    private const string TexturesDir = "Assets/RacingV12Pack/UnityAssets/RacingV12Pack/Textures";

    static BakuTrackMaterialSetup()
    {
        EditorApplication.delayCall += () =>
        {
            if (!SessionState.GetBool("APEX_V12_TrackMatsApplied", false))
            {
                SessionState.SetBool("APEX_V12_TrackMatsApplied", true);
                BuildAndApplyMaterials();
            }
        };
    }

    [MenuItem("APEX V12/Build and Apply Baku Circuit PBR Materials")]
    public static void BuildAndApplyMaterials()
    {
        Debug.Log("<color=#00D2BE><b>[Baku Circuit] Generating high-fidelity PBR materials and binding to circuit...</b></color>");

        if (!Directory.Exists(MaterialsDir)) Directory.CreateDirectory(MaterialsDir);

        Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
        if (litShader == null) litShader = Shader.Find("Standard");

        var mats = new Dictionary<string, Material>(System.StringComparer.OrdinalIgnoreCase);

        // Load textures
        Texture2D asphaltAlbedo = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesDir}/Tex_Asphalt_Albedo.png");
        Texture2D asphaltNormal = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesDir}/Tex_Asphalt_Normal.png");
        Texture2D curbNormal = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesDir}/Tex_Curb_Normal.png");
        Texture2D castleStone = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesDir}/Tex_Castle_Stone.png");
        Texture2D sponsorBaku = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesDir}/Tex_Sponsor_Baku.png");
        Texture2D grassAlbedo = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesDir}/Tex_Grass_Albedo.png");

        // 1. Asphalt (Street road surface)
        mats["Asphalt"] = CreateOrGetMat("Asphalt", litShader, new Color(0.18f, 0.18f, 0.19f, 1f), 0.32f, 0.05f, asphaltAlbedo, asphaltNormal, new Vector2(30f, 30f));

        // 2. Curbs (Red & White alternating street curbs)
        mats["CurbRed"] = CreateOrGetMat("CurbRed", litShader, new Color(0.85f, 0.06f, 0.05f, 1f), 0.58f, 0.08f, null, curbNormal, new Vector2(10f, 2f));
        mats["CurbWhite"] = CreateOrGetMat("CurbWhite", litShader, new Color(0.95f, 0.95f, 0.94f, 1f), 0.58f, 0.08f, null, curbNormal, new Vector2(10f, 2f));

        // 3. Markings (Starting grid, start/finish checkerboard, pit yellow lines)
        mats["RoadWhite"] = CreateOrGetMat("RoadWhite", litShader, new Color(0.98f, 0.98f, 0.98f, 1f), 0.45f, 0.02f, null, null);
        mats["RoadYellow"] = CreateOrGetMat("RoadYellow", litShader, new Color(0.98f, 0.78f, 0.08f, 1f), 0.45f, 0.02f, null, null);
        mats["PaintBlack"] = CreateOrGetMat("PaintBlack", litShader, new Color(0.04f, 0.04f, 0.04f, 1f), 0.40f, 0.02f, null, null);

        // 4. Barriers (Reinforced concrete safety walls, Baku teal sponsors, catch fence)
        mats["Concrete"] = CreateOrGetMat("Concrete", litShader, new Color(0.68f, 0.68f, 0.66f, 1f), 0.22f, 0.02f, null, null);
        mats["BarrierTeal"] = CreateOrGetMat("BarrierTeal", litShader, new Color(0.00f, 0.68f, 0.62f, 1f), 0.52f, 0.10f, sponsorBaku, null, new Vector2(6f, 1f));
        mats["BarrierWhite"] = CreateOrGetMat("BarrierWhite", litShader, new Color(0.95f, 0.95f, 0.95f, 1f), 0.52f, 0.05f, sponsorBaku, null, new Vector2(6f, 1f));
        mats["BarrierDark"] = CreateOrGetMat("BarrierDark", litShader, new Color(0.12f, 0.13f, 0.15f, 1f), 0.35f, 0.75f, null, null);

        // 5. Environment (Old City sandstone walls, terracotta roofs, Caspian towers)
        mats["BuildingSand"] = CreateOrGetMat("BuildingSand", litShader, new Color(0.78f, 0.70f, 0.55f, 1f), 0.18f, 0.02f, castleStone, null, new Vector2(4f, 4f));
        mats["TowerStone"] = CreateOrGetMat("TowerStone", litShader, new Color(0.72f, 0.65f, 0.52f, 1f), 0.18f, 0.02f, castleStone, null, new Vector2(3f, 3f));
        mats["TowerRoof"] = CreateOrGetMat("TowerRoof", litShader, new Color(0.62f, 0.24f, 0.16f, 1f), 0.35f, 0.05f, null, null);
        mats["RoofRed"] = CreateOrGetMat("RoofRed", litShader, new Color(0.60f, 0.20f, 0.14f, 1f), 0.35f, 0.05f, null, null);
        mats["Roof"] = CreateOrGetMat("Roof", litShader, new Color(0.20f, 0.22f, 0.25f, 1f), 0.40f, 0.15f, null, null);
        mats["BuildingDark"] = CreateOrGetMat("BuildingDark", litShader, new Color(0.16f, 0.18f, 0.22f, 1f), 0.65f, 0.45f, null, null);
        mats["BuildingLight"] = CreateOrGetMat("BuildingLight", litShader, new Color(0.85f, 0.86f, 0.88f, 1f), 0.55f, 0.10f, null, null);
        mats["BuildingGlass"] = CreateOrGetMat("BuildingGlass", litShader, new Color(0.12f, 0.32f, 0.52f, 1f), 0.92f, 0.85f, null, null);

        // 6. Grandstands & Floodlights
        mats["Grandstand"] = CreateOrGetMat("Grandstand", litShader, new Color(0.42f, 0.44f, 0.48f, 1f), 0.38f, 0.25f, null, null);
        mats["SeatRed"] = CreateOrGetMat("SeatRed", litShader, new Color(0.82f, 0.08f, 0.08f, 1f), 0.45f, 0.05f, null, null);
        mats["SeatTeal"] = CreateOrGetMat("SeatTeal", litShader, new Color(0.00f, 0.65f, 0.65f, 1f), 0.45f, 0.05f, null, null);
        mats["FloodLight"] = CreateOrGetMat("FloodLight", litShader, new Color(0.25f, 0.26f, 0.28f, 1f), 0.45f, 0.60f, null, null);
        mats["LampGlow"] = CreateOrGetEmissiveMat("LampGlow", litShader, new Color(1.0f, 0.92f, 0.80f, 1f), new Color(2.5f, 2.3f, 1.8f, 1f));

        // 7. Waterfront & Promenade
        mats["Water"] = CreateOrGetMat("Water", litShader, new Color(0.04f, 0.22f, 0.36f, 1f), 0.96f, 0.12f, null, null);
        mats["Promenade"] = CreateOrGetMat("Promenade", litShader, new Color(0.55f, 0.53f, 0.50f, 1f), 0.32f, 0.04f, null, null);
        mats["Land"] = CreateOrGetMat("Land", litShader, new Color(0.28f, 0.38f, 0.22f, 1f), 0.20f, 0.02f, grassAlbedo, null, new Vector2(40f, 40f));
        mats["PitBlue"] = CreateOrGetMat("PitBlue", litShader, new Color(0.05f, 0.35f, 0.75f, 1f), 0.55f, 0.15f, null, null);

        AssetDatabase.SaveAssets();

        // Bind materials to APEX_BakuCircuit_Generated in active scene
        ApplyMaterialsToCircuit(mats);

        Debug.Log("<color=#00D2BE>[Baku Circuit] All 27 PBR materials created and applied successfully!</color>");
    }

    private static void ApplyMaterialsToCircuit(Dictionary<string, Material> mats)
    {
        GameObject circuit = GameObject.Find("APEX_BakuCircuit_Generated");
        if (circuit == null)
        {
            Debug.LogWarning("[Baku Circuit] 'APEX_BakuCircuit_Generated' not found in active scene.");
            return;
        }

        Undo.RecordObject(circuit, "Apply Baku Circuit Materials");

        // Road: Asphalt
        AssignChildMaterials(circuit.transform, "Road", new[] { mats["Asphalt"] });

        // Curbs: CurbRed, CurbWhite
        AssignChildMaterials(circuit.transform, "Curbs", new[] { mats["CurbRed"], mats["CurbWhite"] });

        // RoadMarkings: RoadWhite, PaintBlack, RoadYellow
        AssignChildMaterials(circuit.transform, "RoadMarkings", new[] { mats["RoadWhite"], mats["PaintBlack"], mats["RoadYellow"] });

        // Barriers: Concrete, BarrierDark, BarrierTeal, BarrierWhite
        AssignChildMaterials(circuit.transform, "Barriers", new[] { mats["Concrete"], mats["BarrierDark"], mats["BarrierTeal"], mats["BarrierWhite"] });

        // Environment: 14 submeshes
        AssignChildMaterials(circuit.transform, "Environment", new[] {
            mats["BuildingDark"],
            mats["BuildingGlass"],
            mats["BuildingLight"],
            mats["Roof"],
            mats["BarrierTeal"],
            mats["Grandstand"],
            mats["SeatTeal"],
            mats["SeatRed"],
            mats["FloodLight"],
            mats["LampGlow"],
            mats["BuildingSand"],
            mats["RoofRed"],
            mats["TowerStone"],
            mats["TowerRoof"]
        });

        // Water: Water
        AssignChildMaterials(circuit.transform, "Water", new[] { mats["Water"] });

        // Promenade: Promenade
        AssignChildMaterials(circuit.transform, "Promenade", new[] { mats["Promenade"] });

        // PitLane: Asphalt
        AssignChildMaterials(circuit.transform, "PitLane", new[] { mats["Asphalt"] });

        // Ground: Land
        AssignChildMaterials(circuit.transform, "Ground", new[] { mats["Land"] });

        // StartFinishGantry: Concrete, BarrierDark, RoadWhite
        AssignChildMaterials(circuit.transform, "StartFinishGantry", new[] { mats["Concrete"], mats["BarrierDark"], mats["RoadWhite"] });

        // Mark scene dirty and save
        if (EditorSceneManager.GetActiveScene().IsValid())
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        }
    }

    private static void AssignChildMaterials(Transform root, string childName, Material[] materials)
    {
        Transform child = root.Find(childName);
        if (child == null) return;

        Renderer[] renderers = child.GetComponentsInChildren<Renderer>(true);
        foreach (var rend in renderers)
        {
            Undo.RecordObject(rend, "Assign Materials");
            rend.sharedMaterials = materials;
        }
    }

    private static Material CreateOrGetMat(string name, Shader shader, Color color, float smoothness, float metallic, Texture2D albedo = null, Texture2D normal = null, Vector2? tiling = null)
    {
        string path = $"{MaterialsDir}/{name}.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }

        mat.shader = shader;
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Smoothness", smoothness);
        mat.SetFloat("_Metallic", metallic);

        if (albedo != null)
        {
            mat.SetTexture("_BaseMap", albedo);
            if (tiling.HasValue) mat.SetTextureScale("_BaseMap", tiling.Value);
        }

        if (normal != null)
        {
            mat.SetTexture("_BumpMap", normal);
            mat.EnableKeyword("_NORMALMAP");
            if (tiling.HasValue) mat.SetTextureScale("_BumpMap", tiling.Value);
        }

        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Material CreateOrGetEmissiveMat(string name, Shader shader, Color baseColor, Color emissionColor)
    {
        string path = $"{MaterialsDir}/{name}.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }

        mat.shader = shader;
        mat.SetColor("_BaseColor", baseColor);
        mat.SetFloat("_Smoothness", 0.8f);
        mat.SetColor("_EmissionColor", emissionColor);
        mat.EnableKeyword("_EMISSION");
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

        EditorUtility.SetDirty(mat);
        return mat;
    }
}
