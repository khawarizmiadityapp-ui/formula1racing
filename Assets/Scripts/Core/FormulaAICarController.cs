using UnityEngine;

/// <summary>
/// Lightweight, high-performance Formula AI Opponent Controller optimized for Android and PC.
/// Uses physics-driven Rigidbody driving to follow the BakuWaypointManager closed-loop spline.
/// Features:
/// - Dynamic throttle & braking based on corner curvature
/// - Forward obstacle raycasts to avoid colliding with player or fellow AI
/// - Automatic recovery if spun or stuck
/// - Visual wheel steering and rotation
/// - Real-time progress and lap calculation for leaderboard
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class FormulaAICarController : MonoBehaviour
{
    [Header("═══ AI IDENTITY ═══")]
    [SerializeField] private string driverName = "SAI";
    [SerializeField] private Color liveryColor = Color.red;

    [Header("═══ PERFORMANCE ═══")]
    [Tooltip("AI Top Speed multiplier (0.85 = chill, 1.0 = competitive, 1.05 = pro)")]
    [SerializeField] [Range(0.8f, 1.15f)] private float difficultyMultiplier = 0.95f;
    [SerializeField] private float accelerationRate = 42f;
    [SerializeField] private float brakingRate = 60f;
    [SerializeField] private float steeringSensitivity = 16f;
    [SerializeField] private float lookaheadDistance = 18f;

    [Header("═══ COLLISION AVOIDANCE ═══")]
    [SerializeField] private float avoidanceDistance = 14f;
    [SerializeField] private float lateralLaneOffset = 0f; // Each AI drives in a slightly different line

    [Header("═══ WHEEL VISUALS ═══")]
    [SerializeField] private Transform frontLeftWheel;
    [SerializeField] private Transform frontRightWheel;
    [SerializeField] private Transform rearLeftWheel;
    [SerializeField] private Transform rearRightWheel;
    [SerializeField] private float wheelRadius = 0.395f;

    private Rigidbody rb;
    private BakuWaypointManager waypointMgr;
    private float spinAngle;
    private float currentSteerAngle;
    private float stuckTimer;
    private int currentLap = 1;
    private int lastWaypointIdx = 0;
    private float totalDistanceTravelled;

    // Public properties for Leaderboard
    public string DriverName => driverName;
    public Color LiveryColor => liveryColor;
    public int CurrentLap => currentLap;
    public float CurrentSpeedKph => rb != null ? rb.linearVelocity.magnitude * 3.6f : 0f;
    public float TotalDistanceTravelled => totalDistanceTravelled;

    public void Setup(string name, Color color, float difficulty, float laneOffset)
    {
        driverName = name;
        liveryColor = color;
        difficultyMultiplier = difficulty;
        lateralLaneOffset = laneOffset;
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.mass = 780f;
        rb.linearDamping = 0.08f;
        rb.angularDamping = 2.2f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        ResolveWheels();
    }

    private void Start()
    {
        waypointMgr = BakuWaypointManager.Instance;
        if (waypointMgr == null) waypointMgr = Object.FindAnyObjectByType<BakuWaypointManager>();
    }

    private void ResolveWheels()
    {
        if (frontLeftWheel == null) frontLeftWheel = FindChildRecursive(transform, "FrontLeft");
        if (frontRightWheel == null) frontRightWheel = FindChildRecursive(transform, "FrontRight");
        if (rearLeftWheel == null) rearLeftWheel = FindChildRecursive(transform, "RearLeft");
        if (rearRightWheel == null) rearRightWheel = FindChildRecursive(transform, "RearRight");
    }

    private static Transform FindChildRecursive(Transform parent, string name)
    {
        if (parent == null) return null;
        Transform d = parent.Find(name);
        if (d != null) return d;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindChildRecursive(parent.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }

    private void FixedUpdate()
    {
        if (waypointMgr == null)
        {
            waypointMgr = BakuWaypointManager.Instance;
            if (waypointMgr == null) return;
        }

        Vector3 myPos = transform.position;
        float currentSpeed = rb.linearVelocity.magnitude;
        float currentSpeedKph = currentSpeed * 3.6f;

        // Dynamic lookahead scales with speed (12m at slow speeds up to 35m at 300+ km/h)
        float dynamicLookahead = Mathf.Clamp(lookaheadDistance + (currentSpeed * 0.22f), 12f, 38f);
        BakuWaypointManager.Waypoint targetWp = waypointMgr.GetTargetWaypoint(myPos, dynamicLookahead);

        // Apply lane offset
        Vector3 rightOffset = Vector3.Cross(Vector3.up, targetWp.forward).normalized * lateralLaneOffset;
        Vector3 targetPos = targetWp.position + rightOffset;

        // Steering calculation
        Vector3 localTarget = transform.InverseTransformPoint(targetPos);
        float steerTarget = Mathf.Clamp(localTarget.x / Mathf.Max(localTarget.z, 2.0f), -1f, 1f);

        // Steering damping at high speeds
        float steerFactor = Mathf.Lerp(1.0f, 0.35f, Mathf.Clamp01(currentSpeedKph / 250f));
        float steerTorque = steerTarget * steeringSensitivity * steerFactor;
        rb.AddTorque(transform.up * steerTorque, ForceMode.Acceleration);

        // Target Speed & Obstacle Avoidance
        float targetSpeedKph = targetWp.recommendedSpeedKph * difficultyMultiplier;

        // Forward Obstacle Detection (Raycast against player or other AI)
        bool obstacleAhead = false;
        if (Physics.Raycast(myPos + Vector3.up * 0.4f, transform.forward, out RaycastHit hit, avoidanceDistance))
        {
            if (hit.collider != null && hit.collider.transform.root != transform)
            {
                obstacleAhead = true;
                // Match speed or brake to prevent rear-ending
                targetSpeedKph = Mathf.Min(targetSpeedKph, hit.distance * 12f);
            }
        }

        // Drive / Brake Forces
        if (currentSpeedKph < targetSpeedKph && !obstacleAhead)
        {
            // Accelerate
            float accel = accelerationRate * Mathf.Clamp01((targetSpeedKph - currentSpeedKph) / 40f);
            rb.AddForce(transform.forward * (accel * rb.mass), ForceMode.Force);
        }
        else if (currentSpeedKph > targetSpeedKph + 8f || obstacleAhead)
        {
            // Decelerate / Brake
            float brake = brakingRate * Mathf.Clamp01((currentSpeedKph - targetSpeedKph) / 30f);
            rb.AddForce(-transform.forward * (brake * rb.mass), ForceMode.Force);
        }

        // Aerodynamic Downforce to keep AI glued to track
        float downforce = currentSpeed * currentSpeed * 0.45f;
        rb.AddForce(-Vector3.up * downforce, ForceMode.Force);

        // Update Lap and Race Distance
        UpdateTrackProgress(myPos);

        // Stuck Detection & Recovery
        CheckStuckRecovery(currentSpeed);
    }

    private void UpdateTrackProgress(Vector3 myPos)
    {
        BakuWaypointManager.Waypoint closest = waypointMgr.GetClosestWaypoint(myPos, out int closestIdx);
        
        // Lap completion detection (crossing start/finish from high index to low index)
        if (lastWaypointIdx > waypointMgr.WaypointCount - 25 && closestIdx < 25)
        {
            currentLap++;
            Debug.Log($"[APEX AI] {driverName} started Lap {currentLap}!");
        }
        lastWaypointIdx = closestIdx;

        totalDistanceTravelled = ((currentLap - 1) * waypointMgr.TotalTrackLength) + closest.distanceAlongTrack;
    }

    private void CheckStuckRecovery(float currentSpeed)
    {
        if (currentSpeed < 1.5f)
        {
            stuckTimer += Time.fixedDeltaTime;
            if (stuckTimer > 3.5f)
            {
                // Respawn on nearest track waypoint
                BakuWaypointManager.Waypoint wp = waypointMgr.GetClosestWaypoint(transform.position, out _);
                transform.position = wp.position + Vector3.up * 0.2f;
                transform.rotation = Quaternion.LookRotation(wp.forward, Vector3.up);
                rb.linearVelocity = wp.forward * 15f; // Rolling restart
                rb.angularVelocity = Vector3.zero;
                stuckTimer = 0f;
            }
        }
        else
        {
            stuckTimer = 0f;
        }
    }

    private void Update()
    {
        // Visual wheel animation
        float forwardSpeed = rb != null ? Vector3.Dot(rb.linearVelocity, transform.forward) : 0f;
        spinAngle += (forwardSpeed / Mathf.Max(0.05f, wheelRadius)) * Mathf.Rad2Deg * Time.deltaTime;
        if (spinAngle > 36000f || spinAngle < -36000f) spinAngle = Mathf.Repeat(spinAngle, 360f);

        // Steering angle
        Vector3 localVel = transform.InverseTransformDirection(rb != null ? rb.angularVelocity : Vector3.zero);
        currentSteerAngle = Mathf.Lerp(currentSteerAngle, Mathf.Clamp(localVel.y * 12f, -22f, 22f), Time.deltaTime * 10f);

        // Rotate wheels
        if (frontLeftWheel != null)
            frontLeftWheel.localRotation = Quaternion.Euler(0f, currentSteerAngle, 0f) * Quaternion.Euler(spinAngle, 0f, 0f);
        if (rearLeftWheel != null)
            rearLeftWheel.localRotation = Quaternion.Euler(spinAngle, 0f, 0f);
        if (frontRightWheel != null)
            frontRightWheel.localRotation = Quaternion.Euler(0f, currentSteerAngle, 0f) * Quaternion.Euler(0f, 180f, 0f) * Quaternion.Euler(-spinAngle, 0f, 0f);
        if (rearRightWheel != null)
            rearRightWheel.localRotation = Quaternion.Euler(0f, 180f, 0f) * Quaternion.Euler(-spinAngle, 0f, 0f);
    }
}
