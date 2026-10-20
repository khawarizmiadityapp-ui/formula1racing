using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Arcade vehicle physics controller for the Apex V12 Mercedes-inspired Formula racing car.
/// Features high-speed aerodynamic stability, downforce, progressive lateral grip,
/// off-track grass resistance, automatic out-of-bounds recovery, and manual track reset.
/// Supports both Unity's new Input System (Keyboard.current) and Android touch HUD buttons.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class FormulaCarArcadeController : MonoBehaviour
{
    [Header("Drive & Engine")]
    [Tooltip("Forward acceleration in m/s^2")]
    [SerializeField] private float forwardAcceleration = 54f;
    [Tooltip("Reverse acceleration in m/s^2")]
    [SerializeField] private float reverseAcceleration = 24f;
    [Tooltip("Top forward speed on asphalt in m/s (102.5 m/s ~ 369 km/h, Target: 360-370 km/h)")]
    [SerializeField] private float maxForwardSpeed = 102.5f;
    [Tooltip("Top speed when driving on grass off-track (22 m/s ~ 80 km/h)")]
    [SerializeField] private float maxGrassSpeed = 22f;
    [Tooltip("Top reverse speed in m/s (22 m/s ~ 79 km/h)")]
    [SerializeField] private float maxReverseSpeed = 22f;
    [Tooltip("Braking deceleration strength")]
    [SerializeField] private float brakingStrength = 68f;
    [Tooltip("Natural rolling friction / coasting drag on asphalt")]
    [SerializeField] private float coastDrag = 0.38f;
    [Tooltip("Additional rolling drag when on grass")]
    [SerializeField] private float grassExtraDrag = 2.5f;

    [Header("Steering & Handling")]
    [Tooltip("Base steering speed in degrees/second at low speeds")]
    [SerializeField] private float steeringDegreesPerSecond = 92f;
    [Tooltip("Speed in m/s at which steering responsiveness begins to fade for high-speed stability")]
    [SerializeField] private float steeringFadeAtSpeed = 50f;
    [Tooltip("Minimum steering factor at maximum speed")]
    [SerializeField] [Range(0.2f, 0.8f)] private float minSteeringFactor = 0.38f;
    [Tooltip("Lateral grip factor to dampen sideways sliding")]
    [SerializeField] private float lateralGrip = 8.2f;
    [Tooltip("Downforce multiplier based on speed squared")]
    [SerializeField] private float downforce = 0.75f;
    [Tooltip("Baseline road contact force to keep car grounded over curbs")]
    [SerializeField] private float baseGroundGlue = 1800f;

    [Header("Center of Mass & Stability")]
    [SerializeField] private Vector3 centerOfMassOffset = new Vector3(0f, 0.15f, -0.05f);
    [SerializeField] private float uprightStabilizerTorque = 1500f;

    [Header("Track Safety & Boundaries")]
    [SerializeField] private Vector3 defaultSpawnPosition = new Vector3(-340.0f, 0.45f, -500.0f);
    [SerializeField] private Vector3 defaultSpawnEuler = new Vector3(0f, 90f, 0f);
    [SerializeField] private float outOfBoundsY = -5.0f;
    [SerializeField] private float arenaMaxX = 2500f;
    [SerializeField] private float arenaMaxZ = 2500f;

    private Rigidbody rb;
    private bool isOnGrass;
    private float upsideDownTimer;
    private Vector3 lastTrackPosition;
    private Quaternion lastTrackRotation;
    private readonly RaycastHit[] raycastBuffer = new RaycastHit[16];

    // Mobile touch inputs
    private float mobileSteer;
    private float mobileThrottle;
    private float mobileBrake;
    private bool mobileHandbrake;

    // Public properties exposed for HUD, Wheel Visuals, and Audio
    public float ThrottleInput { get; private set; }
    public float BrakeInput { get; private set; }
    public float CurrentSteer { get; private set; }
    public bool HandbrakeActive { get; private set; }
    public bool IsReversing { get; private set; }
    public bool IsOnGrass => isOnGrass;

    public float SpeedKph => rb != null ? rb.linearVelocity.magnitude * 3.6f : 0f;
    public float ForwardSpeed => rb != null ? Vector3.Dot(rb.linearVelocity, transform.forward) : 0f;
    public float LateralSpeed => rb != null ? Vector3.Dot(rb.linearVelocity, transform.right) : 0f;
    public float Speed01 => Mathf.Clamp01(SpeedKph / (maxForwardSpeed * 3.6f));

    private void Awake()
    {
        Application.targetFrameRate = 60;
        rb = GetComponent<Rigidbody>();
        rb.mass = 780f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.centerOfMass = centerOfMassOffset;

        // Apply a zero-bounciness physics material to eliminate collision catapult impulses
        PhysicsMaterial carMat = new PhysicsMaterial("F1_CarPhysicsMaterial")
        {
            dynamicFriction = 0.65f,
            staticFriction = 0.65f,
            bounciness = 0.0f,
            frictionCombine = PhysicsMaterialCombine.Average,
            bounceCombine = PhysicsMaterialCombine.Minimum
        };
        foreach (var col in GetComponentsInChildren<Collider>())
        {
            col.material = carMat;
        }

        lastTrackPosition = transform.position;
        lastTrackRotation = transform.rotation;
    }

    /// <summary>
    /// Feed mobile touch HUD inputs from onscreen buttons.
    /// </summary>
    public void SetMobileInput(float steer, float throttle, float brake, bool handbrake)
    {
        mobileSteer = Mathf.Clamp(steer, -1f, 1f);
        mobileThrottle = Mathf.Clamp01(throttle);
        mobileBrake = Mathf.Clamp01(brake);
        mobileHandbrake = handbrake;
    }

    /// <summary>
    /// Configure the default respawn / starting position on the grid.
    /// </summary>
    public void SetSpawnPoint(Vector3 pos, Vector3 euler)
    {
        defaultSpawnPosition = pos;
        defaultSpawnEuler = euler;
        lastTrackPosition = pos;
        lastTrackRotation = Quaternion.Euler(euler);
    }

    /// <summary>
    /// Instantly recover and respawn the car upright on the track.
    /// </summary>
    public void RespawnOnTrack()
    {
        if (rb == null) return;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        Vector3 targetPos = (lastTrackPosition.sqrMagnitude > 1f && lastTrackPosition.y > -0.5f)
            ? lastTrackPosition + Vector3.up * 0.15f
            : defaultSpawnPosition;
        Quaternion targetRot = (lastTrackRotation != Quaternion.identity)
            ? lastTrackRotation
            : Quaternion.Euler(defaultSpawnEuler);

        transform.position = targetPos;
        transform.rotation = targetRot;
        upsideDownTimer = 0f;
    }

    private void Update()
    {
        ReadInputs();
        CheckOutOfBoundsAndUpsideDown();
    }

    private void ReadInputs()
    {
        float keyboardSteer = 0f;
        float keyboardDrive = 0f;
        bool keyboardHandbrake = false;
        bool resetTriggered = false;

#if ENABLE_INPUT_SYSTEM
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null)
        {
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) keyboardDrive += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) keyboardDrive -= 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) keyboardSteer -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) keyboardSteer += 1f;
            if (kb.spaceKey.isPressed) keyboardHandbrake = true;
            if (kb.rKey.wasPressedThisFrame) resetTriggered = true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        try
        {
            float legacySteer = Input.GetAxis("Horizontal");
            float legacyDrive = Input.GetAxis("Vertical");
            if (Mathf.Abs(legacySteer) > 0.05f) keyboardSteer = legacySteer;
            if (Mathf.Abs(legacyDrive) > 0.05f) keyboardDrive = legacyDrive;
            if (Input.GetKey(KeyCode.Space)) keyboardHandbrake = true;
            if (Input.GetKeyDown(KeyCode.R)) resetTriggered = true;
        }
        catch { }
