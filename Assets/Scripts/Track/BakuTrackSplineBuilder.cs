using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Procedural Spline Track & Environment Builder for Baku City Circuit (Azerbaijan),
/// specifically recreating the iconic "Castle Corner" (Turn 8 - Turn 10 around Icherisheher)
/// and full circuit street layout.
/// 
/// Generates:
/// 1. Narrowing street circuit road with uphill elevation (7.6m bottleneck width at Castle apex)
/// 2. Inside Red-White-Yellow FIA kerbs with rumble profile
/// 3. Outside white concrete barriers with sponsor logos & curved high catch-fencing
/// 4. Ancient Icherisheher medieval fortress walls, crenelated cylindrical towers & arched gate
/// 5. Classical Eastern European Baku neoclassical buildings, spectator grandstands & trees
/// 6. Fully drivable with physics MeshColliders for F1 racing
/// 7. Automatically saves persistent mesh assets to disk so they persist across scene saves & Play Mode!
/// </summary>
[ExecuteAlways]
[SelectionBase]
public class BakuTrackSplineBuilder : MonoBehaviour
{
    #region ──── DATA STRUCTURES ────

    [System.Serializable]
    public class TrackWayPoint
    {
        public Vector3 position;
        public float trackWidth = 11.0f; // Track width at this point
        public float bankingAngle = 0f;  // Camber/banking in degrees
        public bool hasInnerKerb = false;
        public bool hasOuterBarrier = true;
        public bool hasCastleWall = false;

        public TrackWayPoint(Vector3 pos, float width, float banking = 0f, bool innerKerb = false, bool outerBarrier = true, bool castleWall = false)
        {
            position = pos;
            trackWidth = width;
            bankingAngle = banking;
            hasInnerKerb = innerKerb;
            hasOuterBarrier = outerBarrier;
            hasCastleWall = castleWall;
        }
    }

    #endregion

    #region ──── INSPECTOR FIELDS ────

    [Header("═══ BAKU CASTLE CORNER WAYPOINTS ═══")]
    [Tooltip("Control points defining the Baku Turn 8 bottleneck section")]
    [SerializeField] private List<TrackWayPoint> waypoints = new List<TrackWayPoint>();

    [Header("═══ SPLINE SETTINGS ═══")]
    [SerializeField] private bool isClosedLoop = true;
    [Range(4, 24)]
    [SerializeField] private int subdivisionsPerSegment = 12;
    [SerializeField] private float roadHeightOffset = 0.06f;

    [Header("═══ ROAD & KERBS ═══")]
    [SerializeField] private Material asphaltMaterial;
    [SerializeField] private Material racingLineMaterial;
    [SerializeField] private Material curbRedMaterial;
    [SerializeField] private Material curbWhiteMaterial;
    [SerializeField] private Material curbYellowMaterial;
    [SerializeField] private float kerbWidth = 0.90f;
    [SerializeField] private float kerbHeight = 0.08f;

    [Header("═══ BARRIERS & CATCH FENCE ═══")]
    [SerializeField] private Material concreteBarrierMaterial;
    [SerializeField] private Material catchFenceMaterial;
    [SerializeField] private Material sponsorBannerMaterial;
    [SerializeField] private float barrierHeight = 1.25f;
    [SerializeField] private float barrierThickness = 0.45f;
    [SerializeField] private float fenceHeight = 3.6f;
    [SerializeField] private float fenceOverhangAngle = 35f;

    [Header("═══ ICHERISHEHER CASTLE FORTRESS ═══")]
    [SerializeField] private Material castleStoneMaterial;
    [SerializeField] private float castleWallHeight = 9.5f;
    [SerializeField] private float castleWallThickness = 2.2f;
    [SerializeField] private float towerRadius = 3.8f;
    [SerializeField] private float towerHeight = 14.5f;
    [SerializeField] private int towerCrenelations = 10;
    public float CastleWallThickness => castleWallThickness;

    [Header("═══ CITY ARCHITECTURE & PROPS ═══")]
    [SerializeField] private Material buildingFacadeMaterial;
    [SerializeField] private Material grandstandMaterial;
    [SerializeField] private Material treeFoliageMaterial;
    [SerializeField] private Material gridWhiteMaterial;
    [SerializeField] private Material gridBlackMaterial;
    [SerializeField] private Material metalMaterial;
    [SerializeField] private Material windowMaterial;
    [SerializeField] private Material buildingBlueMaterial;
    [SerializeField] private Material petronasTealMaterial;
    [SerializeField] private Material ledRedMaterial;
    [SerializeField] private Material ledGreenMaterial;

    [Header("═══ GENERATED OBJECT REFERENCES ═══")]
    [Tooltip("Active root GameObject for the generated Baku F1 Full Circuit.")]
    [SerializeField] private GameObject generatedTrackRoot;
    [Tooltip("Inactive backup of the previous/old circuit layout (kept safely without deleting).")]
    [SerializeField] private GameObject oldTrackBackup;

    public GameObject OldTrackBackup => oldTrackBackup;
    public GameObject GeneratedTrackRoot => generatedTrackRoot;

    #endregion

    #region ──── LIFECYCLE ────

    private bool _isBuilding = false;
    private bool _hasBuiltThisSession = false;

    private void Reset()
    {
        InitializeBakuCastleWaypoints();
    }

    private void OnEnable()
    {
        DeactivateOldOverlappingTrackComponents();
        if (!_hasBuiltThisSession)
        {
            EnsureTrackBuilt();
        }
        PositionPlayerCarAtPole();
    }

    private void Awake()
    {
        DeactivateOldOverlappingTrackComponents();
        PositionPlayerCarAtPole();
    }

    private void Start()
    {
        DeactivateOldOverlappingTrackComponents();
        PositionPlayerCarAtPole();
    }

    public void EnsureTrackBuilt()
    {
        // Always deactivate old overlapping scene tracks/assets on check
        DeactivateOldOverlappingTrackComponents();

        // Guard against re-entrant builds (e.g. during Undo/Redo callbacks)
        if (_isBuilding) return;

        bool needsBuild = false;

        // If generatedTrackRoot points to the old track, preserve it as inactive backup
        if (generatedTrackRoot != null && (generatedTrackRoot.name.Contains("CastleCorner") || generatedTrackRoot.name.StartsWith("[OLD")))
        {
            oldTrackBackup = generatedTrackRoot;
            oldTrackBackup.SetActive(false);
            generatedTrackRoot = null;
            needsBuild = true;
        }

        if (generatedTrackRoot == null)
        {
            Transform existingF1 = transform.Find("Baku_F1_FullCircuit_Track");
            if (existingF1 != null)
            {
                generatedTrackRoot = existingF1.gameObject;
            }
            else
            {
                needsBuild = true;
            }
        }

        if (generatedTrackRoot != null && generatedTrackRoot.transform.childCount == 0)
        {
            needsBuild = true;
        }
        else if (generatedTrackRoot != null)
        {
            // Verify if any MeshFilter has lost its mesh
            var filters = generatedTrackRoot.GetComponentsInChildren<MeshFilter>(true);
            if (filters.Length == 0)
            {
                needsBuild = true;
            }
            else
            {
                foreach (var mf in filters)
                {
                    if (mf.sharedMesh == null)
                    {
                        needsBuild = true;
                        break;
                    }
                }
            }
        }

        // Automatically upgrade to full 34-waypoint Baku circuit (690m straight) if old layout is detected
        if (waypoints == null || waypoints.Count < 30)
        {
            InitializeBakuCastleWaypoints();
            needsBuild = true;
        }

        if (needsBuild)
        {
            BuildBakuTrack();
        }

        _hasBuiltThisSession = true;

        PositionPlayerCarAtPole();
    }

    /// <summary>
    /// Positions PlayerCar squarely inside Starting Grid Box P1 (Pole Position).
    /// Grid Slot P1 is at X = +2.2m, Z = -208.0m facing +Z down the 690m DRS straight toward Start/Finish line & Turn 1.
    /// </summary>
    public void PositionPlayerCarAtPole()
    {
        if (!gameObject.activeInHierarchy) return;
        GameObject playerCar = GameObject.Find("PlayerCar");
        if (playerCar == null) return;

        Vector3 polePos = new Vector3(2.2f, 0.12f, -208.0f);
        playerCar.transform.position = polePos;
        playerCar.transform.rotation = Quaternion.identity;

        var rb = playerCar.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        var arcadeCtrl = playerCar.GetComponent<FormulaCarArcadeController>();
        if (arcadeCtrl != null)
        {
            arcadeCtrl.SetSpawnPoint(polePos, Vector3.zero);
        }

#if UNITY_EDITOR
        // Ensure soft red tire material on wheels
        Material softMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/RacingV12Pack/UnityAssets/RacingV12Pack/Materials/TireRubber_SoftRed.mat");
        if (softMat != null)
        {
            foreach (var r in playerCar.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int m = 0; m < mats.Length; m++)
                {
                    if (mats[m] != null && mats[m].name.Contains("TireRubber")) mats[m] = softMat;
                }
                r.sharedMaterials = mats;
            }
        }

        // Ensure Racing HUD exists and is wired
        var existingHud = Object.FindAnyObjectByType<RacingHUD>();
        if (existingHud == null)
        {
            var setupType = System.Type.GetType("ApexV12Setup, Assembly-CSharp-Editor");
            if (setupType != null)
            {
                var buildMethod = setupType.GetMethod("BuildRacingHUD", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                buildMethod?.Invoke(null, new object[] { playerCar });
            }
        }
#endif
    }

    #endregion

    #region ──── DEFAULT BAKU CIRCUIT LAYOUT ────

