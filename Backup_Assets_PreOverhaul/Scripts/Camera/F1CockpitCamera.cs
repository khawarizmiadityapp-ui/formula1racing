using UnityEngine;

/// <summary>
/// F1 cockpit (T-Cam / Above Halo) camera with realistic effects:
/// - Positioned above halo & behind visor (T-Cam perspective)
/// - Perlin noise camera shake that intensifies with speed
/// - Centrifugal roll tilt during cornering (G-force feel)
/// - Speed-dependent FOV expansion
/// - Dynamic vertical bob over kerbs
/// - Multiple camera modes (T-Cam, Cockpit, Chase, TV)
///
/// Attach to: Camera GameObject (child of car, or separate object)
/// Dependencies: F1CarController
/// </summary>
public class F1CockpitCamera : MonoBehaviour
{
    #region ──── ENUMS ────

    public enum CameraMode
    {
        TCam,           // Above halo, behind visor
        Cockpit,        // Inside cockpit, lower
        ChaseFar,       // Classic chase cam
        ChaseClose,     // Close chase cam
        TV              // TV helicopter style
    }

    #endregion

    #region ──── INSPECTOR FIELDS ────

    [Header("═══ TARGET ═══")]
    [SerializeField] private Transform target;

    [Header("═══ CAMERA MODE ═══")]
    [SerializeField] private CameraMode currentMode = CameraMode.TCam;

    [Header("═══ T-CAM POSITION (Above Halo) ═══")]
    [Tooltip("Local offset from car center: above halo, behind visor")]
    [SerializeField] private Vector3 tCamOffset = new Vector3(0f, 1.28f, -0.35f);
    [SerializeField] private Vector3 tCamLookOffset = new Vector3(0f, 0.9f, 3.5f);
    [SerializeField] private float tCamBaseFov = 68f;
    [SerializeField] private float tCamMaxFov = 82f;

    [Header("═══ COCKPIT POSITION ═══")]
    [SerializeField] private Vector3 cockpitOffset = new Vector3(0f, 1.05f, -0.20f);
    [SerializeField] private Vector3 cockpitLookOffset = new Vector3(0f, 0.85f, 4.0f);
    [SerializeField] private float cockpitBaseFov = 72f;
    [SerializeField] private float cockpitMaxFov = 88f;

    [Header("═══ CHASE FAR ═══")]
    [SerializeField] private Vector3 chaseFarOffset = new Vector3(0f, 2.2f, -5.6f);
    [SerializeField] private Vector3 chaseFarLookOffset = new Vector3(0f, 0.95f, 2.4f);
    [SerializeField] private float chaseFarBaseFov = 60f;
    [SerializeField] private float chaseFarMaxFov = 75f;

    [Header("═══ CHASE CLOSE ═══")]
    [SerializeField] private Vector3 chaseCloseOffset = new Vector3(0f, 1.6f, -3.8f);
    [SerializeField] private Vector3 chaseCloseLookOffset = new Vector3(0f, 0.85f, 2.0f);
    [SerializeField] private float chaseCloseBaseFov = 64f;
    [SerializeField] private float chaseCloseMaxFov = 78f;

    [Header("═══ TV CAMERA ═══")]
    [SerializeField] private Vector3 tvOffset = new Vector3(4f, 3f, -8f);
    [SerializeField] private Vector3 tvLookOffset = new Vector3(0f, 0.5f, 2f);
    [SerializeField] private float tvBaseFov = 42f;
    [SerializeField] private float tvMaxFov = 55f;

    [Header("═══ CAMERA SHAKE ═══")]
    [Tooltip("Speed threshold (km/h) to start shake")]
    [SerializeField] private float shakeSpeedThresholdKph = 150f;
    [Tooltip("Maximum shake displacement (meters)")]
    [SerializeField] private float maxShakeAmount = 0.025f;
    [Tooltip("Shake frequency multiplier")]
    [SerializeField] private float shakeFrequency = 32f;
    [Tooltip("Extra shake on kerbs/bumps")]
    [SerializeField] private float kerbShakeMultiplier = 2.5f;

    [Header("═══ CENTRIFUGAL ROLL TILT ═══")]
    [Tooltip("Maximum roll angle (degrees) during cornering")]
    [SerializeField] private float maxRollAngle = 3.5f;
    [Tooltip("Roll response speed")]
    [SerializeField] private float rollSmoothing = 5f;

