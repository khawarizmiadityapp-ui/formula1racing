using UnityEngine;

/// <summary>
/// Professional Formula Cockpit and Follow Camera.
/// Default view is a first-person cockpit perspective matching the reference video:
/// - Inside the cockpit behind the halo and driver visor.
/// - Shows cockpit rim, steering dashboard, halo arch & central strut, front nose, side mirrors, and front tires.
/// - Front tires are clearly visible turning with steering and rotating with speed.
/// - Rigid chassis lock for 1st-person views (zero lag/clipping), subtle head-inertia, centrifugal roll tilt into corners, and apex look-ahead.
/// - Seamless cycling between Cockpit, T-Cam, Chase Close, and Chase Far via key 'C' or HUD button.
/// - Fully compatible with both the new Input System and legacy Input Manager without exceptions.
/// </summary>
[RequireComponent(typeof(Camera))]
public class F1CockpitCamera : MonoBehaviour
{
    public enum CameraMode
    {
        Cockpit,    // 1st person inside cockpit behind halo (Default)
        TCam,       // Above driver helmet / halo airbox
        ChaseClose, // Dynamic close 3rd person chase
        ChaseFar    // Classic broadcast 3rd person chase
    }

    [Header("═══ TARGET ═══")]
    [SerializeField] private Transform target;

    [Header("═══ CAMERA MODE ═══")]
    [SerializeField] private CameraMode currentMode = CameraMode.Cockpit;

    [Header("═══ COCKPIT VIEW (Inside Cockpit / Behind Halo) ═══")]
    [Tooltip("Camera position inside cockpit at driver eye level")]
    [SerializeField] private Vector3 cockpitOffset = new Vector3(0f, 0.84f, 0.12f);
    [Tooltip("Target look-at point forward over the nose cone")]
    [SerializeField] private Vector3 cockpitLookOffset = new Vector3(0f, 0.78f, 5.0f);
    [SerializeField] private float cockpitBaseFov = 74f;
    [SerializeField] private float cockpitMaxFov = 86f;

    [Header("═══ T-CAM VIEW (Above Halo) ═══")]
    [SerializeField] private Vector3 tCamOffset = new Vector3(0f, 1.25f, -0.30f);
    [SerializeField] private Vector3 tCamLookOffset = new Vector3(0f, 0.90f, 4.0f);
    [SerializeField] private float tCamBaseFov = 68f;
    [SerializeField] private float tCamMaxFov = 80f;

    [Header("═══ CHASE VIEW (3rd Person) ═══")]
    [SerializeField] private Vector3 chaseOffset = new Vector3(0f, 1.95f, -4.8f);
    [SerializeField] private Vector3 chaseLookOffset = new Vector3(0f, 0.90f, 2.5f);
    [SerializeField] private float chaseBaseFov = 62f;
    [SerializeField] private float chaseMaxFov = 75f;

    [Header("═══ DYNAMICS & SHAKE ═══")]
    [SerializeField] private float shakeSpeedThresholdKph = 140f;
    [SerializeField] private float maxShakeAmount = 0.018f;
    [SerializeField] private float shakeFrequency = 26f;
    [SerializeField] private float maxRollAngle = 3.5f;
    [SerializeField] private float rollSmoothing = 7f;
    [SerializeField] private float apexLookFactor = 4.5f;

    [Header("═══ SMOOTHING ═══")]
    [SerializeField] private float positionSmooth = 16f;
    [SerializeField] private float rotationSmooth = 18f;
    [SerializeField] private float topSpeedKph = 360f;

    private Camera cam;
    private Rigidbody targetRb;
    private FormulaCarArcadeController arcadeController;
    private float shakeNoiseSeed;
    private float currentRoll;
    private float currentApexYaw;
    private bool isSnapped = false;

