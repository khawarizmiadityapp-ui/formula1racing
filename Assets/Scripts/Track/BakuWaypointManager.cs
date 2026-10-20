using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Closed-loop spline waypoint system for the Baku Street Circuit.
/// Generates 600+ dense waypoints from the Baku 46-control-point centerline.
/// Provides track distance, progress (0..1), target cornering speed,
/// and builds the visual 3D Racing Line ribbon on the asphalt (inspired by the reference video).
/// </summary>
[ExecuteAlways]
public class BakuWaypointManager : MonoBehaviour
{
    public static BakuWaypointManager Instance { get; private set; }

    [System.Serializable]
    public struct Waypoint
    {
        public Vector3 position;
        public Vector3 forward;
        public float curvature; // 0 = straight, higher = sharper turn
        public float recommendedSpeedKph; // AI cornering speed target
        public float distanceAlongTrack; // meters from start line
    }

    [Header("Track Configuration")]
    [SerializeField] private float trackElevation = 0.08f;
    [SerializeField] private int splineResolution = 600;

    [Header("Visual Racing Line")]
    [SerializeField] private bool showRacingLine = true;
    [SerializeField] private float lineWidth = 0.85f;
    [SerializeField] private Material racingLineMaterial;

    [Header("Starting Grid Positions")]
    [Tooltip("Starting grid slots along the main straight (-500 Z)")]
    [SerializeField] private Vector3[] gridSlots;

    private readonly List<Waypoint> waypoints = new List<Waypoint>();
    private float totalTrackLength;
    private GameObject racingLineObj;

    public int WaypointCount => waypoints.Count;
    public float TotalTrackLength => totalTrackLength;
    public IReadOnlyList<Waypoint> Waypoints => waypoints;

    // The 46 control points from Baku_StreetCircuit_Route.json
    private static readonly Vector2[] ControlPointsXZ = new Vector2[]
    {
        new Vector2(-870.0f, -500.0f), // 0: Main straight start
        new Vector2(-340.0f, -500.0f), // Grid / Player Start
        new Vector2(-260.0f, -500.0f), // Start/Finish line
        new Vector2(200.0f, -500.0f),
        new Vector2(700.0f, -500.0f),  // 4: End of 1.57km main straight
        new Vector2(800.0f, -492.0f),  // 5: Turn 1 approach
        new Vector2(865.0f, -445.0f),  // 6: Turn 1 Apex
        new Vector2(890.0f, -365.0f),
        new Vector2(890.0f, -260.0f),  // 8: Turn 2
        new Vector2(860.0f, -205.0f),
        new Vector2(800.0f, -170.0f),
        new Vector2(690.0f, -150.0f),
        new Vector2(560.0f, -138.0f),
        new Vector2(450.0f, -120.0f),
        new Vector2(360.0f, -100.0f),
        new Vector2(280.0f, -80.0f),
        new Vector2(210.0f, -50.0f),
        new Vector2(150.0f, -15.0f),
        new Vector2(100.0f, 30.0f),
        new Vector2(60.0f, 85.0f),
        new Vector2(35.0f, 150.0f),    // 20: Approach Old City
        new Vector2(15.0f, 220.0f),
        new Vector2(-10.0f, 280.0f),   // 22: Turn 8 Castle Corner
        new Vector2(-35.0f, 330.0f),
        new Vector2(-70.0f, 365.0f),
        new Vector2(-120.0f, 385.0f),
        new Vector2(-180.0f, 395.0f),  // 26: Top of Old City
        new Vector2(-260.0f, 395.0f),
        new Vector2(-350.0f, 380.0f),
        new Vector2(-440.0f, 350.0f),
        new Vector2(-520.0f, 305.0f),  // 30: Downhill chicane
        new Vector2(-585.0f, 250.0f),
        new Vector2(-640.0f, 185.0f),
        new Vector2(-685.0f, 110.0f),
        new Vector2(-720.0f, 30.0f),
        new Vector2(-750.0f, -60.0f),
        new Vector2(-780.0f, -160.0f), // 36: Turn 16
        new Vector2(-815.0f, -260.0f),
        new Vector2(-850.0f, -345.0f),
        new Vector2(-875.0f, -405.0f),
        new Vector2(-890.0f, -445.0f), // 40: Fast Turn 18/19
        new Vector2(-895.0f, -475.0f), // 41: Turn 20 onto main straight
        new Vector2(-885.0f, -495.0f),
        new Vector2(-870.0f, -500.0f)  // Close loop
    };

    private void Awake()
    {
        Instance = this;
        GenerateSplineWaypoints();
        SetupStartingGrid();
        if (showRacingLine) BuildVisualRacingLine();
    }

