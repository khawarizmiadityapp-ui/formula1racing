using UnityEngine;

/// <summary>
/// Controls the visual rotation and steering of the four individual Formula racing wheels.
/// Front wheels steer smoothly with driver input; all four wheels rotate based on ground speed.
/// Correctly accounts for left vs right wheel assembly hub orientation (right wheels rotated 180 deg on Y).
/// </summary>
public class FormulaWheelVisuals : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody targetRigidbody;
    [SerializeField] private FormulaCarArcadeController carController;
    [SerializeField] private Transform frontLeftWheel;
    [SerializeField] private Transform frontRightWheel;
    [SerializeField] private Transform rearLeftWheel;
    [SerializeField] private Transform rearRightWheel;

    [Header("Settings")]
    [Tooltip("Effective rolling radius of the tire (supplied model diameter is ~0.79m => radius = 0.395m)")]
    [SerializeField] private float wheelRadius = 0.395f;
    [Tooltip("Maximum visual front steering angle in degrees")]
    [SerializeField] private float maxSteerAngle = 22f;

    private float spinAngle;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnValidate()
    {
        ResolveReferences();
    }

    private void ResolveReferences()
    {
        if (targetRigidbody == null) targetRigidbody = GetComponentInParent<Rigidbody>();
        if (carController == null) carController = GetComponentInParent<FormulaCarArcadeController>();

        // Auto-find child wheels by name if not assigned in inspector
        if (frontLeftWheel == null) frontLeftWheel = FindChildRecursive(transform, "FrontLeft");
        if (frontRightWheel == null) frontRightWheel = FindChildRecursive(transform, "FrontRight");
        if (rearLeftWheel == null) rearLeftWheel = FindChildRecursive(transform, "RearLeft");
        if (rearRightWheel == null) rearRightWheel = FindChildRecursive(transform, "RearRight");
    }

    private static Transform FindChildRecursive(Transform parent, string name)
    {
        if (parent == null) return null;
        Transform direct = parent.Find(name);
        if (direct != null) return direct;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindChildRecursive(parent.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }

    private void Update()
    {
        float forwardSpeed = 0f;
        if (targetRigidbody != null)
        {
            forwardSpeed = Vector3.Dot(targetRigidbody.linearVelocity, transform.forward);
        }

        // Calculate continuous rolling spin angle
        float safeRadius = Mathf.Max(0.05f, wheelRadius);
        spinAngle += (forwardSpeed / safeRadius) * Mathf.Rad2Deg * Time.deltaTime;
        // Keep spinAngle bounded to avoid precision drift over long sessions
        if (spinAngle > 36000f || spinAngle < -36000f)
        {
            spinAngle = Mathf.Repeat(spinAngle, 360f);
        }

        // Get steering input from controller or calculate smoothly
        float steer = carController != null ? carController.CurrentSteer : 0f;
        float steerAngle = steer * maxSteerAngle;

        // Apply transformations
        // Left wheels: outer rim face points to vehicle -X (base rotation = 0 deg).
        // Positive rotation around local X rolls the wheel forward.
        if (frontLeftWheel != null)
        {
            frontLeftWheel.localRotation = Quaternion.Euler(0f, steerAngle, 0f) * Quaternion.Euler(spinAngle, 0f, 0f);
        }
        if (rearLeftWheel != null)
        {
            rearLeftWheel.localRotation = Quaternion.Euler(spinAngle, 0f, 0f);
        }

        // Right wheels: outer rim face points to vehicle +X (base rotation = 180 deg around Y).
        // Negative rotation around local X rolls the wheel forward in vehicle space.
        if (frontRightWheel != null)
        {
            frontRightWheel.localRotation = Quaternion.Euler(0f, steerAngle, 0f) * Quaternion.Euler(0f, 180f, 0f) * Quaternion.Euler(-spinAngle, 0f, 0f);
        }
        if (rearRightWheel != null)
        {
            rearRightWheel.localRotation = Quaternion.Euler(0f, 180f, 0f) * Quaternion.Euler(-spinAngle, 0f, 0f);
        }
    }
}