    public CameraMode CurrentMode => currentMode;
    public Transform Target => target;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        shakeNoiseSeed = Random.value * 100f;
        FindTarget();
    }

    private void Start()
    {
        FindTarget();
        SnapToTarget();
    }

    private void OnEnable()
    {
        isSnapped = false;
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            FindTarget();
            if (target == null) return;
        }

        // On first frame target is found, snap immediately
        if (!isSnapped)
        {
            SnapToTarget();
            isSnapped = true;
        }

        HandleInput();

        // Calculate speed & lateral G
        float speedKph = 0f;
        float lateralG = 0f;
        float steerInput = 0f;

        if (targetRb != null)
        {
            speedKph = targetRb.linearVelocity.magnitude * 3.6f;
            Vector3 localVel = target.InverseTransformDirection(targetRb.linearVelocity);
            lateralG = localVel.x;
        }
        else if (arcadeController != null)
        {
            speedKph = arcadeController.SpeedKph;
            lateralG = arcadeController.LateralSpeed;
        }

        if (arcadeController != null)
        {
            steerInput = arcadeController.CurrentSteer;
        }

        // Get active offsets and FOV parameters
        GetActiveOffsets(out Vector3 localOffset, out Vector3 localLook, out float baseFov, out float maxFov);

        // Apply high speed subtle shake
        Vector3 shake = CalculateShake(speedKph);

        // Calculate Centrifugal Roll Tilt & Apex Look-Ahead
        float targetRoll = Mathf.Clamp(-lateralG * 0.35f, -maxRollAngle, maxRollAngle);
        currentRoll = Mathf.Lerp(currentRoll, targetRoll, 1f - Mathf.Exp(-rollSmoothing * Time.deltaTime));

        float targetApexYaw = steerInput * apexLookFactor;
        currentApexYaw = Mathf.Lerp(currentApexYaw, targetApexYaw, 1f - Mathf.Exp(-8f * Time.deltaTime));

        if (currentMode == CameraMode.Cockpit)
        {
            // COCKPIT: Rigidly lock to car monocoque with subtle head-inertia & vibration
            // Driver head leans slightly under G forces but NEVER clips out of cockpit
            Vector3 headInertia = -target.right * Mathf.Clamp(lateralG * 0.003f, -0.04f, 0.04f);
            transform.position = target.TransformPoint(localOffset) + headInertia + shake;

            // Rotation locks to car heading + look down nose cone + centrifugal roll + apex yaw
            Quaternion baseRot = target.rotation;
            Vector3 lookVector = localLook - localOffset;
            Quaternion lookRot = (lookVector.sqrMagnitude > 0.001f) ? Quaternion.LookRotation(lookVector, Vector3.up) : Quaternion.identity;

            Quaternion desiredRot = baseRot * lookRot * Quaternion.Euler(0f, currentApexYaw, currentRoll);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, 1f - Mathf.Exp(-rotationSmooth * 1.5f * Time.deltaTime));
        }
        else if (currentMode == CameraMode.TCam)
        {
            // T-CAM: Rigidly mounted above halo airbox
            transform.position = target.TransformPoint(localOffset) + shake * 0.5f;

            Vector3 lookVector = localLook - localOffset;
            Quaternion lookRot = (lookVector.sqrMagnitude > 0.001f) ? Quaternion.LookRotation(lookVector, Vector3.up) : Quaternion.identity;
            Quaternion desiredRot = target.rotation * lookRot * Quaternion.Euler(0f, 0f, currentRoll * 0.5f);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, 1f - Mathf.Exp(-rotationSmooth * 1.5f * Time.deltaTime));
        }
        else
        {
            // CHASE VIEWS: Smooth spring-damping follow behind car
            Vector3 desiredPos = target.TransformPoint(localOffset) + shake;
            Vector3 lookTarget = target.TransformPoint(localLook);

            float t = 1f - Mathf.Exp(-positionSmooth * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, desiredPos, t);

            Vector3 lookDir = lookTarget - transform.position;
            if (lookDir.sqrMagnitude > 0.001f)
            {
                Quaternion desiredRot = Quaternion.LookRotation(lookDir, Vector3.up) * Quaternion.Euler(0f, 0f, currentRoll);
                float rotT = 1f - Mathf.Exp(-rotationSmooth * Time.deltaTime);
                transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, rotT);
            }
        }

        // Dynamic FOV scaling with speed
        if (cam != null)
        {
            float speed01 = Mathf.Clamp01(speedKph / topSpeedKph);
            float targetFov = Mathf.Lerp(baseFov, maxFov, speed01);
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, Time.deltaTime * 4f);
        }
    }

    private void GetActiveOffsets(out Vector3 offset, out Vector3 look, out float baseFov, out float maxFov)
    {
        switch (currentMode)
        {
            case CameraMode.Cockpit:
                offset = cockpitOffset;
                look = cockpitLookOffset;
                baseFov = cockpitBaseFov;
                maxFov = cockpitMaxFov;
                break;
            case CameraMode.TCam:
                offset = tCamOffset;
                look = tCamLookOffset;
                baseFov = tCamBaseFov;
                maxFov = tCamMaxFov;
                break;
            case CameraMode.ChaseClose:
                offset = chaseOffset * 0.85f;
                look = chaseLookOffset;
                baseFov = chaseBaseFov;
                maxFov = chaseMaxFov;
                break;
            case CameraMode.ChaseFar:
            default:
                offset = chaseOffset;
                look = chaseLookOffset;
                baseFov = chaseBaseFov;
                maxFov = chaseMaxFov;
                break;
        }
    }

    private Vector3 CalculateShake(float speedKph)
    {
        if (speedKph < shakeSpeedThresholdKph) return Vector3.zero;

        float factor = Mathf.Clamp01((speedKph - shakeSpeedThresholdKph) / (topSpeedKph - shakeSpeedThresholdKph));
        float t = Time.time * shakeFrequency;
        float sx = (Mathf.PerlinNoise(shakeNoiseSeed, t) - 0.5f) * 2f * maxShakeAmount * factor;
        float sy = (Mathf.PerlinNoise(shakeNoiseSeed + 13f, t) - 0.5f) * 2f * maxShakeAmount * factor;
        float sz = (Mathf.PerlinNoise(shakeNoiseSeed + 31f, t * 0.8f) - 0.5f) * 2f * maxShakeAmount * factor * 0.5f;

        if (target != null)
        {
            return target.right * sx + target.up * sy + target.forward * sz;
        }
        return new Vector3(sx, sy, sz);
    }

    private void HandleInput()
    {
        bool cyclePressed = false;

#if ENABLE_INPUT_SYSTEM
        try
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.cKey.wasPressedThisFrame)
            {
                cyclePressed = true;
            }
        }
        catch { }