#endif

        if (resetTriggered)
        {
            RespawnOnTrack();
            return;
        }

        // Combine keyboard and mobile touch inputs
        float combinedSteer = Mathf.Clamp(keyboardSteer + mobileSteer, -1f, 1f);
        float combinedThrottle = Mathf.Clamp01(Mathf.Max(Mathf.Clamp01(keyboardDrive), mobileThrottle));
        float combinedBrake = Mathf.Clamp01(Mathf.Max(Mathf.Clamp01(-keyboardDrive), mobileBrake));
        bool combinedHandbrake = keyboardHandbrake || mobileHandbrake;

        CurrentSteer = Mathf.Lerp(CurrentSteer, combinedSteer, 1f - Mathf.Exp(-Time.deltaTime * 18f));
        ThrottleInput = combinedThrottle;
        BrakeInput = combinedBrake;
        HandbrakeActive = combinedHandbrake;
    }

    private void CheckOutOfBoundsAndUpsideDown()
    {
        Vector3 pos = transform.position;

        // Auto respawn if fallen below map or exited outer arena limits
        if (pos.y < outOfBoundsY || Mathf.Abs(pos.x) > arenaMaxX || Mathf.Abs(pos.z) > arenaMaxZ)
        {
            RespawnOnTrack();
            return;
        }

        // Upside down detection (if car rolled over and stuck for > 1.8 seconds)
        if (Vector3.Dot(transform.up, Vector3.up) < 0.15f)
        {
            upsideDownTimer += Time.deltaTime;
            if (upsideDownTimer > 1.8f)
            {
                RespawnOnTrack();
            }
        }
        else
        {
            upsideDownTimer = 0f;
        }

        // Detect surface underneath (Road vs Grass) using non-allocating raycast
        int hitCount = Physics.RaycastNonAlloc(transform.position + Vector3.up * 0.8f, Vector3.down, raycastBuffer, 3.0f);
        bool foundSurface = false;
        bool surfaceIsGrass = false;
        for (int i = 0; i < hitCount; i++)
        {
            var h = raycastBuffer[i];
            if (h.collider != null && h.collider.transform.root != transform.root && !h.collider.isTrigger)
            {
                string hitName = h.collider.gameObject.name.ToLower();
                // Explicit grass detection (do NOT treat Baku_City_Ground or city asphalt as grass)
                if (hitName.Contains("grass") || hitName.Contains("turf") || hitName.Contains("lawn"))
                {
                    surfaceIsGrass = true;
                }
                else
                {
                    surfaceIsGrass = false;
                }
                foundSurface = true;
                break;
            }
        }

        if (foundSurface)
        {
            isOnGrass = surfaceIsGrass;
            if (!isOnGrass && rb.linearVelocity.magnitude > 2f && Vector3.Dot(transform.up, Vector3.up) > 0.85f && Mathf.Abs(rb.linearVelocity.y) < 2f)
            {
                lastTrackPosition = transform.position;
                lastTrackRotation = Quaternion.LookRotation(transform.forward, Vector3.up);
            }
        }
    }

    private void FixedUpdate()
    {
        if (rb == null) return;

        // Ground detection in FixedUpdate using non-allocating raycast
        bool isGrounded = false;
        Vector3 groundNormal = Vector3.up;
        int fixedHitCount = Physics.RaycastNonAlloc(transform.position + Vector3.up * 0.6f, Vector3.down, raycastBuffer, 1.4f, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < fixedHitCount; i++)
        {
            var h = raycastBuffer[i];
            if (h.collider != null && h.collider.transform.root != transform.root)
            {
                isGrounded = true;
                groundNormal = h.normal;
                break;
            }
        }

        float fwdSpeed = ForwardSpeed;
        bool movingForward = fwdSpeed > 0.5f;
        bool movingBackward = fwdSpeed < -0.5f;

        // Effective speed limit (dramatically reduced if driving on grass)
        float currentMaxSpeed = isOnGrass ? maxGrassSpeed : maxForwardSpeed;

        // Drive / Brake / Reverse logic
        if (ThrottleInput > 0.01f)
        {
            IsReversing = false;
            if (movingBackward)
            {
                // Active braking while rolling backwards
                rb.AddForce(transform.forward * ThrottleInput * brakingStrength, ForceMode.Acceleration);
            }
            else if (fwdSpeed < currentMaxSpeed)
            {
                // Progressive F1 acceleration curve: high punch at low gear/speed, progressive taper towards 360+ km/h
                float speedRatio = Mathf.Clamp01(fwdSpeed / currentMaxSpeed);
                float progressiveFactor = Mathf.Lerp(1.0f, 0.32f, speedRatio * speedRatio);
                float accel = (isOnGrass ? forwardAcceleration * 0.40f : forwardAcceleration) * progressiveFactor;
                if (!isGrounded) accel *= 0.2f;
                rb.AddForce(transform.forward * ThrottleInput * accel, ForceMode.Acceleration);
            }
        }
        else if (BrakeInput > 0.01f)
        {
            if (movingForward)
            {
                // Active forward braking
                rb.AddForce(-transform.forward * BrakeInput * brakingStrength, ForceMode.Acceleration);
                IsReversing = false;
            }
            else if (fwdSpeed > -maxReverseSpeed)
            {
                // Reverse gear
                IsReversing = true;
                float revAccel = reverseAcceleration;
                if (!isGrounded) revAccel *= 0.2f;
                rb.AddForce(-transform.forward * BrakeInput * revAccel, ForceMode.Acceleration);
            }
        }
        else
        {
            IsReversing = false;
        }

        // Handbrake
        if (HandbrakeActive)
        {
            Vector3 horizVel = Vector3.ProjectOnPlane(rb.linearVelocity, transform.up);
            if (horizVel.sqrMagnitude > 0.1f)
            {
                rb.AddForce(-horizVel.normalized * brakingStrength * 1.25f, ForceMode.Acceleration);
            }
        }

        // Steering: rotate around car's local up vector
        float speedFactor = Mathf.Lerp(1f, minSteeringFactor, Mathf.Clamp01(Mathf.Abs(fwdSpeed) / steeringFadeAtSpeed));
        float steerDirection = fwdSpeed >= -0.5f ? 1f : -1f;
        float steerAuthority = isGrounded ? 1.0f : 0.25f;
        float yawDelta = CurrentSteer * steeringDegreesPerSecond * speedFactor * steerDirection * steerAuthority * Time.fixedDeltaTime;

        Quaternion rotDelta = Quaternion.AngleAxis(yawDelta, transform.up);
        rb.MoveRotation(rotDelta * rb.rotation);

        // Damp yaw angular velocity so PhysX doesn't fight MoveRotation
        Vector3 angVel = rb.angularVelocity;
        angVel.y = Mathf.Lerp(angVel.y, 0f, 15f * Time.fixedDeltaTime);
        rb.angularVelocity = angVel;

        // Lateral grip: damp sideways velocity to give tight, responsive arcade cornering
        if (isGrounded)
        {
            Vector3 localVel = transform.InverseTransformDirection(rb.linearVelocity);
            float currentGrip = isOnGrass ? lateralGrip * 0.45f : lateralGrip;
            localVel.x = Mathf.Lerp(localVel.x, 0f, currentGrip * Time.fixedDeltaTime);
            rb.linearVelocity = transform.TransformDirection(localVel);
        }

        // Aerodynamic downforce & ground glue - firmly anchor car to the track
        float uprightDot = Vector3.Dot(transform.up, Vector3.up);
        if (uprightDot > 0.2f)
        {
            // Aerodynamic downforce pushes down onto car chassis
            float speedSqr = rb.linearVelocity.sqrMagnitude;
            float aeroDownforce = Mathf.Min(downforce * speedSqr, 14000f);
            rb.AddForce(-transform.up * aeroDownforce, ForceMode.Force);

            // Base ground glue: always pulls DOWN towards the track, never up into the sky
            if (isGrounded)
            {
                Vector3 downVector = Vector3.Dot(groundNormal, Vector3.up) > 0.5f ? -groundNormal : -Vector3.up;
                rb.AddForce(downVector * baseGroundGlue, ForceMode.Force);
            }
        }

        // Anti-Launch: Clamp / damp vertical velocity spikes caused by hitting curbs or mesh edges
        if (isGrounded)
        {
            Vector3 curVel = rb.linearVelocity;
            if (curVel.y > 1.8f)
            {
                curVel.y = Mathf.Lerp(curVel.y, 0.5f, 25f * Time.fixedDeltaTime);
                rb.linearVelocity = curVel;
            }
        }
        else
        {
            // If in mid-air, clamp any absurd collision glitch launch velocity
            Vector3 curVel = rb.linearVelocity;
            if (curVel.y > 6.0f)
            {
                curVel.y = 6.0f;
                rb.linearVelocity = curVel;
            }
        }

        // Natural coasting drag on asphalt + heavy rolling resistance on grass
        float activeDrag = coastDrag + (isOnGrass ? grassExtraDrag : 0f);
        if (ThrottleInput < 0.02f && BrakeInput < 0.02f)
        {
            rb.AddForce(-rb.linearVelocity * activeDrag, ForceMode.Acceleration);
        }
        else if (isOnGrass)
        {
            rb.AddForce(-rb.linearVelocity * (grassExtraDrag * 0.4f), ForceMode.Acceleration);
        }

        // Stable PD Upright Stabilizer (Anti-Roll & Anti-Pitch)
        // Keeps car level without positive feedback explosion or division by zero
        Vector3 targetUp = isGrounded && Vector3.Dot(groundNormal, Vector3.up) > 0.6f ? groundNormal : Vector3.up;
        Vector3 tiltCorrection = Vector3.Cross(transform.up, targetUp);

        // Isolate roll and pitch angular velocity (exclude yaw around transform.up)
        Vector3 yawComp = Vector3.Project(rb.angularVelocity, transform.up);
        Vector3 rollPitchAngVel = rb.angularVelocity - yawComp;

        // PD control: Proportional restoring torque + Derivative damping torque
        Vector3 restoringTorque = tiltCorrection * uprightStabilizerTorque;
        Vector3 dampingTorque = -rollPitchAngVel * (uprightStabilizerTorque * 0.35f);
        rb.AddTorque(restoringTorque + dampingTorque, ForceMode.Force);
    }
}