    [Header("═══ SMOOTHING ═══")]
    [SerializeField] [Range(2f, 30f)] private float positionSmooth = 14f;
    [SerializeField] [Range(2f, 30f)] private float rotationSmooth = 16f;
    [SerializeField] private float modeSwitchSpeed = 6f;

    #endregion

    #region ──── RUNTIME ────

    private Camera cam;
    private Rigidbody targetRb;
    private F1CarController carController;
    private float shakeNoiseSeed;
    private float currentRoll;
    private Vector3 activeOffset;
    private Vector3 activeLookOffset;
    private float activeBaseFov;
    private float activeMaxFov;
    private float topSpeedKph = 350f;

    #endregion

    #region ──── PUBLIC PROPERTIES ────

    public CameraMode CurrentMode => currentMode;

    #endregion

    #region ──── LIFECYCLE ────

    private void Awake()
    {
        cam = GetComponent<Camera>();
        shakeNoiseSeed = Random.value * 100f;
        FindTarget();
        ApplyModeSettings(true);
    }

    private void Start()
    {
        FindTarget();
        if (target != null)
        {
            SnapToTarget();
        }
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            FindTarget();
            if (target == null) return;
        }

        HandleModeSwitch();
        ApplyModeSettings(false);

        float speedKph = targetRb != null ? targetRb.linearVelocity.magnitude * 3.6f : 0f;

        // Calculate desired position
        Vector3 desiredPos;
        Vector3 lookTarget;

        if (currentMode == CameraMode.TCam || currentMode == CameraMode.Cockpit)
        {
            // Onboard: attached to car, no smoothing on position
            desiredPos = target.TransformPoint(activeOffset);
            lookTarget = target.TransformPoint(activeLookOffset);
        }
        else
        {
            // Chase/TV: smooth follow
            desiredPos = target.TransformPoint(activeOffset);
            lookTarget = target.TransformPoint(activeLookOffset);
        }

        // Camera shake
        Vector3 shakeOffset = CalculateShake(speedKph);
        desiredPos += shakeOffset;

        // Position smoothing
        if (currentMode == CameraMode.TCam || currentMode == CameraMode.Cockpit)
        {
            // Minimal smoothing for onboard (direct attachment feel)
            float onboardSmooth = 1f - Mathf.Exp(-positionSmooth * 2f * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, desiredPos, onboardSmooth);
        }
        else
        {
            float posT = 1f - Mathf.Exp(-positionSmooth * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, desiredPos, posT);
        }

        // Rotation with centrifugal roll
        Vector3 lookDir = lookTarget - transform.position;
        if (lookDir.sqrMagnitude > 0.001f)
        {
            Quaternion desiredRotation = Quaternion.LookRotation(lookDir, Vector3.up);

            // Centrifugal roll tilt
            float lateralG = carController != null ? carController.LateralSpeed : 0f;
            float targetRollAngle = Mathf.Clamp(-lateralG * 0.8f, -maxRollAngle, maxRollAngle);
            currentRoll = Mathf.Lerp(currentRoll, targetRollAngle,
                1f - Mathf.Exp(-rollSmoothing * Time.deltaTime));
            desiredRotation *= Quaternion.Euler(0f, 0f, currentRoll);

            float rotT = 1f - Mathf.Exp(-rotationSmooth * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotT);
        }

        // Dynamic FOV
        if (cam != null)
        {
            float speed01 = Mathf.Clamp01(speedKph / topSpeedKph);
            float targetFov = Mathf.Lerp(activeBaseFov, activeMaxFov, speed01);
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, Time.deltaTime * 4f);
        }
    }

    #endregion

    #region ──── CAMERA MODE ────

    public void SetCameraMode(CameraMode mode)
    {
        currentMode = mode;
    }

    public void CycleMode()
    {
        currentMode = currentMode switch
        {
            CameraMode.TCam => CameraMode.Cockpit,
            CameraMode.Cockpit => CameraMode.ChaseClose,
            CameraMode.ChaseClose => CameraMode.ChaseFar,
            CameraMode.ChaseFar => CameraMode.TV,
            CameraMode.TV => CameraMode.TCam,
            _ => CameraMode.TCam,
        };
    }

    private void HandleModeSwitch()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && kb.cKey.wasPressedThisFrame)
        {
            CycleMode();
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.C))
        {
            CycleMode();
        }