#endif

#if !ENABLE_INPUT_SYSTEM && ENABLE_LEGACY_INPUT_MANAGER
        try
        {
            if (Input.GetKeyDown(KeyCode.C))
            {
                cyclePressed = true;
            }
        }
        catch { }
#endif

        if (cyclePressed)
        {
            CycleMode();
        }
    }

    public void ToggleCameraMode()
    {
        CycleMode();
    }

    public void CycleMode()
    {
        int totalModes = System.Enum.GetValues(typeof(CameraMode)).Length;
        currentMode = (CameraMode)(((int)currentMode + 1) % totalModes);
        isSnapped = false;
        SnapToTarget();
        Debug.Log($"<color=#00D2BE>[APEX V12 Camera] Switched view to: <b>{currentMode}</b></color>");
    }

    public void SetCameraMode(CameraMode mode)
    {
        currentMode = mode;
        isSnapped = false;
        SnapToTarget();
    }

    public void SnapToTarget()
    {
        if (target == null) FindTarget();
        if (target == null) return;

        GetActiveOffsets(out Vector3 offset, out Vector3 look, out float baseFov, out _);

        if (currentMode == CameraMode.Cockpit || currentMode == CameraMode.TCam)
        {
            transform.position = target.TransformPoint(offset);
            Vector3 lookVector = look - offset;
            Quaternion lookRot = (lookVector.sqrMagnitude > 0.001f) ? Quaternion.LookRotation(lookVector, Vector3.up) : Quaternion.identity;
            transform.rotation = target.rotation * lookRot;
        }
        else
        {
            transform.position = target.TransformPoint(offset);
            Vector3 lookPos = target.TransformPoint(look);
            Vector3 dir = lookPos - transform.position;
            if (dir.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            }
        }

        if (cam != null) cam.fieldOfView = baseFov;
    }

    private void FindTarget()
    {
        if (target != null)
        {
            if (targetRb == null) targetRb = target.GetComponent<Rigidbody>();
            if (arcadeController == null) arcadeController = target.GetComponent<FormulaCarArcadeController>();
            return;
        }

        // Try find PlayerCar by tag
        GameObject playerGo = GameObject.FindWithTag("Player");
        if (playerGo == null) playerGo = GameObject.Find("PlayerCar");

        if (playerGo != null)
        {
            target = playerGo.transform;
            targetRb = playerGo.GetComponent<Rigidbody>();
            arcadeController = playerGo.GetComponent<FormulaCarArcadeController>();
            return;
        }

        var arcade = Object.FindAnyObjectByType<FormulaCarArcadeController>();
        if (arcade != null)
        {
            target = arcade.transform;
            targetRb = arcade.GetComponent<Rigidbody>();
            arcadeController = arcade;
            return;
        }
    }
}