    private void OnValidate()
    {
        Instance = this;
        GenerateSplineWaypoints();
        SetupStartingGrid();
    }

    public void GenerateSplineWaypoints()
    {
        waypoints.Clear();
        int cpCount = ControlPointsXZ.Length;
        if (cpCount < 4) return;

        List<Vector3> rawPositions = new List<Vector3>();

        // Sample Catmull-Rom spline
        int segments = cpCount - 1;
        int pointsPerSegment = Mathf.Max(2, splineResolution / segments);

        for (int i = 0; i < segments; i++)
        {
            Vector2 p0 = ControlPointsXZ[(i - 1 + cpCount) % cpCount];
            Vector2 p1 = ControlPointsXZ[i];
            Vector2 p2 = ControlPointsXZ[(i + 1) % cpCount];
            Vector2 p3 = ControlPointsXZ[(i + 2) % cpCount];

            for (int step = 0; step < pointsPerSegment; step++)
            {
                float t = (float)step / pointsPerSegment;
                Vector2 pos2D = CatmullRom(p0, p1, p2, p3, t);
                rawPositions.Add(new Vector3(pos2D.x, trackElevation, pos2D.y));
            }
        }

        if (rawPositions.Count < 2) return;

        // Calculate distances, tangents, and curvature
        totalTrackLength = 0f;
        for (int i = 0; i < rawPositions.Count; i++)
        {
            Vector3 curr = rawPositions[i];
            Vector3 next = rawPositions[(i + 1) % rawPositions.Count];
            Vector3 prev = rawPositions[(i - 1 + rawPositions.Count) % rawPositions.Count];

            Vector3 fwd = (next - curr).normalized;
            Vector3 prevFwd = (curr - prev).normalized;

            // Curvature based on angular deflection between segments
            float angleDeg = Vector3.Angle(prevFwd, fwd);
            float segLen = Vector3.Distance(prev, curr);
            float curvature = segLen > 0.01f ? (angleDeg / segLen) : 0f;

            // Speed formula: straight (curvature < 0.2) = 360 km/h, hairpins (curvature > 3) = 85 km/h
            float speedTarget = Mathf.Clamp(360f - (curvature * 75f), 85f, 360f);

            Waypoint wp = new Waypoint
            {
                position = curr,
                forward = fwd,
                curvature = curvature,
                recommendedSpeedKph = speedTarget,
                distanceAlongTrack = totalTrackLength
            };

            waypoints.Add(wp);
            totalTrackLength += Vector3.Distance(curr, next);
        }

        // Second pass: smooth braking zones backwards so AI brakes in advance of sharp turns
        SmoothBrakingZones();
    }

    private void SmoothBrakingZones()
    {
        if (waypoints.Count == 0) return;
        const float maxDecelKphPerMeter = 1.6f; // ~15 m/s^2 F1 braking rate

        for (int pass = 0; pass < 2; pass++)
        {
            for (int i = waypoints.Count - 1; i >= 0; i--)
            {
                int nextIdx = (i + 1) % waypoints.Count;
                Waypoint curr = waypoints[i];
                Waypoint next = waypoints[nextIdx];

                float dist = Vector3.Distance(curr.position, next.position);
                float maxAllowedSpeed = next.recommendedSpeedKph + (dist * maxDecelKphPerMeter);

                if (curr.recommendedSpeedKph > maxAllowedSpeed)
                {
                    curr.recommendedSpeedKph = maxAllowedSpeed;
                    waypoints[i] = curr;
                }
            }
        }
    }

    private void SetupStartingGrid()
    {
        // 8 Starting grid slots along the main straight (X between -340 and -270, Z ~ -500)
        // Alternating Left / Right positions (F1 staggered grid)
        gridSlots = new Vector3[8];
        float startX = -340f;
        float slotSpacing = 8.5f;

        for (int i = 0; i < gridSlots.Length; i++)
        {
            float sideOffset = (i % 2 == 0) ? -2.2f : 2.2f; // Left/Right of centerline
            gridSlots[i] = new Vector3(startX + (i * slotSpacing), trackElevation + 0.12f, -500f + sideOffset);
        }
    }

    public Vector3 GetGridSlotPosition(int slotIndex)
    {
        if (gridSlots == null || gridSlots.Length == 0) SetupStartingGrid();
        if (slotIndex >= 0 && slotIndex < gridSlots.Length) return gridSlots[slotIndex];
        return new Vector3(-340f + (slotIndex * 8.5f), trackElevation + 0.12f, -500f);
    }

    public Quaternion GetGridSlotRotation()
    {
        return Quaternion.Euler(0f, 90f, 0f); // Facing +X down main straight
    }

