using UnityEngine;

/// <summary>
/// Smooth arcade chase camera for the Formula racing car.
/// Follows from behind with damping, maintains clear sightlines over the rear wing
/// and front nose cone, and features dynamic FOV expansion and organic high-speed cockpit buffeting.
/// </summary>
public class RacingCameraFollow : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("Target car transform to follow")]
    public Transform target;

    [Header("Follow Positioning")]
    [Tooltip("Camera offset in target local space: height 2.2m, distance 5.6m behind")]
    public Vector3 localOffset = new Vector3(0f, 2.2f, -5.6f);
    [Tooltip("Point in target space to look at: slightly above cockpit toward the nose")]
    public Vector3 lookAtOffset = new Vector3(0f, 0.95f, 2.4f);

    [Header("Smoothing")]
    [Range(2f, 25f)] public float positionSmooth = 9f;
    [Range(2f, 30f)] public float rotationSmooth = 12f;

    [Header("Dynamic FOV")]
    [SerializeField] private bool enableDynamicFov = true;
    [SerializeField] private float baseFov = 60f;
    [SerializeField] private float maxFov = 75f;
    [SerializeField] private float topSpeedKph = 370f;

    [Header("High-Speed Micro Shake")]
    [SerializeField] private bool enableSpeedShake = true;
    [SerializeField] private float shakeSpeedThresholdKph = 200f;
    [SerializeField] private float maxShakeAmount = 0.035f;

    private Camera cam;
    private Rigidbody targetRb;
    private float shakeNoiseSeed;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        shakeNoiseSeed = Random.value * 100f;
        FindTargetIfNull();
    }

    private void OnValidate()
    {
        FindTargetIfNull();
    }

    private void FindTargetIfNull()
    {
        if (target == null)
        {
            var controller = Object.FindAnyObjectByType<FormulaCarArcadeController>();
            if (controller != null)
            {
                target = controller.transform;
            }
        }
        if (target != null && targetRb == null)
        {
            targetRb = target.GetComponent<Rigidbody>();
        }
    }

    private void Start()
    {
        FindTargetIfNull();
        if (target != null)
        {
            transform.position = target.TransformPoint(localOffset);
            Vector3 lookTarget = target.TransformPoint(lookAtOffset);
            transform.rotation = Quaternion.LookRotation(lookTarget - transform.position, Vector3.up);
        }
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            FindTargetIfNull();
            if (target == null) return;
        }

        // Calculate chase position
        Vector3 desiredPosition = target.TransformPoint(localOffset);

        // High-speed vibration effect
        float speedKph = targetRb != null ? targetRb.linearVelocity.magnitude * 3.6f : 0f;
        if (enableSpeedShake && speedKph > shakeSpeedThresholdKph)
        {
            float shakeFactor = Mathf.Clamp01((speedKph - shakeSpeedThresholdKph) / (topSpeedKph - shakeSpeedThresholdKph));
            float timeScale = Time.time * 28f;
            float shakeX = (Mathf.PerlinNoise(shakeNoiseSeed, timeScale) - 0.5f) * 2f * maxShakeAmount * shakeFactor;
            float shakeY = (Mathf.PerlinNoise(shakeNoiseSeed + 20f, timeScale) - 0.5f) * 2f * maxShakeAmount * shakeFactor;
            desiredPosition += target.right * shakeX + target.up * shakeY;
        }

        float posT = 1f - Mathf.Exp(-positionSmooth * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, desiredPosition, posT);

        // Look-at smoothing
        Vector3 lookPoint = target.TransformPoint(lookAtOffset);
        Vector3 lookDir = lookPoint - transform.position;
        if (lookDir.sqrMagnitude > 0.001f)
        {
            Quaternion desiredRotation = Quaternion.LookRotation(lookDir, Vector3.up);
            float rotT = 1f - Mathf.Exp(-rotationSmooth * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotT);
        }

        // Dynamic FOV
        if (enableDynamicFov && cam != null)
        {
            float targetFov = Mathf.Lerp(baseFov, maxFov, Mathf.Clamp01(speedKph / topSpeedKph));
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, Time.deltaTime * 3.5f);
        }
    }
}