    /// <summary>
    /// Populates waypoints recreating the full, authentic Baku City Circuit:
    /// - 690m Waterfront DRS Straight (Neftchilar Avenue) reaching 360-370 km/h
    /// - Start/Finish line & Starting Grid at Z = -200
    /// - Turn 1 & Turn 2 (Government House / Azadliq Square 90-degree left complex)
    /// - Pushkin & Khagani street boulevard sweeps (Turns 3-6)
    /// - Turn 7 right turn onto Aziz Aliyev Street uphill approach
    /// - The iconic Turn 8 Castle Corner bottleneck (7.6m width, +8% climb past medieval fortress tower)
    /// - Turn 9 & 10 fortress rampart S-curves
    /// - Maiden Tower / Kichik Qala descent (Turns 11-12)
    /// - Coastal link (Turns 13-15) and final sweeping Turn 16 onto the main straight
    /// </summary>
    public void InitializeBakuCastleWaypoints()
    {
        waypoints.Clear();

        // ═══ 1. WATERFRONT DRS MAIN STRAIGHT (NEFTCHILAR AVENUE) — 690 METERS FLAT-OUT ═══
        // Straight runs from Z = -350 to Z = +340 along X = 0 at full 15.0m width.
        // Start / Finish line & Pole Position are at Z = -200.
        waypoints.Add(new TrackWayPoint(new Vector3(0f, 0f, -350f), 15.0f, 0f, false, true, false));  // wp 0: Exit of final sweeper
        waypoints.Add(new TrackWayPoint(new Vector3(0f, 0f, -270f), 15.0f, 0f, false, true, false));  // wp 1: Starting grid rear
        waypoints.Add(new TrackWayPoint(new Vector3(0f, 0f, -200f), 15.0f, 0f, false, true, false));  // wp 2: START / FINISH LINE & Pole Position
        waypoints.Add(new TrackWayPoint(new Vector3(0f, 0f, -90f), 15.0f, 0f, false, true, false));   // wp 3: Mid-straight past pit garages
        waypoints.Add(new TrackWayPoint(new Vector3(0f, 0f, 30f), 15.0f, 0f, false, true, false));    // wp 4: Speed ~310 km/h
        waypoints.Add(new TrackWayPoint(new Vector3(0f, 0f, 150f), 15.0f, 0f, false, true, false));   // wp 5: Speed ~345 km/h
        waypoints.Add(new TrackWayPoint(new Vector3(0f, 0f, 260f), 15.0f, 0f, false, true, false));   // wp 6: Maximum speed ~365-370 km/h
        waypoints.Add(new TrackWayPoint(new Vector3(0f, 0f, 340f), 15.0f, 0f, false, true, false));   // wp 7: Turn 1 heavy braking zone (360 -> 120 km/h)

        // ═══ 2. TURN 1 & TURN 2 (AZADLIQ SQUARE / GOVERNMENT HOUSE COMPLEX) ═══
        waypoints.Add(new TrackWayPoint(new Vector3(-15f, 0f, 385f), 14.0f, 0f, true, true, false));  // wp 8: Turn 1 90-degree left apex
        waypoints.Add(new TrackWayPoint(new Vector3(-55f, 0f, 400f), 13.5f, 0f, false, true, false)); // wp 9: Azadliq Square wide transition
        waypoints.Add(new TrackWayPoint(new Vector3(-100f, 0f, 375f), 13.0f, 0f, true, true, false)); // wp 10: Turn 2 90-degree left apex
        waypoints.Add(new TrackWayPoint(new Vector3(-115f, 0f, 300f), 12.5f, 0f, false, true, false)); // wp 11: Pushkin Street acceleration

        // ═══ 3. TURNS 3 & 4 (KHAGANI STREET BOULEVARD SWEEPS) ═══
        waypoints.Add(new TrackWayPoint(new Vector3(-110f, 0f, 210f), 12.0f, 0f, true, true, false)); // wp 12: Turn 3 right flick
        waypoints.Add(new TrackWayPoint(new Vector3(-80f, 0f, 140f), 12.0f, 0f, true, true, false));  // wp 13: Turn 4 left flick
        waypoints.Add(new TrackWayPoint(new Vector3(-88f, 0f, 75f), 12.0f, 0f, false, true, false));  // wp 14: Short straight

        // ═══ 4. TURNS 5 & 6 (APPROACH TO OLD CITY) ═══
        waypoints.Add(new TrackWayPoint(new Vector3(-75f, 0f, 15f), 12.0f, 0f, true, true, false));   // wp 15: Turn 5 apex
        waypoints.Add(new TrackWayPoint(new Vector3(-52f, 0f, -25f), 12.0f, 0f, false, true, false)); // wp 16: Turn 6 transition

        // ═══ 5. TURN 7 & AZIZ ALIYEV UPHILL CLIMB ═══
        waypoints.Add(new TrackWayPoint(new Vector3(-36f, 0.2f, 10f), 11.5f, 0f, true, true, false));  // wp 17: Turn 7 right turn into Aziz Aliyev
        waypoints.Add(new TrackWayPoint(new Vector3(-28f, 1.2f, 50f), 10.5f, 0f, false, true, false)); // wp 18: Uphill climb begins (+4% grade)
        waypoints.Add(new TrackWayPoint(new Vector3(-25f, 2.6f, 85f), 9.2f, 0.5f, false, true, true)); // wp 19: Street narrows between stone walls

        // ═══ 6. TURN 8 — THE ICONIC CASTLE CORNER BOTTLENECK (ICHERISHEHER) ═══
        // Narrowest section in Formula 1: only 7.6m width, climbing uphill right past the medieval tower
        waypoints.Add(new TrackWayPoint(new Vector3(-23f, 4.0f, 112f), 7.6f, 2.0f, true, true, true)); // wp 20: Turn 8 apex against cylindrical tower
        waypoints.Add(new TrackWayPoint(new Vector3(-28f, 5.2f, 130f), 7.6f, 2.5f, true, true, true)); // wp 21: Steep climb past ancient fortress gate

        // ═══ 7. TURNS 9 & 10 (FORTRESS RAMPARTS S-CURVE) ═══
        waypoints.Add(new TrackWayPoint(new Vector3(-38f, 6.0f, 142f), 8.2f, 1.5f, true, true, true)); // wp 22: Turn 9 bastion apex
        waypoints.Add(new TrackWayPoint(new Vector3(-54f, 6.4f, 140f), 9.0f, 1.0f, true, true, true)); // wp 23: Turn 10 crest
        waypoints.Add(new TrackWayPoint(new Vector3(-70f, 6.0f, 128f), 10.0f, 0f, false, true, true));  // wp 24: Castle ramparts exit

        // ═══ 8. TURNS 11 & 12 (MAIDEN TOWER DESCENT TO COAST) ═══
        waypoints.Add(new TrackWayPoint(new Vector3(-90f, 4.8f, 100f), 11.0f, 0f, false, true, false)); // wp 25: Steep descent begins
        waypoints.Add(new TrackWayPoint(new Vector3(-108f, 3.2f, 50f), 12.0f, 0f, false, true, false)); // wp 26: Passing historical quarter
        waypoints.Add(new TrackWayPoint(new Vector3(-115f, 1.6f, -10f), 12.5f, 0f, false, true, false)); // wp 27: Downhill acceleration
        waypoints.Add(new TrackWayPoint(new Vector3(-112f, 0.4f, -75f), 13.0f, 0f, false, true, false)); // wp 28: Coastal elevation reached

        // ═══ 9. TURNS 13-15 (AZADLIQ LINK TO WATERFRONT) ═══
        waypoints.Add(new TrackWayPoint(new Vector3(-95f, 0.0f, -145f), 13.5f, 0f, true, true, false)); // wp 29: Turn 13 apex
        waypoints.Add(new TrackWayPoint(new Vector3(-65f, 0.0f, -215f), 14.0f, 0f, false, true, false)); // wp 30: High-speed sweep
        waypoints.Add(new TrackWayPoint(new Vector3(-40f, 0.0f, -280f), 14.0f, 0f, true, true, false));  // wp 31: Turn 15 entry

        // ═══ 10. TURN 16 (FINAL ACCELERATION SWEEPER ONTO DRS MAIN STRAIGHT) ═══
        waypoints.Add(new TrackWayPoint(new Vector3(-18f, 0.0f, -325f), 14.5f, 0f, true, true, false)); // wp 32: Turn 16 apex
        waypoints.Add(new TrackWayPoint(new Vector3(-5f, 0.0f, -345f), 15.0f, 0f, false, true, false));  // wp 33: Straightening onto 690m straight
    }

    #endregion

    #region ──── SPLINE MATH ────

    public struct SplineSample
    {
        public Vector3 position;
        public Vector3 forward;
        public Vector3 right;
        public Vector3 up;
        public float width;
        public float banking;
        public bool innerKerb;
        public bool outerBarrier;
        public bool castleWall;
        public float distanceAlongTrack;
    }

    public List<SplineSample> GenerateSplineSamples()
    {
        var samples = new List<SplineSample>();
        if (waypoints == null || waypoints.Count < 3) return samples;

        float accumulatedDist = 0f;
        Vector3 prevPos = waypoints[0].position;

        int segmentCount = isClosedLoop ? waypoints.Count : (waypoints.Count - 1);

        for (int i = 0; i < segmentCount; i++)
        {
            int i0 = isClosedLoop ? ((i - 1 + waypoints.Count) % waypoints.Count) : (i > 0 ? i - 1 : i);
            int i1 = i;
            int i2 = (i + 1) % waypoints.Count;
            int i3 = isClosedLoop ? ((i + 2) % waypoints.Count) : ((i + 2 < waypoints.Count) ? i + 2 : i2);

            Vector3 p0 = waypoints[i0].position;
            Vector3 p1 = waypoints[i1].position;
            Vector3 p2 = waypoints[i2].position;
            Vector3 p3 = waypoints[i3].position;

            TrackWayPoint wpA = waypoints[i1];
            TrackWayPoint wpB = waypoints[i2];

            for (int s = 0; s < subdivisionsPerSegment; s++)
            {
                float t = (float)s / subdivisionsPerSegment;
                Vector3 pos = EvaluateCatmullRom(p0, p1, p2, p3, t);
                Vector3 tangent = EvaluateCatmullRomDerivative(p0, p1, p2, p3, t).normalized;
                if (tangent == Vector3.zero) tangent = Vector3.forward;

                float width = Mathf.Lerp(wpA.trackWidth, wpB.trackWidth, t);
                float banking = Mathf.Lerp(wpA.bankingAngle, wpB.bankingAngle, t);
                bool innerKerb = t < 0.5f ? wpA.hasInnerKerb : wpB.hasInnerKerb;
                bool outerBarrier = t < 0.5f ? wpA.hasOuterBarrier : wpB.hasOuterBarrier;
                bool castleWall = t < 0.5f ? wpA.hasCastleWall : wpB.hasCastleWall;

                // Banking rotation around tangent
                Quaternion bankRot = Quaternion.AngleAxis(banking, tangent);
                Vector3 baseUp = Vector3.up;
                Vector3 right = bankRot * Vector3.Cross(baseUp, tangent).normalized;
                Vector3 up = Vector3.Cross(tangent, right).normalized;

                accumulatedDist += Vector3.Distance(prevPos, pos);
                prevPos = pos;

                samples.Add(new SplineSample
                {
                    position = pos,
                    forward = tangent,
                    right = right,
                    up = up,
                    width = width,
                    banking = banking,
                    innerKerb = innerKerb,
                    outerBarrier = outerBarrier,
                    castleWall = castleWall,
                    distanceAlongTrack = accumulatedDist
                });
            }
        }

        if (!isClosedLoop)
        {
            var lastWp = waypoints[waypoints.Count - 1];
            Vector3 lastTangent = (lastWp.position - waypoints[waypoints.Count - 2].position).normalized;
            Vector3 lastRight = Vector3.Cross(Vector3.up, lastTangent).normalized;
            samples.Add(new SplineSample
            {
                position = lastWp.position,
                forward = lastTangent,
                right = lastRight,
                up = Vector3.up,
                width = lastWp.trackWidth,
                banking = lastWp.bankingAngle,
                innerKerb = lastWp.hasInnerKerb,
                outerBarrier = lastWp.hasOuterBarrier,
                castleWall = lastWp.hasCastleWall,
                distanceAlongTrack = accumulatedDist + Vector3.Distance(prevPos, lastWp.position)
            });
        }

        return samples;
    }