    public Waypoint GetClosestWaypoint(Vector3 worldPos, out int closestIndex)
    {
        closestIndex = 0;
        if (waypoints.Count == 0) return default;

        float bestDistSqr = float.MaxValue;
        for (int i = 0; i < waypoints.Count; i++)
        {
            float dSqr = (waypoints[i].position - worldPos).sqrMagnitude;
            if (dSqr < bestDistSqr)
            {
                bestDistSqr = dSqr;
                closestIndex = i;
            }
        }
        return waypoints[closestIndex];
    }

    public Waypoint GetTargetWaypoint(Vector3 currentPos, float lookaheadMeters)
    {
        if (waypoints.Count == 0) return default;
        GetClosestWaypoint(currentPos, out int idx);

        float accumulated = 0f;
        while (accumulated < lookaheadMeters)
        {
            int nextIdx = (idx + 1) % waypoints.Count;
            accumulated += Vector3.Distance(waypoints[idx].position, waypoints[nextIdx].position);
            idx = nextIdx;
        }

        return waypoints[idx];
    }

    public float GetTrackProgress(Vector3 worldPos)
    {
        if (totalTrackLength <= 0.01f || waypoints.Count == 0) return 0f;
        Waypoint wp = GetClosestWaypoint(worldPos, out _);
        return Mathf.Clamp01(wp.distanceAlongTrack / totalTrackLength);
    }

    public void BuildVisualRacingLine()
    {
        if (racingLineObj != null) DestroyImmediate(racingLineObj);
        if (waypoints.Count < 3) return;

        racingLineObj = new GameObject("Baku_Visual_RacingLine");
        racingLineObj.transform.SetParent(transform, false);

        MeshFilter mf = racingLineObj.AddComponent<MeshFilter>();
        MeshRenderer mr = racingLineObj.AddComponent<MeshRenderer>();

        // Material setup: Yellow/Green dotted line on asphalt matching reference
        Material lineMat = racingLineMaterial;
        if (lineMat == null)
        {
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlit == null) unlit = Shader.Find("Unlit/Color");
            lineMat = new Material(unlit) { name = "RacingLine_VisualMat" };
            lineMat.SetColor("_BaseColor", new Color(0.96f, 0.88f, 0.12f, 0.92f));
        }
        mr.sharedMaterial = lineMat;

        // Generate ribbon mesh with dotted segments
        Mesh mesh = new Mesh { name = "RacingLine_Mesh" };
        List<Vector3> verts = new List<Vector3>();
        List<int> tris = new List<int>();
        List<Color> colors = new List<Color>();
        List<Vector2> uvs = new List<Vector2>();

        int count = waypoints.Count;
        float halfW = lineWidth * 0.5f;

        for (int i = 0; i < count; i += 2) // Dotted segments (draw every 2nd segment)
        {
            int iNext = (i + 1) % count;
            Waypoint w0 = waypoints[i];
            Waypoint w1 = waypoints[iNext];

            Vector3 right0 = Vector3.Cross(Vector3.up, w0.forward).normalized * halfW;
            Vector3 right1 = Vector3.Cross(Vector3.up, w1.forward).normalized * halfW;

            // Color: Yellow/Green on fast sections, Orange/Red in heavy braking zones
            Color segColor = (w0.recommendedSpeedKph > 200f) ? 
                new Color(0.95f, 0.90f, 0.10f, 0.95f) : // Fast: Bright Yellow
                new Color(0.98f, 0.22f, 0.15f, 0.95f);   // Braking: Vivid Red

            int vBase = verts.Count;
            // Elevate slightly above road (Y + 0.035) to avoid Z-fighting
            verts.Add(w0.position - right0 + Vector3.up * 0.035f);
            verts.Add(w0.position + right0 + Vector3.up * 0.035f);
            verts.Add(w1.position - right1 + Vector3.up * 0.035f);
            verts.Add(w1.position + right1 + Vector3.up * 0.035f);

            colors.Add(segColor);
            colors.Add(segColor);
            colors.Add(segColor);
            colors.Add(segColor);

            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(1f, 0f));
            uvs.Add(new Vector2(0f, 1f));
            uvs.Add(new Vector2(1f, 1f));

            tris.Add(vBase + 0);
            tris.Add(vBase + 2);
            tris.Add(vBase + 1);

            tris.Add(vBase + 1);
            tris.Add(vBase + 2);
            tris.Add(vBase + 3);
        }

        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.SetColors(colors);
        mesh.SetUVs(0, uvs);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        mf.sharedMesh = mesh;
    }

    private static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
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
}