#endif
    }

    private void ApplyModeSettings(bool instant)
    {
        Vector3 targetOffset, targetLookOffset;
        float targetBaseFov, targetMaxFov;

        switch (currentMode)
        {
            case CameraMode.TCam:
                targetOffset = tCamOffset;
                targetLookOffset = tCamLookOffset;
                targetBaseFov = tCamBaseFov;
                targetMaxFov = tCamMaxFov;
                break;
            case CameraMode.Cockpit:
                targetOffset = cockpitOffset;
                targetLookOffset = cockpitLookOffset;
                targetBaseFov = cockpitBaseFov;
                targetMaxFov = cockpitMaxFov;
                break;
            case CameraMode.ChaseFar:
                targetOffset = chaseFarOffset;
                targetLookOffset = chaseFarLookOffset;
                targetBaseFov = chaseFarBaseFov;
                targetMaxFov = chaseFarMaxFov;
                break;
            case CameraMode.ChaseClose:
                targetOffset = chaseCloseOffset;
                targetLookOffset = chaseCloseLookOffset;
                targetBaseFov = chaseCloseBaseFov;
                targetMaxFov = chaseCloseMaxFov;
                break;
            case CameraMode.TV:
                targetOffset = tvOffset;
                targetLookOffset = tvLookOffset;
                targetBaseFov = tvBaseFov;
                targetMaxFov = tvMaxFov;
                break;
            default:
                targetOffset = tCamOffset;
                targetLookOffset = tCamLookOffset;
                targetBaseFov = tCamBaseFov;
                targetMaxFov = tCamMaxFov;
                break;
        }

        if (instant)
        {
            activeOffset = targetOffset;
            activeLookOffset = targetLookOffset;
            activeBaseFov = targetBaseFov;
            activeMaxFov = targetMaxFov;
        }
        else
        {
            float t = 1f - Mathf.Exp(-modeSwitchSpeed * Time.deltaTime);
            activeOffset = Vector3.Lerp(activeOffset, targetOffset, t);
            activeLookOffset = Vector3.Lerp(activeLookOffset, targetLookOffset, t);
            activeBaseFov = Mathf.Lerp(activeBaseFov, targetBaseFov, t);
            activeMaxFov = Mathf.Lerp(activeMaxFov, targetMaxFov, t);
        }
    }

    #endregion

    #region ──── CAMERA SHAKE ────

    private Vector3 CalculateShake(float speedKph)
    {
        if (speedKph < shakeSpeedThresholdKph) return Vector3.zero;

        float shakeFactor = Mathf.Clamp01(
            (speedKph - shakeSpeedThresholdKph) / (topSpeedKph - shakeSpeedThresholdKph)
        );

        // Scale shake based on camera mode (onboard = more shake)
        float modeScale = (currentMode == CameraMode.TCam || currentMode == CameraMode.Cockpit) ? 1.5f : 1f;
        float shakeAmt = maxShakeAmount * shakeFactor * modeScale;

        float t = Time.time * shakeFrequency;
        float sx = (Mathf.PerlinNoise(shakeNoiseSeed, t) - 0.5f) * 2f * shakeAmt;
        float sy = (Mathf.PerlinNoise(shakeNoiseSeed + 17f, t) - 0.5f) * 2f * shakeAmt;
        float sz = (Mathf.PerlinNoise(shakeNoiseSeed + 37f, t * 0.7f) - 0.5f) * 2f * shakeAmt * 0.3f;

        return target.right * sx + target.up * sy + target.forward * sz;
    }

    #endregion

    #region ──── UTILITY ────

    private void FindTarget()
    {
        if (target == null)
        {
            var controller = Object.FindAnyObjectByType<F1CarController>();
            if (controller != null)
            {
                target = controller.transform;
                carController = controller;
            }
        }
        if (target != null && targetRb == null)
        {
            targetRb = target.GetComponent<Rigidbody>();
        }
        if (target != null && carController == null)
        {
            carController = target.GetComponent<F1CarController>();
        }
    }

    private void SnapToTarget()
    {
        if (target == null) return;
        transform.position = target.TransformPoint(activeOffset);
        Vector3 look = target.TransformPoint(activeLookOffset);
        transform.rotation = Quaternion.LookRotation(look - transform.position, Vector3.up);
    }

    #endregion
}