    private static Vector3 EvaluateCatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return 0.5f * (
            (2.0f * p1) +
            (-p0 + p2) * t +
            (2.0f * p0 - 5.0f * p1 + 4.0f * p2 - p3) * t2 +
            (-p0 + 3.0f * p1 - 3.0f * p2 + p3) * t3
        );
    }

    private static Vector3 EvaluateCatmullRomDerivative(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        return 0.5f * (
            (-p0 + p2) +
            2.0f * (2.0f * p0 - 5.0f * p1 + 4.0f * p2 - p3) * t +
            3.0f * (-p0 + 3.0f * p1 - 3.0f * p2 + p3) * t2
        );
    }

    #endregion

    #region ──── MESH ASSET PERSISTENCE ────

    private Mesh SaveOrUpdateMeshAsset(Mesh mesh, string assetName)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            string folder = "Assets/RacingV12Pack/UnityAssets/RacingV12Pack/Models/Generated";
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            string path = $"{folder}/{assetName}.asset";
            Mesh existing = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                existing.Clear();
                UnityEditor.EditorUtility.CopySerialized(mesh, existing);
                UnityEditor.AssetDatabase.SaveAssets();
                return existing;
            }
            else
            {
                UnityEditor.AssetDatabase.CreateAsset(mesh, path);
                UnityEditor.AssetDatabase.SaveAssets();
                return mesh;
            }
        }
#endif
        return mesh;
    }

    #endregion

    #region ──── GENERATION ORCHESTRATOR ────

    /// <summary>
    /// Safely deactivates old overlapping track components without deleting or destroying any objects or assets.
    /// Preserves existing Baku_CastleCorner_Track as an inactive backup.
    /// </summary>
    public void DeactivateOldOverlappingTrackComponents()
    {
        // 1. If generatedTrackRoot points to an old track (not Baku_F1_FullCircuit_Track), deactivate and rename it safely
        if (generatedTrackRoot != null && generatedTrackRoot.name != "Baku_F1_FullCircuit_Track")
        {
            generatedTrackRoot.SetActive(false);
            if (!generatedTrackRoot.name.StartsWith("[OLD_INACTIVE_BACKUP]"))
            {
                generatedTrackRoot.name = "[OLD_INACTIVE_BACKUP]_" + generatedTrackRoot.name;
            }
            oldTrackBackup = generatedTrackRoot;
        }

        // 2. Scan all children of this SplineBuilder transform and deactivate any old overlapping circuit components
        //    Also deactivate any duplicate Baku_F1_FullCircuit_Track children beyond the first active one
        int activeF1TrackCount = 0;
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child.name.Contains("CastleCorner") || child.name.Contains("OldTrack") || child.name.StartsWith("[OLD"))
            {
                child.gameObject.SetActive(false);
                if (oldTrackBackup == null) oldTrackBackup = child.gameObject;
            }
            // Catch duplicate F1 tracks (from multiple builds without cleanup)
            else if (child.name == "Baku_F1_FullCircuit_Track")
            {
                activeF1TrackCount++;
                if (activeF1TrackCount > 1)
                {
                    // More than one F1 track child — deactivate extras as backup
                    child.gameObject.SetActive(false);
                    child.name = "[OLD_INACTIVE_BACKUP]_Baku_F1_FullCircuit_Track";
                    Debug.Log($"<color=#FFD700>[Baku Track] Deactivated duplicate F1 track child #{activeF1TrackCount}</color>");
                }
            }
        }

        // 3. Scan scene root objects for any orphaned old track roots that might overlap
        //    Use FindObjectsByType which also finds inactive objects (unlike GameObject.Find)
#if UNITY_2023_1_OR_NEWER
        foreach (var builder in Object.FindObjectsByType<BakuTrackSplineBuilder>(FindObjectsInactive.Include))
#else
        foreach (var builder in Object.FindObjectsOfType<BakuTrackSplineBuilder>(true))
