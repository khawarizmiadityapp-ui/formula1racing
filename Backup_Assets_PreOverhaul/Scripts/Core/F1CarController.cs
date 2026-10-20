using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Formula 1 car physics controller using Rigidbody with optional WheelCollider or Raycast suspension.
/// Features aerodynamic downforce, ground effect, tire grip model, 8-gear sequential transmission
/// (auto/manual), DRS (ALA) and ERS (OTK/SET) systems, and full telemetry output.
///
/// Attach to: Root GameObject of the F1 car (must have Rigidbody).
/// Dependencies: None (standalone). Works with F1DRSERSSystem, F1TireModel, F1EngineModel.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class F1CarController : MonoBehaviour
{
    #region ──── ENUMS ────
    public enum SuspensionMode { Arcade, WheelCollider, Raycast }
    public enum TransmissionMode { Automatic, Manual }
    #endregion

    #region ──── INSPECTOR FIELDS ────

    [Header("═══ SUSPENSION MODE ═══")]
    [Tooltip("Arcade: Force-based (original). WheelCollider: Unity built-in. Raycast: Custom raycast suspension.")]
    [SerializeField] private SuspensionMode suspensionMode = SuspensionMode.Arcade;

    [Header("═══ ENGINE & DRIVETRAIN ═══")]
    [Tooltip("Peak engine power in kW (F1 ~735 kW / 1000 HP)")]
    [SerializeField] private float enginePowerKW = 735f;
    [Tooltip("Idle RPM")]
    [SerializeField] private float idleRPM = 3500f;
    [Tooltip("Maximum RPM (F1 regulation limit 15000)")]
    [SerializeField] private float maxRPM = 15000f;
    [Tooltip("RPM at which shift light activates")]
    [SerializeField] private float shiftLightRPM = 13500f;
    [Tooltip("Forward acceleration in m/s² (arcade mode)")]
    [SerializeField] private float forwardAcceleration = 55f;
    [Tooltip("Reverse acceleration in m/s²")]
    [SerializeField] private float reverseAcceleration = 22f;
    [Tooltip("Top forward speed in m/s (~350 km/h = 97.2 m/s)")]
    [SerializeField] private float maxForwardSpeed = 97.2f;
    [Tooltip("Top speed on grass in m/s (~65 km/h)")]
    [SerializeField] private float maxGrassSpeed = 18f;
    [Tooltip("Top reverse speed in m/s")]
    [SerializeField] private float maxReverseSpeed = 16f;
    [Tooltip("Braking deceleration")]
    [SerializeField] private float brakingStrength = 52f;

    [Header("═══ TRANSMISSION (8-SPEED) ═══")]
    [SerializeField] private TransmissionMode transmissionMode = TransmissionMode.Automatic;
    [Tooltip("Gear ratios for 8 forward gears + final drive")]
    [SerializeField] private float[] gearRatios = { 0f, 3.30f, 2.52f, 2.02f, 1.68f, 1.40f, 1.18f, 1.02f, 0.88f };
    [SerializeField] private float finalDriveRatio = 3.42f;
    [SerializeField] private float autoUpshiftRPM = 13800f;
    [SerializeField] private float autoDownshiftRPM = 7500f;
    [SerializeField] private float shiftDuration = 0.045f; // F1-style near-instant shifts

    [Header("═══ AERODYNAMICS ═══")]
    [Tooltip("Base downforce coefficient (N per (m/s)²)")]
    [SerializeField] private float downforceCoefficient = 3.8f;
    [Tooltip("Air drag coefficient")]
    [SerializeField] private float dragCoefficient = 0.72f;
    [Tooltip("Ground effect downforce extra at low ride height")]
    [SerializeField] private float groundEffectMultiplier = 1.45f;
    [Tooltip("Ground effect max distance from ground (m)")]
    [SerializeField] private float groundEffectMaxHeight = 0.15f;
    [Tooltip("Frontal area (m²)")]
    [SerializeField] private float frontalArea = 1.5f;
    [SerializeField] private float baseGroundGlue = 2200f;

    [Header("═══ STEERING & HANDLING ═══")]
    [SerializeField] private float steeringDegreesPerSecond = 110f;
    [SerializeField] private float steeringFadeAtSpeed = 45f;
    [Range(0.2f, 0.8f)]
    [SerializeField] private float minSteeringFactor = 0.38f;
    [SerializeField] private float lateralGripBase = 8.5f;

    [Header("═══ CENTER OF MASS & STABILITY ═══")]
    [SerializeField] private Vector3 centerOfMassOffset = new Vector3(0f, 0.15f, -0.05f);
    [SerializeField] private float uprightStabilizerTorque = 1500f;
    [SerializeField] private float coastDrag = 0.45f;
    [SerializeField] private float grassExtraDrag = 2.8f;

    [Header("═══ WHEELCOLLIDER REFERENCES (WheelCollider mode) ═══")]
    [SerializeField] private WheelCollider wheelFL;
    [SerializeField] private WheelCollider wheelFR;
    [SerializeField] private WheelCollider wheelRL;
    [SerializeField] private WheelCollider wheelRR;
    [SerializeField] private Transform wheelVisualFL;
    [SerializeField] private Transform wheelVisualFR;
    [SerializeField] private Transform wheelVisualRL;
    [SerializeField] private Transform wheelVisualRR;

    [Header("═══ RAYCAST SUSPENSION (Raycast mode) ═══")]
    [SerializeField] private Transform[] raycastWheelOrigins = new Transform[4];
    [SerializeField] private float raycastSuspensionRestLength = 0.35f;
    [SerializeField] private float raycastSpringStrength = 45000f;
    [SerializeField] private float raycastDamperStrength = 4500f;
    [SerializeField] private float raycastWheelRadius = 0.33f;

    [Header("═══ TRACK SAFETY ═══")]
    [SerializeField] private Vector3 defaultSpawnPosition = new Vector3(-8.0f, 0.05f, -94.0f);
    [SerializeField] private Vector3 defaultSpawnEuler = new Vector3(0f, 90f, 0f);
    [SerializeField] private float outOfBoundsY = -1.0f;
    [SerializeField] private float arenaMaxX = 265f;
    [SerializeField] private float arenaMaxZ = 215f;

    #endregion

    #region ──── RUNTIME STATE ────

    private Rigidbody rb;
    private bool isOnGrass;
    private float upsideDownTimer;
    private Vector3 lastTrackPosition;
    private Quaternion lastTrackRotation;

    // Input state
    private float mobileSteer, mobileThrottle, mobileBrake;
    private bool mobileHandbrake;

    // Transmission state
    private int currentGear = 1;
    private float currentRPM;
    private float shiftTimer;
    private bool isShifting;

    // Ground effect
    private float currentRideHeight = 0.1f;

    #endregion

    #region ──── PUBLIC TELEMETRY PROPERTIES ────

    /// <summary>Input throttle 0-1</summary>
    public float ThrottleInput { get; private set; }
    /// <summary>Input brake 0-1</summary>
    public float BrakeInput { get; private set; }
    /// <summary>Smoothed steer -1 to 1</summary>
    public float CurrentSteer { get; private set; }
    /// <summary>Handbrake active flag</summary>
    public bool HandbrakeActive { get; private set; }
    /// <summary>Currently reversing</summary>
    public bool IsReversing { get; private set; }
    /// <summary>Is car on grass surface</summary>
    public bool IsOnGrass => isOnGrass;
    /// <summary>Speed in KM/H</summary>
    public float SpeedKph => rb != null ? rb.linearVelocity.magnitude * 3.6f : 0f;
    /// <summary>Forward speed in m/s (signed)</summary>
    public float ForwardSpeed => rb != null ? Vector3.Dot(rb.linearVelocity, transform.forward) : 0f;
    /// <summary>Lateral speed in m/s (signed)</summary>
    public float LateralSpeed => rb != null ? Vector3.Dot(rb.linearVelocity, transform.right) : 0f;
    /// <summary>Speed as 0-1 ratio</summary>
    public float Speed01 => Mathf.Clamp01(SpeedKph / (maxForwardSpeed * 3.6f));
    /// <summary>Current gear (1-8, 0=R)</summary>
    public int CurrentGear => currentGear;
    /// <summary>Current engine RPM</summary>
    public float CurrentRPM => currentRPM;
    /// <summary>RPM as 0-1 ratio</summary>
    public float RPM01 => Mathf.InverseLerp(idleRPM, maxRPM, currentRPM);
    /// <summary>Idle RPM value</summary>
    public float IdleRPM => idleRPM;
    /// <summary>Max RPM value</summary>
    public float MaxRPM => maxRPM;
    /// <summary>Is shift light on</summary>
    public bool ShiftLightActive => currentRPM >= shiftLightRPM;
    /// <summary>Is currently shifting gears</summary>
    public bool IsShifting => isShifting;
    /// <summary>Maximum gear count</summary>
    public int MaxGears => gearRatios.Length - 1;
    /// <summary>Transmission auto/manual</summary>
    public TransmissionMode CurrentTransmissionMode => transmissionMode;
    /// <summary>Current ride height in meters</summary>
    public float RideHeight => currentRideHeight;
    /// <summary>Rigidbody reference</summary>
    public Rigidbody CarRigidbody => rb;
    /// <summary>Current downforce in Newtons</summary>
    public float CurrentDownforceN { get; private set; }
    /// <summary>Max forward speed setting</summary>
    public float MaxForwardSpeedMs => maxForwardSpeed;

    #endregion

    #region ──── EVENTS ────

    public event System.Action<int, int> OnGearChanged;   // oldGear, newGear
    public event System.Action OnShiftLightOn;
    public event System.Action OnRespawn;

    #endregion

    #region ──── LIFECYCLE ────

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.mass = 798f; // 2024 F1 minimum weight
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.centerOfMass = centerOfMassOffset;

        lastTrackPosition = transform.position;
        lastTrackRotation = transform.rotation;
        currentRPM = idleRPM;
    }

    private void Update()
    {
        ReadInputs();
        CheckOutOfBoundsAndUpsideDown();
        UpdateTransmission();
    }

    private void FixedUpdate()
    {
        if (rb == null) return;

        switch (suspensionMode)
        {
            case SuspensionMode.Arcade:
                FixedUpdate_Arcade();
                break;
            case SuspensionMode.WheelCollider:
                FixedUpdate_WheelCollider();
                break;
            case SuspensionMode.Raycast:
                FixedUpdate_Raycast();
                break;
        }

        ApplyAerodynamics();
        ApplyGroundEffect();
        ApplyStabilizer();
        DetectSurface();
    }

    #endregion

    #region ──── INPUT ────

    /// <summary>
    /// Feed mobile touch inputs from HUD buttons.
    /// </summary>
    public void SetMobileInput(float steer, float throttle, float brake, bool handbrake)
    {
        mobileSteer = Mathf.Clamp(steer, -1f, 1f);
        mobileThrottle = Mathf.Clamp01(throttle);
        mobileBrake = Mathf.Clamp01(brake);
        mobileHandbrake = handbrake;
    }

    private void ReadInputs()
    {
        float keyboardSteer = 0f, keyboardDrive = 0f;
        bool keyboardHandbrake = false, resetTriggered = false;
        bool shiftUpPressed = false, shiftDownPressed = false;

#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) keyboardDrive += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) keyboardDrive -= 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) keyboardSteer -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) keyboardSteer += 1f;
            if (kb.spaceKey.isPressed) keyboardHandbrake = true;
            if (kb.rKey.wasPressedThisFrame) resetTriggered = true;
            // Manual gear shift keys
            if (kb.eKey.wasPressedThisFrame) shiftUpPressed = true;
            if (kb.qKey.wasPressedThisFrame) shiftDownPressed = true;
            // Toggle transmission mode
            if (kb.tKey.wasPressedThisFrame) ToggleTransmissionMode();
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        try
        {
            keyboardSteer = Input.GetAxis("Horizontal");
            keyboardDrive = Input.GetAxis("Vertical");
            keyboardHandbrake = Input.GetKey(KeyCode.Space);
            if (Input.GetKeyDown(KeyCode.R)) resetTriggered = true;
            if (Input.GetKeyDown(KeyCode.E)) shiftUpPressed = true;
            if (Input.GetKeyDown(KeyCode.Q)) shiftDownPressed = true;
            if (Input.GetKeyDown(KeyCode.T)) ToggleTransmissionMode();
        }
        catch { }
#endif

        if (resetTriggered)
        {
            RespawnOnTrack();
            return;
        }

        if (shiftUpPressed) ShiftUp();
        if (shiftDownPressed) ShiftDown();

        float combinedSteer = Mathf.Clamp(keyboardSteer + mobileSteer, -1f, 1f);
        float combinedThrottle = Mathf.Clamp01(Mathf.Max(Mathf.Clamp01(keyboardDrive), mobileThrottle));
        float combinedBrake = Mathf.Clamp01(Mathf.Max(Mathf.Clamp01(-keyboardDrive), mobileBrake));
        bool combinedHandbrake = keyboardHandbrake || mobileHandbrake;

        CurrentSteer = Mathf.Lerp(CurrentSteer, combinedSteer, 1f - Mathf.Exp(-Time.deltaTime * 18f));
        ThrottleInput = combinedThrottle;
        BrakeInput = combinedBrake;
        HandbrakeActive = combinedHandbrake;
    }

    #endregion

    #region ──── TRANSMISSION ────

    /// <summary>
    /// Gear speed thresholds in KM/H for 8 forward gears.
    /// </summary>
    private static readonly float[] GearTopSpeeds = { 0f, 62f, 108f, 155f, 202f, 248f, 292f, 332f, 365f };

    private void UpdateTransmission()
    {
        float speedKph = SpeedKph;

        // Handle shift timer
        if (isShifting)
        {
            shiftTimer -= Time.deltaTime;
            if (shiftTimer <= 0f)
            {
                isShifting = false;
            }
        }

        // Calculate RPM based on gear and speed
        float gearMinSpeed = currentGear > 1 ? GearTopSpeeds[currentGear - 1] : 0f;
        float gearMaxSpeed = currentGear > 0 && currentGear < GearTopSpeeds.Length
            ? GearTopSpeeds[currentGear]
            : 25f;
        float gearProgress = Mathf.Clamp01(
            (speedKph - gearMinSpeed) / Mathf.Max(5f, gearMaxSpeed - gearMinSpeed)
        );

        float baseRPM = Mathf.Lerp(
            currentGear == 1 ? idleRPM : 6500f,
            maxRPM,
            gearProgress
        );
        float targetRPM = Mathf.Clamp(
            baseRPM + ThrottleInput * 1500f,
            idleRPM,
            maxRPM
        );

        // Idle fluctuation
        if (speedKph < 2f && ThrottleInput < 0.05f)
        {
            targetRPM = idleRPM + Mathf.Sin(Time.time * 2.5f) * 80f;
        }

        // During shift, drop RPM briefly
        if (isShifting)
        {
            targetRPM = Mathf.Lerp(currentRPM, currentRPM * 0.7f, 0.3f);
        }

        float smoothing = (targetRPM > currentRPM) ? 10f : 7f;
        currentRPM = Mathf.Lerp(currentRPM, targetRPM, 1f - Mathf.Exp(-Time.deltaTime * smoothing));

        // Shift light
        if (currentRPM >= shiftLightRPM)
        {
            OnShiftLightOn?.Invoke();
        }

        // Automatic transmission
        if (transmissionMode == TransmissionMode.Automatic && !isShifting && !IsReversing)
        {
            if (currentRPM >= autoUpshiftRPM && currentGear < MaxGears)
            {
                ShiftUp();
            }
            else if (currentRPM <= autoDownshiftRPM && currentGear > 1 && speedKph > 10f)
            {
                ShiftDown();
            }
        }
    }

    public void ShiftUp()
    {
        if (currentGear < MaxGears && !isShifting)
        {
            int oldGear = currentGear;
            currentGear++;
            isShifting = true;
            shiftTimer = shiftDuration;
            OnGearChanged?.Invoke(oldGear, currentGear);
        }
    }

    public void ShiftDown()
    {
        if (currentGear > 1 && !isShifting)
        {
            int oldGear = currentGear;
            currentGear--;
            isShifting = true;
            shiftTimer = shiftDuration;
            OnGearChanged?.Invoke(oldGear, currentGear);
        }
    }

    public void ToggleTransmissionMode()
    {
        transmissionMode = transmissionMode == TransmissionMode.Automatic
            ? TransmissionMode.Manual
            : TransmissionMode.Automatic;
    }

    #endregion

    #region ──── ARCADE PHYSICS ────

    private void FixedUpdate_Arcade()
    {
        float fwdSpeed = ForwardSpeed;
        bool movingForward = fwdSpeed > 0.5f;
        bool movingBackward = fwdSpeed < -0.5f;
        float currentMaxSpeed = isOnGrass ? maxGrassSpeed : maxForwardSpeed;

        // Throttle / Brake / Reverse
        if (ThrottleInput > 0.01f)
        {
            IsReversing = false;
            if (movingBackward)
            {
                rb.AddForce(transform.forward * ThrottleInput * brakingStrength, ForceMode.Acceleration);
            }
            else if (fwdSpeed < currentMaxSpeed)
            {
                float accel = isOnGrass ? forwardAcceleration * 0.45f : forwardAcceleration;
                // Reduce acceleration at higher RPMs for realism
                float rpmFactor = 1f - (RPM01 * 0.15f);
                rb.AddForce(transform.forward * ThrottleInput * accel * rpmFactor, ForceMode.Acceleration);
            }
        }
        else if (BrakeInput > 0.01f)
        {
            if (movingForward)
            {
                rb.AddForce(-transform.forward * BrakeInput * brakingStrength, ForceMode.Acceleration);
                IsReversing = false;
            }
            else if (fwdSpeed > -maxReverseSpeed)
            {
                IsReversing = true;
                rb.AddForce(-transform.forward * BrakeInput * reverseAcceleration, ForceMode.Acceleration);
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

        // Speed-dependent steering
        float speedFactor = Mathf.Lerp(1f, minSteeringFactor,
            Mathf.Clamp01(Mathf.Abs(fwdSpeed) / steeringFadeAtSpeed));
        float steerDirection = fwdSpeed >= -0.5f ? 1f : -1f;
        float yawDelta = CurrentSteer * steeringDegreesPerSecond * speedFactor
            * steerDirection * Time.fixedDeltaTime;
        rb.MoveRotation(rb.rotation * Quaternion.Euler(0f, yawDelta, 0f));

        // Lateral grip
        Vector3 localVel = transform.InverseTransformDirection(rb.linearVelocity);
        float currentGrip = isOnGrass ? lateralGripBase * 0.45f : lateralGripBase;
        localVel.x = Mathf.Lerp(localVel.x, 0f, currentGrip * Time.fixedDeltaTime);
        rb.linearVelocity = transform.TransformDirection(localVel);

        // Coast drag
        float activeDrag = coastDrag + (isOnGrass ? grassExtraDrag : 0f);
        if (ThrottleInput < 0.02f && BrakeInput < 0.02f)
        {
            rb.AddForce(-rb.linearVelocity * activeDrag, ForceMode.Acceleration);
        }
        else if (isOnGrass)
        {
            rb.AddForce(-rb.linearVelocity * (grassExtraDrag * 0.4f), ForceMode.Acceleration);
        }
    }

    #endregion

    #region ──── WHEELCOLLIDER PHYSICS ────

    private void FixedUpdate_WheelCollider()
    {
        if (wheelFL == null || wheelFR == null || wheelRL == null || wheelRR == null) return;

        float motorTorque = 0f;
        float brakeTorque = 0f;
        float steerAngle = CurrentSteer * 22f *
            Mathf.Lerp(1f, minSteeringFactor, Mathf.Clamp01(SpeedKph / (steeringFadeAtSpeed * 3.6f)));

        if (ThrottleInput > 0.01f)
        {
            IsReversing = false;
            float powerPerWheel = (enginePowerKW * 1000f * ThrottleInput) /
                (Mathf.Max(1f, SpeedKph / 3.6f) * 2f);
            motorTorque = powerPerWheel;
        }
        else if (BrakeInput > 0.01f && ForwardSpeed > 0.5f)
        {
            brakeTorque = brakingStrength * 100f * BrakeInput;
            IsReversing = false;
        }
        else if (BrakeInput > 0.01f)
        {
            IsReversing = true;
            motorTorque = -reverseAcceleration * 50f * BrakeInput;
        }

        // Apply to wheels
        wheelFL.steerAngle = steerAngle;
        wheelFR.steerAngle = steerAngle;

        // Rear-wheel drive
        wheelRL.motorTorque = motorTorque;
        wheelRR.motorTorque = motorTorque;

        // Brakes on all 4 wheels
        wheelFL.brakeTorque = brakeTorque;
        wheelFR.brakeTorque = brakeTorque;
        wheelRL.brakeTorque = brakeTorque + (HandbrakeActive ? brakingStrength * 200f : 0f);
        wheelRR.brakeTorque = brakeTorque + (HandbrakeActive ? brakingStrength * 200f : 0f);

        // Sync wheel visuals
        SyncWheelVisual(wheelFL, wheelVisualFL);
        SyncWheelVisual(wheelFR, wheelVisualFR);
        SyncWheelVisual(wheelRL, wheelVisualRL);
        SyncWheelVisual(wheelRR, wheelVisualRR);
    }

    private void SyncWheelVisual(WheelCollider col, Transform visual)
    {
        if (col == null || visual == null) return;
        col.GetWorldPose(out Vector3 pos, out Quaternion rot);
        visual.position = pos;
        visual.rotation = rot;
    }

    #endregion

    #region ──── RAYCAST SUSPENSION ────

    private void FixedUpdate_Raycast()
    {
        if (raycastWheelOrigins == null || raycastWheelOrigins.Length < 4) return;

        for (int i = 0; i < 4; i++)
        {
            Transform origin = raycastWheelOrigins[i];
            if (origin == null) continue;

            float maxLength = raycastSuspensionRestLength + raycastWheelRadius;
            Ray ray = new Ray(origin.position, -origin.up);

            if (Physics.Raycast(ray, out RaycastHit hit, maxLength + 0.1f))
            {
                float currentLength = hit.distance - raycastWheelRadius;
                float compression = raycastSuspensionRestLength - currentLength;

                // Spring force
                float springForce = compression * raycastSpringStrength;

                // Damper force
                float velAlongSpring = Vector3.Dot(rb.GetPointVelocity(origin.position), origin.up);
                float damperForce = -velAlongSpring * raycastDamperStrength;

                float totalForce = springForce + damperForce;
                rb.AddForceAtPosition(origin.up * totalForce, origin.position, ForceMode.Force);

                // Drive force (rear wheels only, index 2 and 3)
                if (i >= 2 && ThrottleInput > 0.01f)
                {
                    float drivePower = forwardAcceleration * ThrottleInput * rb.mass * 0.5f;
                    rb.AddForceAtPosition(transform.forward * drivePower, origin.position, ForceMode.Force);
                }

                // Lateral grip at each wheel
                Vector3 wheelVel = rb.GetPointVelocity(origin.position);
                Vector3 lateralVel = Vector3.Project(wheelVel, origin.right);
                rb.AddForceAtPosition(-lateralVel * lateralGripBase * rb.mass * 0.25f,
                    origin.position, ForceMode.Force);
            }
        }

        // Steering via torque
        float speedFactor = Mathf.Lerp(1f, minSteeringFactor,
            Mathf.Clamp01(Mathf.Abs(ForwardSpeed) / steeringFadeAtSpeed));
        float yawDelta = CurrentSteer * steeringDegreesPerSecond * speedFactor * Time.fixedDeltaTime;
        rb.MoveRotation(rb.rotation * Quaternion.Euler(0f, yawDelta, 0f));
    }

    #endregion

    #region ──── AERODYNAMICS & GROUND EFFECT ────

    private void ApplyAerodynamics()
    {
        float speedSqr = rb.linearVelocity.sqrMagnitude;

        // Downforce: F = Cl * 0.5 * rho * A * v²
        float aeroDownforce = downforceCoefficient * speedSqr;
        CurrentDownforceN = aeroDownforce;
        rb.AddForce(-transform.up * (aeroDownforce + baseGroundGlue), ForceMode.Force);

        // Air drag: opposite to velocity
        float airDrag = dragCoefficient * 0.5f * 1.225f * frontalArea * speedSqr;
        if (rb.linearVelocity.sqrMagnitude > 0.01f)
        {
            rb.AddForce(-rb.linearVelocity.normalized * airDrag, ForceMode.Force);
        }
    }

    private void ApplyGroundEffect()
    {
        // Raycast to detect ride height for ground effect calculation
        Ray ray = new Ray(transform.position + Vector3.up * 0.3f, Vector3.down);
        if (Physics.Raycast(ray, out RaycastHit hit, 2.0f))
        {
            currentRideHeight = hit.distance - 0.3f;

            if (currentRideHeight < groundEffectMaxHeight && !isOnGrass)
            {
                float effectRatio = 1f - Mathf.Clamp01(currentRideHeight / groundEffectMaxHeight);
                float extraDownforce = downforceCoefficient * rb.linearVelocity.sqrMagnitude
                    * (groundEffectMultiplier - 1f) * effectRatio;
                rb.AddForce(-transform.up * extraDownforce, ForceMode.Force);
            }
        }
    }

    private void ApplyStabilizer()
    {
        Vector3 predictedUp = Quaternion.AngleAxis(
            rb.angularVelocity.magnitude * Mathf.Rad2Deg * 0.1f,
            rb.angularVelocity
        ) * transform.up;
        Vector3 torqueCorrection = Vector3.Cross(predictedUp, Vector3.up);
        rb.AddTorque(torqueCorrection * uprightStabilizerTorque, ForceMode.Force);
    }

    #endregion

    #region ──── SURFACE DETECTION ────

    private void DetectSurface()
    {
        Ray ray = new Ray(transform.position + Vector3.up * 0.5f, Vector3.down);
        if (Physics.Raycast(ray, out RaycastHit hit, 2.0f))
        {
            string hitName = hit.collider.gameObject.name.ToLower();
            if (hitName.Contains("ground") || hitName.Contains("grass"))
            {
                isOnGrass = true;
            }
            else
            {
                isOnGrass = false;
                if (rb.linearVelocity.magnitude > 2f)
                {
                    lastTrackPosition = transform.position;
                    lastTrackRotation = Quaternion.LookRotation(transform.forward, Vector3.up);
                }
            }
        }
    }

    #endregion

    #region ──── RESPAWN & SAFETY ────

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
        currentGear = 1;
        currentRPM = idleRPM;

        OnRespawn?.Invoke();
    }

    private void CheckOutOfBoundsAndUpsideDown()
    {
        Vector3 pos = transform.position;

        if (pos.y < outOfBoundsY || Mathf.Abs(pos.x) > arenaMaxX || Mathf.Abs(pos.z) > arenaMaxZ)
        {
            RespawnOnTrack();
            return;
        }

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
    }

    #endregion

    #region ──── DRS/ERS INTEGRATION ────

    /// <summary>
    /// Called by F1DRSERSSystem to apply DRS speed boost.
    /// </summary>
    public void ApplyDRSBoost(float extraSpeedMultiplier)
    {
        if (rb == null) return;
        // Reduce drag when DRS is active
        float drsForce = forwardAcceleration * extraSpeedMultiplier * ThrottleInput;
        rb.AddForce(transform.forward * drsForce, ForceMode.Acceleration);
    }

    /// <summary>
    /// Called by F1DRSERSSystem to apply ERS power boost.
    /// </summary>
    public void ApplyERSBoost(float boostPower)
    {
        if (rb == null) return;
        rb.AddForce(transform.forward * boostPower, ForceMode.Acceleration);
    }

    #endregion
}