#endif
        {
            if (builder == this) continue;
            // Deactivate old track roots from other builders
            for (int i = 0; i < builder.transform.childCount; i++)
            {
                Transform child = builder.transform.GetChild(i);
                if (child.name.Contains("CastleCorner") || child.name == "Baku_F1_FullCircuit_Track")
                {
                    child.gameObject.SetActive(false);
                }
            }
        }

        // 4. Deactivate internal old backup track if any
        DeactivateSceneObjectByName("Baku_CastleCorner_Track");

        Debug.Log("<color=#FFD700><b>[Baku Track] Safely updated track components!</b></color>");
    }

    /// <summary>
    /// Finds and deactivates any scene GameObject matching the pattern. Works at runtime and in editor.
    /// </summary>
    private void DeactivateSceneObjectByName(string objectNamePattern)
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.IsValid() && scene.isLoaded)
        {
            var rootObjs = scene.GetRootGameObjects();
            foreach (var rootGo in rootObjs)
            {
                if (rootGo == this.gameObject) continue;
                if (rootGo.name.Contains(objectNamePattern) && !rootGo.name.Contains("Baku_F1_FullCircuit"))
                {
                    rootGo.SetActive(false);
                    if (!rootGo.name.StartsWith("[OLD_INACTIVE_BACKUP]"))
                    {
                        rootGo.name = "[OLD_INACTIVE_BACKUP]_" + rootGo.name;
                    }
                    Debug.Log($"<color=#FFD700>[Baku Track] Deactivated overlapping root object: {rootGo.name}</color>");
                }
            }
        }

        // Also check by GameObject.Find for any active object
        GameObject go = GameObject.Find(objectNamePattern);
        if (go != null && go != this.gameObject && !go.name.Contains("Baku_F1_FullCircuit"))
        {
            go.SetActive(false);
            if (!go.name.StartsWith("[OLD_INACTIVE_BACKUP]"))
            {
                go.name = "[OLD_INACTIVE_BACKUP]_" + go.name;
            }
        }
    }

    /// <summary>
    /// Allows toggling between the New F1 Full Circuit and the Old Castle Corner backup.
    /// </summary>
    public void ToggleTrackVersion(bool useNewF1Track)
    {
        if (oldTrackBackup != null)
        {
            oldTrackBackup.SetActive(!useNewF1Track);
        }
        if (generatedTrackRoot != null)
        {
            generatedTrackRoot.SetActive(useNewF1Track);
        }
        Debug.Log($"[Baku Track] Active track version switched to: {(useNewF1Track ? "New F1 Full Circuit" : "Old Castle Corner Backup")}");
    }

    /// <summary>
    /// Generates the complete 3D Baku Castle Corner environment:
    /// - Spline Road Surface with dynamic width & MeshCollider
    /// - Inside Kerbs (Red-White-Yellow)
    /// - Concrete Barriers & Curved Catch Fences
    /// - Medieval Icherisheher Castle Walls, Cylindrical Towers & Gate
    /// - Neoclassical City Buildings, Spectator Grandstands & Trees
    /// </summary>
    public void BuildBakuTrack()
    {
        // Re-entrancy guard
        if (_isBuilding) return;
        _isBuilding = true;

        try
        {
            ResolveMaterials();

            // 1. Deactivate old overlapping circuit components first (NEVER delete them or their assets!)
            DeactivateOldOverlappingTrackComponents();

            // 2. Clean ALL previously generated new F1 tracks under this builder
            //    Loop backwards since we may destroy children
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child.name == "Baku_F1_FullCircuit_Track")
                {
                    if (Application.isPlaying) Destroy(child.gameObject);
                    else DestroyImmediate(child.gameObject);
                }
            }

            generatedTrackRoot = new GameObject("Baku_F1_FullCircuit_Track");
            generatedTrackRoot.transform.SetParent(transform, false);

            var samples = GenerateSplineSamples();
            if (samples.Count < 2) return;

            // 1. Build Drivable Road Surface
            BuildRoadMesh(samples, generatedTrackRoot.transform);

            // 2. Build Inside Kerbs
            BuildKerbsMesh(samples, generatedTrackRoot.transform);

            // 3. Build Concrete Barriers & FIA Catch Fences
            BuildBarriersAndFences(samples, generatedTrackRoot.transform);

            // 4. Build Ancient Icherisheher Castle Fortress & Towers
            BuildIcherisheherFortress(samples, generatedTrackRoot.transform);

            // 5. Build Start/Finish Line Gantry & Starting Grid
            BuildStartFinishAndGrid(generatedTrackRoot.transform);

            // 6. Build Pit Lane, Pit Wall & Paddock Garages
            BuildPitLaneAndPaddock(generatedTrackRoot.transform);

            // 7. Build Distant Baku City Skyline (Flame Towers & Coastal High-Rises)
            BuildDistantSkyline(generatedTrackRoot.transform);

            // 8. Build Trackside Stadium Floodlight Towers
            BuildTracksideFloodlights(generatedTrackRoot.transform);

            // 9. Build City Buildings, Grandstands & Environment
            BuildCityAndEnvironment(samples, generatedTrackRoot.transform);

            Debug.Log($"<color=#00D2BE><b>[Baku Track] Successfully generated full Baku City Circuit (690m Straight + Castle Corner) with {samples.Count} spline nodes!</b></color>");
        }
        finally
        {
            _isBuilding = false;
        }
    }

    private void ResolveMaterials()
    {
        const string matDir = "Assets/RacingV12Pack/UnityAssets/RacingV12Pack/Materials";

#if UNITY_EDITOR
        if (asphaltMaterial == null) asphaltMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>($"{matDir}/Asphalt.mat");
        if (racingLineMaterial == null) racingLineMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>($"{matDir}/Asphalt_RacingLine.mat");
        if (curbRedMaterial == null) curbRedMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>($"{matDir}/CurbRed.mat");
        if (curbWhiteMaterial == null) curbWhiteMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>($"{matDir}/CurbWhite.mat");
        if (curbYellowMaterial == null) curbYellowMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>($"{matDir}/CurbYellow.mat");
        if (concreteBarrierMaterial == null) concreteBarrierMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>($"{matDir}/SponsorBarrier_Red.mat") ?? UnityEditor.AssetDatabase.LoadAssetAtPath<Material>($"{matDir}/Barrier.mat");
        if (catchFenceMaterial == null) catchFenceMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>($"{matDir}/BarrierDark.mat");
        if (sponsorBannerMaterial == null) sponsorBannerMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>($"{matDir}/SponsorBarrier_Red.mat") ?? UnityEditor.AssetDatabase.LoadAssetAtPath<Material>($"{matDir}/PetronasTeal.mat");
        if (castleStoneMaterial == null) castleStoneMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>($"{matDir}/CastleStone.mat") ?? UnityEditor.AssetDatabase.LoadAssetAtPath<Material>($"{matDir}/Concrete.mat");
        if (buildingFacadeMaterial == null) buildingFacadeMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>($"{matDir}/BuildingWhite.mat");
        if (grandstandMaterial == null) grandstandMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>($"{matDir}/SeatRed.mat");
        if (treeFoliageMaterial == null) treeFoliageMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>($"{matDir}/Tree.mat");
        if (gridWhiteMaterial == null) gridWhiteMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>($"{matDir}/GridWhite.mat") ?? UnityEditor.AssetDatabase.LoadAssetAtPath<Material>($"{matDir}/White.mat");
        if (gridBlackMaterial == null) gridBlackMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>($"{matDir}/GridBlack.mat") ?? UnityEditor.AssetDatabase.LoadAssetAtPath<Material>($"{matDir}/Black.mat");
        if (metalMaterial == null) metalMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>($"{matDir}/SilverMetal.mat") ?? UnityEditor.AssetDatabase.LoadAssetAtPath<Material>($"{matDir}/Metal.mat");
        if (windowMaterial == null) windowMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>($"{matDir}/Window.mat");
        if (buildingBlueMaterial == null) buildingBlueMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>($"{matDir}/BuildingBlue.mat");
        if (petronasTealMaterial == null) petronasTealMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>($"{matDir}/PetronasTeal.mat");
        if (ledRedMaterial == null) ledRedMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>($"{matDir}/LEDRed.mat");
        if (ledGreenMaterial == null) ledGreenMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>($"{matDir}/LEDGreen.mat");
#endif
    }

    #endregion

    #region ──── 1. ROAD MESH GENERATION ────

    private void BuildRoadMesh(List<SplineSample> samples, Transform parent)
    {
        GameObject roadGo = new GameObject("Road_Asphalt");
        roadGo.transform.SetParent(parent, false);

        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();
        var triangles = new List<int>();

        // Cross-section: Left Edge, Center-Left, Center, Center-Right, Right Edge
        for (int i = 0; i < samples.Count; i++)
        {
            var s = samples[i];
            float halfW = s.width * 0.5f;
            float v = s.distanceAlongTrack * 0.25f; // UV tiling along track

            Vector3 leftPos = s.position - s.right * halfW + s.up * roadHeightOffset;
            Vector3 midPos = s.position + s.up * roadHeightOffset;
            Vector3 rightPos = s.position + s.right * halfW + s.up * roadHeightOffset;

            int baseIdx = vertices.Count;

            vertices.Add(leftPos);
            vertices.Add(midPos);
            vertices.Add(rightPos);

            normals.Add(s.up);
            normals.Add(s.up);
            normals.Add(s.up);

            uvs.Add(new Vector2(0f, v));
            uvs.Add(new Vector2(0.5f, v));
            uvs.Add(new Vector2(1f, v));

            if (i > 0)
            {
                int prevBase = baseIdx - 3;

                // Left half quad
                triangles.Add(prevBase);
                triangles.Add(prevBase + 1);
                triangles.Add(baseIdx);

                triangles.Add(baseIdx);
                triangles.Add(prevBase + 1);
                triangles.Add(baseIdx + 1);

                // Right half quad
                triangles.Add(prevBase + 1);
                triangles.Add(prevBase + 2);
                triangles.Add(baseIdx + 1);

                triangles.Add(baseIdx + 1);
                triangles.Add(prevBase + 2);
                triangles.Add(baseIdx + 2);
            }
        }

        // Connect loop if closed
        if (isClosedLoop && samples.Count > 2)
        {
            int lastBase = (samples.Count - 1) * 3;
            int firstBase = 0;

            triangles.Add(lastBase);
            triangles.Add(lastBase + 1);
            triangles.Add(firstBase);

            triangles.Add(firstBase);
            triangles.Add(lastBase + 1);
            triangles.Add(firstBase + 1);

            triangles.Add(lastBase + 1);
            triangles.Add(lastBase + 2);
            triangles.Add(firstBase + 1);

            triangles.Add(firstBase + 1);
            triangles.Add(lastBase + 2);
            triangles.Add(firstBase + 2);
        }

        Mesh mesh = new Mesh { name = "Mesh_BakuRoad" };
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();

        Mesh savedMesh = SaveOrUpdateMeshAsset(mesh, "Mesh_BakuF1_Road");

        MeshFilter mf = roadGo.AddComponent<MeshFilter>();
        mf.sharedMesh = savedMesh;

        MeshRenderer mr = roadGo.AddComponent<MeshRenderer>();
        mr.sharedMaterial = asphaltMaterial != null ? asphaltMaterial : new Material(Shader.Find("Universal Render Pipeline/Lit"));

        MeshCollider mc = roadGo.AddComponent<MeshCollider>();
        mc.sharedMesh = savedMesh;
    }

    #endregion

    #region ──── 2. KERBS GENERATION (RED-WHITE-YELLOW) ────

    private void BuildKerbsMesh(List<SplineSample> samples, Transform parent)
    {
        GameObject kerbsGo = new GameObject("Kerbs_InnerApex");
        kerbsGo.transform.SetParent(parent, false);

        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();
        var triangles = new List<int>();

        int lastKerbBaseIdx = -1; // Track the actual last kerb vertex index to handle gaps correctly
        int lastKerbSampleIdx = -1;

        for (int i = 0; i < samples.Count; i++)
        {
            var s = samples[i];
            if (!s.innerKerb) continue;

            float halfW = s.width * 0.5f;
            Vector3 roadEdge = s.position - s.right * halfW + s.up * roadHeightOffset;
            Vector3 kerbRidge = roadEdge - s.right * (kerbWidth * 0.5f) + s.up * kerbHeight;
            Vector3 kerbBack = roadEdge - s.right * kerbWidth + s.up * (kerbHeight * 0.2f);

            int baseIdx = vertices.Count;

            vertices.Add(roadEdge);
            vertices.Add(kerbRidge);
            vertices.Add(kerbBack);

            Vector3 kerbUp = (s.up + -s.right * 0.3f).normalized;
            normals.Add(kerbUp);
            normals.Add(kerbUp);
            normals.Add(s.up);

            float v = s.distanceAlongTrack * 0.8f; // Kerb striped pattern frequency
            uvs.Add(new Vector2(0f, v));
            uvs.Add(new Vector2(0.5f, v));
            uvs.Add(new Vector2(1f, v));

            // Connect to previous kerb sample — only if it was the immediately preceding sample (contiguous)
            // This prevents wrong triangle winding when there are gaps in innerKerb sections
            if (lastKerbBaseIdx >= 0 && lastKerbSampleIdx == i - 1)
            {
                int prevBase = lastKerbBaseIdx;

                triangles.Add(prevBase);
                triangles.Add(prevBase + 1);
                triangles.Add(baseIdx);

                triangles.Add(baseIdx);
                triangles.Add(prevBase + 1);
                triangles.Add(baseIdx + 1);

                triangles.Add(prevBase + 1);
                triangles.Add(prevBase + 2);
                triangles.Add(baseIdx + 1);

                triangles.Add(baseIdx + 1);
                triangles.Add(prevBase + 2);
                triangles.Add(baseIdx + 2);
            }

            lastKerbBaseIdx = baseIdx;
            lastKerbSampleIdx = i;
        }

        if (vertices.Count >= 3)
        {
            Mesh mesh = new Mesh { name = "Mesh_BakuKerbs" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();

            Mesh savedMesh = SaveOrUpdateMeshAsset(mesh, "Mesh_BakuF1_Kerbs");

            MeshFilter mf = kerbsGo.AddComponent<MeshFilter>();
            mf.sharedMesh = savedMesh;

            MeshRenderer mr = kerbsGo.AddComponent<MeshRenderer>();
            mr.sharedMaterial = curbYellowMaterial != null ? curbYellowMaterial : (curbRedMaterial != null ? curbRedMaterial : asphaltMaterial);

            MeshCollider mc = kerbsGo.AddComponent<MeshCollider>();
            mc.sharedMesh = savedMesh;
        }
    }

    #endregion

    #region ──── 3. BARRIERS & FIA CURVED CATCH FENCE ────

    private void BuildBarriersAndFences(List<SplineSample> samples, Transform parent)
    {
        GameObject barriersRoot = new GameObject("Barriers_And_CatchFences");
        barriersRoot.transform.SetParent(parent, false);

        var bVerts = new List<Vector3>();
        var bNorms = new List<Vector3>();
        var bUVs = new List<Vector2>();
        var bTris = new List<int>();

        var fVerts = new List<Vector3>();
        var fNorms = new List<Vector3>();
        var fUVs = new List<Vector2>();
        var fTris = new List<int>();

        // Outer Barriers along the perimeter with 3D thickness (front, top, back faces)
        int lastBarrierBaseIdx = -1;
        int lastFenceBaseIdx = -1;
        int lastBarrierSampleIdx = -1;

        for (int i = 0; i < samples.Count; i++)
        {
            var s = samples[i];
            if (!s.outerBarrier) continue;

            float halfW = s.width * 0.5f;
            Vector3 barrierBase = s.position + s.right * halfW;
            Vector3 barrierTop = barrierBase + s.up * barrierHeight;
            Vector3 barrierBackBase = barrierBase + s.right * barrierThickness;
            Vector3 barrierBackTop = barrierTop + s.right * barrierThickness;

            // Curved fence top (angles towards the track per FIA safety spec)
            Vector3 fenceCurvedOffset = (s.up * Mathf.Cos(fenceOverhangAngle * Mathf.Deg2Rad) -
                                        s.right * Mathf.Sin(fenceOverhangAngle * Mathf.Deg2Rad)) * (fenceHeight - barrierHeight);
            Vector3 fenceTop = barrierTop + fenceCurvedOffset;

            int bIdx = bVerts.Count;
            int fIdx = fVerts.Count;

            // 1. Barrier Front Face (facing track - sponsor banner)
            bVerts.Add(barrierBase);
            bVerts.Add(barrierTop);
            bNorms.Add(-s.right);
            bNorms.Add(-s.right);

            // 2. Barrier Top Face
            bVerts.Add(barrierBackTop);
            bNorms.Add(s.up);

            // 3. Barrier Back Face
            bVerts.Add(barrierBackBase);
            bNorms.Add(s.right);

            float bv = s.distanceAlongTrack * 0.2f;
            bUVs.Add(new Vector2(0f, bv));
            bUVs.Add(new Vector2(1f, bv));
            bUVs.Add(new Vector2(1.2f, bv));
            bUVs.Add(new Vector2(2f, bv));

            // Catch Fence mesh
            fVerts.Add(barrierTop);
            fVerts.Add(fenceTop);
            fNorms.Add(-s.right);
            fNorms.Add(-s.right);

            float fv = s.distanceAlongTrack * 0.5f;
            fUVs.Add(new Vector2(0f, fv));
            fUVs.Add(new Vector2(1f, fv));

            // Connect to previous barrier sample — only if contiguous (immediately preceding)
            if (lastBarrierBaseIdx >= 0 && lastBarrierSampleIdx == i - 1)
            {
                int bPrev = lastBarrierBaseIdx;

                // Front quad
                bTris.Add(bPrev);
                bTris.Add(bPrev + 1);
                bTris.Add(bIdx);

                bTris.Add(bIdx);
                bTris.Add(bPrev + 1);
                bTris.Add(bIdx + 1);

                // Top quad
                bTris.Add(bPrev + 1);
                bTris.Add(bPrev + 2);
                bTris.Add(bIdx + 1);

                bTris.Add(bIdx + 1);
                bTris.Add(bPrev + 2);
                bTris.Add(bIdx + 2);

                // Back quad
                bTris.Add(bPrev + 2);
                bTris.Add(bPrev + 3);
                bTris.Add(bIdx + 2);

                bTris.Add(bIdx + 2);
                bTris.Add(bPrev + 3);
                bTris.Add(bIdx + 3);

                // Catch fence quad
                int fPrev = lastFenceBaseIdx;
                fTris.Add(fPrev);
                fTris.Add(fPrev + 1);
                fTris.Add(fIdx);

                fTris.Add(fIdx);
                fTris.Add(fPrev + 1);
                fTris.Add(fIdx + 1);
            }

            lastBarrierBaseIdx = bIdx;
            lastFenceBaseIdx = fIdx;
            lastBarrierSampleIdx = i;
        }

        // Concrete Barrier GameObject
        if (bVerts.Count >= 4)
        {
            GameObject bGo = new GameObject("Concrete_Barriers_Outer");
            bGo.transform.SetParent(barriersRoot.transform, false);

            Mesh bMesh = new Mesh { name = "Mesh_BakuBarriers" };
            bMesh.SetVertices(bVerts);
            bMesh.SetNormals(bNorms);
            bMesh.SetUVs(0, bUVs);
            bMesh.SetTriangles(bTris, 0);

            Mesh savedBMesh = SaveOrUpdateMeshAsset(bMesh, "Mesh_BakuF1_Barriers");

            MeshFilter mf = bGo.AddComponent<MeshFilter>();
            mf.sharedMesh = savedBMesh;

            MeshRenderer mr = bGo.AddComponent<MeshRenderer>();
            mr.sharedMaterial = concreteBarrierMaterial != null ? concreteBarrierMaterial : new Material(Shader.Find("Universal Render Pipeline/Lit"));

            MeshCollider mc = bGo.AddComponent<MeshCollider>();
            mc.sharedMesh = savedBMesh;
        }

        // Catch Fence GameObject
        if (fVerts.Count >= 2)
        {
            GameObject fGo = new GameObject("Catch_Fence_High");
            fGo.transform.SetParent(barriersRoot.transform, false);

            Mesh fMesh = new Mesh { name = "Mesh_BakuCatchFences" };
            fMesh.SetVertices(fVerts);
            fMesh.SetNormals(fNorms);
            fMesh.SetUVs(0, fUVs);
            fMesh.SetTriangles(fTris, 0);

            Mesh savedFMesh = SaveOrUpdateMeshAsset(fMesh, "Mesh_BakuF1_CatchFences");

            MeshFilter mf = fGo.AddComponent<MeshFilter>();
            mf.sharedMesh = savedFMesh;

            MeshRenderer mr = fGo.AddComponent<MeshRenderer>();
            mr.sharedMaterial = catchFenceMaterial != null ? catchFenceMaterial : new Material(Shader.Find("Universal Render Pipeline/Lit"));
        }
    }

    #endregion

    #region ──── 4. ICHERISHEHER FORTRESS WALL & CYLINDRICAL TOWERS ────

    private void BuildIcherisheherFortress(List<SplineSample> samples, Transform parent)
    {
        GameObject castleRoot = new GameObject("Icherisheher_Ancient_Fortress");
        castleRoot.transform.SetParent(parent, false);

        var wallVerts = new List<Vector3>();
        var wallNorms = new List<Vector3>();
        var wallUVs = new List<Vector2>();
        var wallTris = new List<int>();

        Vector3 towerTurn8Position = Vector3.zero;
        Vector3 towerTurn9Position = Vector3.zero;
        bool foundT8 = false, foundT9 = false;

        // Castle Wall follows the inner curve on the left side
        for (int i = 0; i < samples.Count; i++)
        {
            var s = samples[i];
            if (!s.castleWall) continue;

            float halfW = s.width * 0.5f;
            Vector3 wallBase = s.position - s.right * (halfW + 0.35f); // Hugs the left apex closely
            Vector3 wallTop = wallBase + s.up * castleWallHeight;

            // Locate Turn 8 Apex for primary iconic tower
            if (!foundT8 && s.innerKerb && s.width <= 7.8f && s.position.z > 95f)
            {
                towerTurn8Position = wallBase - s.right * (towerRadius * 0.8f);
                foundT8 = true;
            }

            // Locate Turn 9 Apex for secondary bastion tower
            if (foundT8 && !foundT9 && s.innerKerb && s.position.z > 135f)
            {
                towerTurn9Position = wallBase - s.right * (towerRadius * 0.8f);
                foundT9 = true;
            }

            int wIdx = wallVerts.Count;
            wallVerts.Add(wallBase);
            wallVerts.Add(wallTop);

            wallNorms.Add(s.right); // Faces towards the track
            wallNorms.Add(s.right);

            float wv = s.distanceAlongTrack * 0.15f;
            wallUVs.Add(new Vector2(0f, wv));
            wallUVs.Add(new Vector2(1f, wv));

            if (wallVerts.Count >= 4 && i > 0 && samples[i - 1].castleWall)
            {
                int wPrev = wIdx - 2;
                wallTris.Add(wPrev);
                wallTris.Add(wIdx);
                wallTris.Add(wPrev + 1);

                wallTris.Add(wIdx);
                wallTris.Add(wIdx + 1);
                wallTris.Add(wPrev + 1);
            }
        }

        // Build Wall Mesh
        if (wallVerts.Count >= 2)
        {
            GameObject wallGo = new GameObject("Castle_Rampart_Wall");
            wallGo.transform.SetParent(castleRoot.transform, false);

            Mesh wMesh = new Mesh { name = "Mesh_CastleWall" };
            wMesh.SetVertices(wallVerts);
            wMesh.SetNormals(wallNorms);
            wMesh.SetUVs(0, wallUVs);
            wMesh.SetTriangles(wallTris, 0);

            Mesh savedWMesh = SaveOrUpdateMeshAsset(wMesh, "Mesh_BakuF1_CastleWall");

            MeshFilter mf = wallGo.AddComponent<MeshFilter>();
            mf.sharedMesh = savedWMesh;

            MeshRenderer mr = wallGo.AddComponent<MeshRenderer>();
            mr.sharedMaterial = castleStoneMaterial != null ? castleStoneMaterial : new Material(Shader.Find("Universal Render Pipeline/Lit"));

            MeshCollider mc = wallGo.AddComponent<MeshCollider>();
            mc.sharedMesh = savedWMesh;
        }

        // Build Turn 8 Iconic Cylindrical Tower with crenelations
        if (foundT8)
        {
            BuildCylindricalCastleTower("Turn8_Iconic_Tower", towerTurn8Position, towerHeight, towerRadius, castleRoot.transform);
        }

        if (foundT9)
        {
            BuildCylindricalCastleTower("Turn9_Bastion_Tower", towerTurn9Position, towerHeight * 0.9f, towerRadius * 0.85f, castleRoot.transform);
        }

        // Build Ancient Arched Gate
        if (foundT8)
        {
            Vector3 gatePos = towerTurn8Position - Vector3.forward * 12f;
            BuildArchedFortressGate("Icherisheher_Old_City_Gate", gatePos, Vector3.right, castleRoot.transform);
        }
    }

    private void BuildCylindricalCastleTower(string name, Vector3 basePos, float height, float radius, Transform parent)
    {
        GameObject towerGo = new GameObject(name);
        towerGo.transform.SetParent(parent, false);
        towerGo.transform.position = basePos;

        int segments = 24;
        var verts = new List<Vector3>();
        var norms = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();

        // Cylinder body
        for (int i = 0; i <= segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2f;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);

            Vector3 norm = new Vector3(cos, 0f, sin);
            Vector3 bPos = norm * radius;
            Vector3 tPos = bPos + Vector3.up * height;

            int idx = verts.Count;
            verts.Add(bPos);
            verts.Add(tPos);

            norms.Add(norm);
            norms.Add(norm);

            float u = (float)i / segments * 3f;
            uvs.Add(new Vector2(u, 0f));
            uvs.Add(new Vector2(u, 4f));

            if (i > 0)
            {
                int prev = idx - 2;
                tris.Add(prev);
                tris.Add(idx);
                tris.Add(prev + 1);

                tris.Add(idx);
                tris.Add(idx + 1);
                tris.Add(prev + 1);
            }
        }

        // Crenelated battlements on top (gigi-gigi menara benteng)
        float battlementH = 1.4f;
        float battlementR = radius * 1.08f;
        for (int b = 0; b < towerCrenelations; b++)
        {
            float a0 = (float)b / towerCrenelations * Mathf.PI * 2f;
            float a1 = a0 + (Mathf.PI * 2f / towerCrenelations) * 0.55f; // Gap between teeth

            Vector3 p0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * battlementR + Vector3.up * height;
            Vector3 p1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * battlementR + Vector3.up * height;
            Vector3 p0Top = p0 + Vector3.up * battlementH;
            Vector3 p1Top = p1 + Vector3.up * battlementH;

            int cIdx = verts.Count;
            verts.Add(p0);
            verts.Add(p1);
            verts.Add(p0Top);
            verts.Add(p1Top);

            Vector3 cNorm = (p0 + p1).normalized;
            norms.Add(cNorm); norms.Add(cNorm); norms.Add(cNorm); norms.Add(cNorm);
            uvs.Add(new Vector2(0f, 0f)); uvs.Add(new Vector2(1f, 0f));
            uvs.Add(new Vector2(0f, 1f)); uvs.Add(new Vector2(1f, 1f));

            tris.Add(cIdx); tris.Add(cIdx + 2); tris.Add(cIdx + 1);
            tris.Add(cIdx + 1); tris.Add(cIdx + 2); tris.Add(cIdx + 3);
        }

        Mesh towerMesh = new Mesh { name = $"Mesh_{name}" };
        towerMesh.SetVertices(verts);
        towerMesh.SetNormals(norms);
        towerMesh.SetUVs(0, uvs);
        towerMesh.SetTriangles(tris, 0);

        Mesh savedTowerMesh = SaveOrUpdateMeshAsset(towerMesh, $"Mesh_BakuF1_{name}");

        MeshFilter mf = towerGo.AddComponent<MeshFilter>();
        mf.sharedMesh = savedTowerMesh;

        MeshRenderer mr = towerGo.AddComponent<MeshRenderer>();
        mr.sharedMaterial = castleStoneMaterial != null ? castleStoneMaterial : new Material(Shader.Find("Universal Render Pipeline/Lit"));

        MeshCollider mc = towerGo.AddComponent<MeshCollider>();
        mc.sharedMesh = savedTowerMesh;
    }

    private void BuildArchedFortressGate(string name, Vector3 pos, Vector3 forward, Transform parent)
    {
        GameObject gateGo = new GameObject(name);
        gateGo.transform.SetParent(parent, false);
        gateGo.transform.position = pos;
        gateGo.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);

        // Left pillar (widened to X=-4.8 to clear 7.6m track with ample margin)
        GameObject pLeft = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pLeft.name = "Pillar_Left";
        pLeft.transform.SetParent(gateGo.transform, false);
        pLeft.transform.localPosition = new Vector3(-4.8f, 3.5f, 0f);
        pLeft.transform.localScale = new Vector3(1.4f, 7.0f, 1.8f);
        if (castleStoneMaterial != null) pLeft.GetComponent<Renderer>().sharedMaterial = castleStoneMaterial;
        SafeDestroyCollider(pLeft);

        // Right pillar (widened to X=+4.8 to clear 7.6m track with ample margin)
        GameObject pRight = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pRight.name = "Pillar_Right";
        pRight.transform.SetParent(gateGo.transform, false);
        pRight.transform.localPosition = new Vector3(4.8f, 3.5f, 0f);
        pRight.transform.localScale = new Vector3(1.4f, 7.0f, 1.8f);
        if (castleStoneMaterial != null) pRight.GetComponent<Renderer>().sharedMaterial = castleStoneMaterial;
        SafeDestroyCollider(pRight);

        // Arch lintel on top (spans 11m over the widened pillars)
        GameObject arch = GameObject.CreatePrimitive(PrimitiveType.Cube);
        arch.name = "Arch_Top";
        arch.transform.SetParent(gateGo.transform, false);
        arch.transform.localPosition = new Vector3(0f, 7.5f, 0f);
        arch.transform.localScale = new Vector3(11.0f, 1.4f, 2.0f);
        if (castleStoneMaterial != null) arch.GetComponent<Renderer>().sharedMaterial = castleStoneMaterial;
        SafeDestroyCollider(arch);
    }

    #endregion

    #region ──── 5. START / FINISH GANTRY & STARTING GRID ────

    private void BuildStartFinishAndGrid(Transform parent)
    {
        GameObject sfRoot = new GameObject("StartFinish_And_StartingGrid");
        sfRoot.transform.SetParent(parent, false);

        float sfZ = -200f; // Main straight Start/Finish line position

        // 1. Start/Finish Overhead Gantry Arch (over track at Z = -200)
        GameObject gantry = new GameObject("StartFinish_Gantry");
        gantry.transform.SetParent(sfRoot.transform, false);
        gantry.transform.position = new Vector3(0f, 0f, sfZ);

        // Left Truss Tower
        GameObject towerLeft = GameObject.CreatePrimitive(PrimitiveType.Cube);
        towerLeft.name = "Gantry_Tower_Left";
        towerLeft.transform.SetParent(gantry.transform, false);
        towerLeft.transform.localPosition = new Vector3(-8.8f, 3.6f, 0f);
        towerLeft.transform.localScale = new Vector3(0.9f, 7.2f, 0.9f);
        if (metalMaterial != null) towerLeft.GetComponent<Renderer>().sharedMaterial = metalMaterial;
        SafeDestroyCollider(towerLeft);

        // Right Truss Tower
        GameObject towerRight = GameObject.CreatePrimitive(PrimitiveType.Cube);
        towerRight.name = "Gantry_Tower_Right";
        towerRight.transform.SetParent(gantry.transform, false);
        towerRight.transform.localPosition = new Vector3(8.8f, 3.6f, 0f);
        towerRight.transform.localScale = new Vector3(0.9f, 7.2f, 0.9f);
        if (metalMaterial != null) towerRight.GetComponent<Renderer>().sharedMaterial = metalMaterial;
        SafeDestroyCollider(towerRight);

        // Overhead Cross Beam
        GameObject crossBeam = GameObject.CreatePrimitive(PrimitiveType.Cube);
        crossBeam.name = "Gantry_CrossBeam";
        crossBeam.transform.SetParent(gantry.transform, false);
        crossBeam.transform.localPosition = new Vector3(0f, 7.0f, 0f);
        crossBeam.transform.localScale = new Vector3(18.5f, 1.0f, 0.9f);
        if (metalMaterial != null) crossBeam.GetComponent<Renderer>().sharedMaterial = metalMaterial;
        SafeDestroyCollider(crossBeam);

        // Main Overhead Sponsor / Circuit Banner (facing oncoming cars at Z < -200)
        GameObject banner = GameObject.CreatePrimitive(PrimitiveType.Cube);
        banner.name = "Gantry_SponsorBanner";
        banner.transform.SetParent(gantry.transform, false);
        banner.transform.localPosition = new Vector3(0f, 6.9f, -0.55f);
        banner.transform.localScale = new Vector3(15.2f, 1.6f, 0.15f);
        if (sponsorBannerMaterial != null) banner.GetComponent<Renderer>().sharedMaterial = sponsorBannerMaterial;
        SafeDestroyCollider(banner);

        // F1 5-Light Starting Light Clusters
        GameObject lightsBox = GameObject.CreatePrimitive(PrimitiveType.Cube);
        lightsBox.name = "Gantry_StartingLights_Box";
        lightsBox.transform.SetParent(gantry.transform, false);
        lightsBox.transform.localPosition = new Vector3(0f, 5.4f, -0.45f);
        lightsBox.transform.localScale = new Vector3(6.5f, 0.9f, 0.35f);
        if (gridBlackMaterial != null) lightsBox.GetComponent<Renderer>().sharedMaterial = gridBlackMaterial;
        SafeDestroyCollider(lightsBox);

        // 5 Starting Light Pairs (Red on top, Green on bottom)
        for (int l = 0; l < 5; l++)
        {
            float lx = -2.4f + l * 1.2f;
            GameObject redLight = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            redLight.name = $"StartLight_Red_{l + 1}";
            redLight.transform.SetParent(lightsBox.transform, false);
            redLight.transform.localPosition = new Vector3(lx / 6.5f, 0.22f, -0.55f);
            redLight.transform.localScale = new Vector3(0.12f, 0.38f, 0.12f);
            if (ledRedMaterial != null) redLight.GetComponent<Renderer>().sharedMaterial = ledRedMaterial;
            SafeDestroyCollider(redLight);

            GameObject greenLight = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            greenLight.name = $"StartLight_Green_{l + 1}";
            greenLight.transform.SetParent(lightsBox.transform, false);
            greenLight.transform.localPosition = new Vector3(lx / 6.5f, -0.22f, -0.55f);
            greenLight.transform.localScale = new Vector3(0.12f, 0.38f, 0.12f);
            if (ledGreenMaterial != null) greenLight.GetComponent<Renderer>().sharedMaterial = ledGreenMaterial;
            SafeDestroyCollider(greenLight);
        }

        // Timing Screen (Facing back)
        GameObject timingScreen = GameObject.CreatePrimitive(PrimitiveType.Cube);
        timingScreen.name = "Gantry_TimingScreen";
        timingScreen.transform.SetParent(gantry.transform, false);
        timingScreen.transform.localPosition = new Vector3(0f, 6.9f, 0.55f);
        timingScreen.transform.localScale = new Vector3(15.2f, 1.6f, 0.15f);
        if (windowMaterial != null) timingScreen.GetComponent<Renderer>().sharedMaterial = windowMaterial;
        SafeDestroyCollider(timingScreen);

        // 2. Checkered Start/Finish Line on the Road Surface
        GameObject sfLine = GameObject.CreatePrimitive(PrimitiveType.Cube);
        sfLine.name = "StartFinish_CheckeredLine";
        sfLine.transform.SetParent(sfRoot.transform, false);
        sfLine.transform.position = new Vector3(0f, roadHeightOffset + 0.015f, sfZ);
        sfLine.transform.localScale = new Vector3(15.0f, 0.02f, 1.8f);
        if (gridWhiteMaterial != null) sfLine.GetComponent<Renderer>().sharedMaterial = gridWhiteMaterial;
        SafeDestroyCollider(sfLine);

        // 3. Staggered Starting Grid Boxes (10 Grid Slots: Pole Position at (0, 0.08, -200))
        GameObject gridSlotsRoot = new GameObject("Grid_Boxes");
        gridSlotsRoot.transform.SetParent(sfRoot.transform, false);

        for (int g = 0; g < 10; g++)
        {
            float sideX = (g % 2 == 0) ? 2.2f : -2.2f; // Right / Left staggered (P1 on the right side)
            float gz = (sfZ - 8.0f) - g * 7.5f;

            GameObject gridBox = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gridBox.name = $"Grid_Slot_P{g + 1}";
            gridBox.transform.SetParent(gridSlotsRoot.transform, false);
            gridBox.transform.position = new Vector3(sideX, roadHeightOffset + 0.012f, gz);
            gridBox.transform.localScale = new Vector3(2.4f, 0.015f, 4.6f);
            if (gridWhiteMaterial != null) gridBox.GetComponent<Renderer>().sharedMaterial = gridWhiteMaterial;
            SafeDestroyCollider(gridBox);
        }
    }

    #endregion

    #region ──── 6. PIT LANE & PADDOCK BUILDINGS ────

    private void BuildPitLaneAndPaddock(Transform parent)
    {
        GameObject paddockRoot = new GameObject("PitLane_And_Paddock_Garages");
        paddockRoot.transform.SetParent(parent, false);

        // 1. Pit Lane Drivable Road Strip (runs at X = +11.8m from Z = -230 to Z = +150)
        GameObject pitRoad = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pitRoad.name = "PitLane_RoadSurface";
        pitRoad.transform.SetParent(paddockRoot.transform, false);
        pitRoad.transform.position = new Vector3(11.8f, roadHeightOffset, -40f);
        pitRoad.transform.localScale = new Vector3(6.6f, 0.05f, 380f);
        if (asphaltMaterial != null) pitRoad.GetComponent<Renderer>().sharedMaterial = asphaltMaterial;

        // 2. Concrete Pit Wall with Catch Fence (separates main straight from pit lane)
        // Track right edge is at X = +7.5m, so pit wall must be beyond that to avoid overlap
        GameObject pitWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pitWall.name = "PitWall_Concrete";
        pitWall.transform.SetParent(paddockRoot.transform, false);
        pitWall.transform.position = new Vector3(9.0f, 0.6f + roadHeightOffset, -40f);
        pitWall.transform.localScale = new Vector3(0.55f, 1.2f, 360f);
        if (concreteBarrierMaterial != null) pitWall.GetComponent<Renderer>().sharedMaterial = concreteBarrierMaterial;

        // Pit Wall Catch Fence
        GameObject pitFence = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pitFence.name = "PitWall_CatchFence";
        pitFence.transform.SetParent(paddockRoot.transform, false);
        pitFence.transform.position = new Vector3(9.0f, 1.8f + roadHeightOffset, -40f);
        pitFence.transform.localScale = new Vector3(0.12f, 1.8f, 360f);
        if (catchFenceMaterial != null) pitFence.GetComponent<Renderer>().sharedMaterial = catchFenceMaterial;
        SafeDestroyCollider(pitFence);

        // 4 Team Pit Wall Telemetry Perches (strategically spaced along the pit wall)
        // Moved to X=9.5 so they don't clip into the 15m-wide track
        float[] perchZ = { -160f, -90f, -20f, 50f };
        for (int p = 0; p < perchZ.Length; p++)
        {
            GameObject perch = GameObject.CreatePrimitive(PrimitiveType.Cube);
            perch.name = $"PitWall_Perch_{p + 1}";
            perch.transform.SetParent(paddockRoot.transform, false);
            perch.transform.position = new Vector3(9.5f, 1.5f + roadHeightOffset, perchZ[p]);
            perch.transform.localScale = new Vector3(1.6f, 2.2f, 8.5f);
            if (petronasTealMaterial != null) perch.GetComponent<Renderer>().sharedMaterial = petronasTealMaterial;
            SafeDestroyCollider(perch);
        }

        // 3. Multi-Bay Pit Garages & Paddock Building (X = +21.5m from Z = -200 to Z = +120)
        GameObject garageBuilding = new GameObject("Paddock_Building_Main");
        garageBuilding.transform.SetParent(paddockRoot.transform, false);
        garageBuilding.transform.position = new Vector3(21.5f, 0f, -40f);

        // Ground Floor Garages Structure
        GameObject groundFloor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        groundFloor.name = "Garages_GroundFloor";
        groundFloor.transform.SetParent(garageBuilding.transform, false);
        groundFloor.transform.localPosition = new Vector3(0f, 2.8f, 0f);
        groundFloor.transform.localScale = new Vector3(12.5f, 5.6f, 320f);
        if (buildingFacadeMaterial != null) groundFloor.GetComponent<Renderer>().sharedMaterial = buildingFacadeMaterial;

        // 2nd Floor VIP Paddock Club / Hospitality Suite with Panoramic Windows
        GameObject secondFloor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        secondFloor.name = "PaddockClub_SecondFloor";
        secondFloor.transform.SetParent(garageBuilding.transform, false);
        secondFloor.transform.localPosition = new Vector3(0f, 7.8f, 0f);
        secondFloor.transform.localScale = new Vector3(13.2f, 4.4f, 320f);
        if (windowMaterial != null) secondFloor.GetComponent<Renderer>().sharedMaterial = windowMaterial;

        // 10 Team Garage Door Bays along the front facade (facing Pit Lane at X = +15.2m)
        string[] teamNames = { "Mercedes-AMG Petronas", "Scuderia Ferrari", "Red Bull Racing", "McLaren F1", "Aston Martin", "Alpine F1", "Williams Racing", "Kick Sauber", "Haas F1", "APEX V12 Team" };
        Material[] teamMats = { petronasTealMaterial, curbRedMaterial, buildingBlueMaterial, curbYellowMaterial, petronasTealMaterial, buildingBlueMaterial, windowMaterial, gridWhiteMaterial, curbRedMaterial, petronasTealMaterial };

        float baySpacing = 28f;
        float startBayZ = -140f;
        for (int b = 0; b < 10; b++)
        {
            float bz = startBayZ + b * baySpacing;

            // Garage Door Recess
            GameObject bayDoor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bayDoor.name = $"GarageBay_{teamNames[b]}";
            bayDoor.transform.SetParent(garageBuilding.transform, false);
            bayDoor.transform.localPosition = new Vector3(-6.32f, 2.2f, bz);
            bayDoor.transform.localScale = new Vector3(0.2f, 4.2f, 18.0f);
            if (gridBlackMaterial != null) bayDoor.GetComponent<Renderer>().sharedMaterial = gridBlackMaterial;

            // Team Header Sign above the door
            GameObject teamSign = GameObject.CreatePrimitive(PrimitiveType.Cube);
            teamSign.name = $"TeamSign_{teamNames[b]}";
            teamSign.transform.SetParent(garageBuilding.transform, false);
            teamSign.transform.localPosition = new Vector3(-6.38f, 4.8f, bz);
            teamSign.transform.localScale = new Vector3(0.25f, 0.9f, 18.0f);
            Material sMat = (teamMats[b] != null) ? teamMats[b] : sponsorBannerMaterial;
            if (sMat != null) teamSign.GetComponent<Renderer>().sharedMaterial = sMat;
        }
    }

    #endregion

    #region ──── 7. DISTANT BAKU CITY SKYLINE & FLAME TOWERS ────

    private void BuildDistantSkyline(Transform parent)
    {
        GameObject skylineRoot = new GameObject("Baku_Distant_City_Skyline");
        skylineRoot.transform.SetParent(parent, false);

        // 1. The Iconic Baku Flame Towers on the Western Hill (inspired by Baku's iconic landmark)
        Vector3[] flamePositions = { new Vector3(-230f, 5f, 110f), new Vector3(-265f, 12f, 150f), new Vector3(-295f, 8f, 85f) };
        float[] flameHeights = { 76f, 92f, 68f };
        float[] flameWidths = { 18f, 22f, 16f };

        for (int f = 0; f < flamePositions.Length; f++)
        {
            GameObject flame = new GameObject($"FlameTower_{f + 1}");
            flame.transform.SetParent(skylineRoot.transform, false);
            flame.transform.position = flamePositions[f];

            // Build tapering curved flame tiers
            int tiers = 5;
            float tierH = flameHeights[f] / tiers;
            for (int t = 0; t < tiers; t++)
            {
                float taper = 1.0f - (float)t / tiers * 0.55f;
                GameObject tier = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tier.name = $"FlameTier_{t + 1}";
                tier.transform.SetParent(flame.transform, false);
                tier.transform.localPosition = new Vector3(0f, (t + 0.5f) * tierH, 0f);
                tier.transform.localScale = new Vector3(flameWidths[f] * taper, tierH, flameWidths[f] * 0.7f * taper);
                if (buildingBlueMaterial != null) tier.GetComponent<Renderer>().sharedMaterial = buildingBlueMaterial;
                SafeDestroyCollider(tier);
            }
        }

        // 2. Coastal Modern High-Rises & Skyscrapers along Caspian Boulevard
        float[] coastalZ = { -260f, -170f, -80f, 10f, 100f, 190f, 280f };
        float[] coastalHeights = { 48f, 62f, 54f, 68f, 52f, 58f, 44f };
        float[] coastalWidths = { 26f, 32f, 28f, 35f, 25f, 30f, 24f };

        for (int c = 0; c < coastalZ.Length; c++)
        {
            GameObject tower = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tower.name = $"Caspian_Skyscraper_{c + 1}";
            tower.transform.SetParent(skylineRoot.transform, false);
            tower.transform.position = new Vector3(75f + (c % 2) * 12f, coastalHeights[c] * 0.5f, coastalZ[c]);
            tower.transform.localScale = new Vector3(coastalWidths[c], coastalHeights[c], coastalWidths[c]);
            Material tMat = (c % 2 == 0) ? buildingBlueMaterial : windowMaterial;
            if (tMat != null) tower.GetComponent<Renderer>().sharedMaterial = tMat;
            SafeDestroyCollider(tower);
        }
    }

    #endregion

    #region ──── 8. TRACKSIDE STADIUM FLOODLIGHTS ────

    private void BuildTracksideFloodlights(Transform parent)
    {
        GameObject lightsRoot = new GameObject("Trackside_Stadium_Floodlights");
        lightsRoot.transform.SetParent(parent, false);

        // Floodlight Tower positions along the circuit:
        // 5 on Main Straight (X=-11 safely beyond track left edge X=-7.5), 
        // 2 around Turn 1/2 Azadliq Square, 1 near Castle Corner (clear of 7.6m bottleneck)
        Vector3[] lightPositions = {
            new Vector3(-11.0f, 0f, -280f),
            new Vector3(-11.0f, 0f, -140f),
            new Vector3(-11.0f, 0f, 0f),
            new Vector3(-11.0f, 0f, 160f),
            new Vector3(-11.0f, 0f, 310f),
            new Vector3(-70f, 0f, 395f),
            new Vector3(-125f, 0f, 310f),
            new Vector3(-12.0f, 4.0f, 110f)  // Moved clear of Turn 8 bottleneck (track right edge ≈ -19.2m)
        };

        for (int i = 0; i < lightPositions.Length; i++)
        {
            GameObject mast = new GameObject($"Floodlight_Mast_{i + 1}");
            mast.transform.SetParent(lightsRoot.transform, false);
            mast.transform.position = lightPositions[i];

            // Steel Vertical Pole
            GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "Pole";
            pole.transform.SetParent(mast.transform, false);
            pole.transform.localPosition = new Vector3(0f, 9.0f, 0f);
            pole.transform.localScale = new Vector3(0.55f, 9.0f, 0.55f);
            if (metalMaterial != null) pole.GetComponent<Renderer>().sharedMaterial = metalMaterial;
            SafeDestroyCollider(pole);

            // Overhead Floodlight Cross-head Frame
            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Cube);
            head.name = "LightHead_Frame";
            head.transform.SetParent(mast.transform, false);
            head.transform.localPosition = new Vector3(0f, 18.2f, 0f);
            head.transform.localScale = new Vector3(4.2f, 1.4f, 0.8f);
            if (gridBlackMaterial != null) head.GetComponent<Renderer>().sharedMaterial = gridBlackMaterial;
            SafeDestroyCollider(head);

            // 6 Emissive Light Bulbs
            for (int b = 0; b < 6; b++)
            {
                float bx = -1.6f + (b % 3) * 1.6f;
                float by = (b < 3) ? 0.35f : -0.35f;

                GameObject bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                bulb.name = $"Bulb_{b + 1}";
                bulb.transform.SetParent(head.transform, false);
                bulb.transform.localPosition = new Vector3(bx / 4.2f, by / 1.4f, -0.55f);
                bulb.transform.localScale = new Vector3(0.2f, 0.35f, 0.15f);
                if (gridWhiteMaterial != null) bulb.GetComponent<Renderer>().sharedMaterial = gridWhiteMaterial;
                SafeDestroyCollider(bulb);
            }
        }
    }

    #endregion

    #region ──── 9. CITY BUILDINGS, GRANDSTANDS & TREES ────

    private void BuildCityAndEnvironment(List<SplineSample> samples, Transform parent)
    {
        GameObject cityRoot = new GameObject("Baku_City_Buildings_And_Grandstands");
        cityRoot.transform.SetParent(parent, false);

        // 1. Spectator Grandstands:
        // A. Main Straight Grandstand (facing pit lane & starting grid, 90m long, 12 tiers)
        BuildSpectatorGrandstand("Grandstand_MainStraight_PaddockView", new Vector3(-16.0f, 0f, -190f), Vector3.right, cityRoot.transform, 12, 90f);

        // B. Turn 1 Azadliq Grandstand (overlooking 360 km/h heavy braking zone)
        BuildSpectatorGrandstand("Grandstand_Turn1_Azadliq", new Vector3(-25.0f, 0f, 370f), new Vector3(0.3f, 0f, -1f).normalized, cityRoot.transform, 8, 48f);

        // C. Turn 8 Castle Corner Grandstand (overlooking narrow bottleneck apex)
        // Positioned well clear of the track edge to avoid blocking
        BuildSpectatorGrandstand("Grandstand_Turn8_CastleView", new Vector3(-8.0f, 3.8f, 110f), new Vector3(-1f, 0f, 0.2f).normalized, cityRoot.transform, 6, 28f);

        // 2. Neoclassical Baku City Buildings lining Pushkin, Khagani & Aziz Aliyev Streets
        Vector3[] bPositions = {
            new Vector3(-135f, 10f, 290f),
            new Vector3(-135f, 12f, 220f),
            new Vector3(-65f, 9f, 130f),
            new Vector3(-70f, 11f, 60f),
            new Vector3(-58f, 10f, 45f)   // Moved further from track to avoid clipping Turn 7 path
        };
        Vector3[] bScales = {
            new Vector3(28f, 20f, 45f),
            new Vector3(25f, 24f, 40f),
            new Vector3(24f, 18f, 35f),
            new Vector3(26f, 22f, 38f),
            new Vector3(22f, 20f, 30f)
        };

        for (int b = 0; b < bPositions.Length; b++)
        {
            GameObject bGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bGo.name = $"Baku_ClassicalBuilding_{b + 1}";
            bGo.transform.SetParent(cityRoot.transform, false);
            bGo.transform.position = bPositions[b];
            bGo.transform.localScale = bScales[b];

            if (buildingFacadeMaterial != null)
            {
                bGo.GetComponent<Renderer>().sharedMaterial = buildingFacadeMaterial;
            }
            SafeDestroyCollider(bGo);
        }

        // 3. Street Trees along the Waterfront Boulevard
        // Placed at X=-12.5 (clear of 15m track left edge at X=-7.5)
        float[] treeZ = { -320f, -250f, -170f, -90f, -10f, 70f, 150f, 230f, 310f };
        for (int t = 0; t < treeZ.Length; t++)
        {
            BuildStreetTree($"Baku_Tree_{t + 1}", new Vector3(-12.5f, 0f, treeZ[t]), cityRoot.transform);
        }
    }

    private void BuildSpectatorGrandstand(string name, Vector3 pos, Vector3 forward, Transform parent, int tiers = 8, float tierWidth = 32f)
    {
        GameObject standGo = new GameObject(name);
        standGo.transform.SetParent(parent, false);
        standGo.transform.position = pos;
        standGo.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);

        float tierDepth = 1.1f;
        float tierHeight = 0.55f;

        for (int t = 0; t < tiers; t++)
        {
            GameObject tier = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tier.name = $"Tier_{t + 1}";
            tier.transform.SetParent(standGo.transform, false);
            tier.transform.localPosition = new Vector3(0f, (t + 1) * tierHeight * 0.5f, t * tierDepth);
            tier.transform.localScale = new Vector3(tierWidth, (t + 1) * tierHeight, tierDepth);

            if (grandstandMaterial != null)
            {
                tier.GetComponent<Renderer>().sharedMaterial = grandstandMaterial;
            }
            SafeDestroyCollider(tier);
        }
    }

    private void BuildStreetTree(string name, Vector3 pos, Transform parent)
    {
        GameObject treeGo = new GameObject(name);
        treeGo.transform.SetParent(parent, false);
        treeGo.transform.position = pos;

        // Trunk
        GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        trunk.name = "Trunk";
        trunk.transform.SetParent(treeGo.transform, false);
        trunk.transform.localPosition = new Vector3(0f, 2.0f, 0f);
        trunk.transform.localScale = new Vector3(0.45f, 2.0f, 0.45f);
        SafeDestroyCollider(trunk); // Don't block the car

        // Foliage Crown
        GameObject foliage = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        foliage.name = "Foliage";
        foliage.transform.SetParent(treeGo.transform, false);
        foliage.transform.localPosition = new Vector3(0f, 4.8f, 0f);
        foliage.transform.localScale = new Vector3(3.8f, 3.8f, 3.8f);
        SafeDestroyCollider(foliage); // Don't block the car

        if (treeFoliageMaterial != null)
        {
            foliage.GetComponent<Renderer>().sharedMaterial = treeFoliageMaterial;
        }
    }

    private static void SafeDestroyCollider(GameObject go)
    {
        Collider col = go.GetComponent<Collider>();
        if (col != null)
        {
            if (Application.isPlaying) Destroy(col);
            else DestroyImmediate(col);
        }
    }

    private SplineSample GetSampleAtDistance(List<SplineSample> samples, float targetDist)
    {
        if (samples == null || samples.Count == 0) return default;
        for (int i = 0; i < samples.Count - 1; i++)
        {
            if (samples[i].distanceAlongTrack <= targetDist && samples[i + 1].distanceAlongTrack >= targetDist)
            {
                float factor = (targetDist - samples[i].distanceAlongTrack) / Mathf.Max(0.001f, samples[i + 1].distanceAlongTrack - samples[i].distanceAlongTrack);
                return new SplineSample
                {
                    position = Vector3.Lerp(samples[i].position, samples[i + 1].position, factor),
                    forward = Vector3.Lerp(samples[i].forward, samples[i + 1].forward, factor).normalized,
                    right = Vector3.Lerp(samples[i].right, samples[i + 1].right, factor).normalized,
                    up = Vector3.Lerp(samples[i].up, samples[i + 1].up, factor).normalized,
                    width = Mathf.Lerp(samples[i].width, samples[i + 1].width, factor),
                    distanceAlongTrack = targetDist
                };
            }
        }
        return samples[samples.Count - 1];
    }

    #endregion
}
