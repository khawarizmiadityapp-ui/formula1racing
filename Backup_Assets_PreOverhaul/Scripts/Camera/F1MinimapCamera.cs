using UnityEngine;

/// <summary>
/// Minimap camera system for the racing HUD.
/// Renders a top-down orthographic view of the track with player marker.
///
/// Setup:
/// 1. Create a secondary Camera with Culling Mask set to track layers only
/// 2. Set this camera to render to a RenderTexture
/// 3. Display that RenderTexture on a RawImage UI element
/// 4. Attach this script to the minimap camera
///
/// Attach to: Minimap Camera GameObject
/// Dependencies: F1CarController (auto-resolved)
/// </summary>
public class F1MinimapCamera : MonoBehaviour
{
    [Header("═══ TARGET ═══")]
    [SerializeField] private Transform target;

    [Header("═══ MINIMAP SETTINGS ═══")]
    [Tooltip("Height above the track")]
    [SerializeField] private float cameraHeight = 200f;
    [Tooltip("Orthographic size (zoom level)")]
    [SerializeField] private float orthoSize = 120f;
    [Tooltip("Follow the player (true) or show entire track (false)")]
    [SerializeField] private bool followPlayer = false;
    [Tooltip("Rotate minimap with player heading")]
    [SerializeField] private bool rotateWithPlayer = false;
    [Tooltip("Track center position (used when not following player)")]
    [SerializeField] private Vector3 trackCenter = Vector3.zero;

    [Header("═══ PLAYER MARKER ═══")]
    [Tooltip("Optional sprite/transform to mark player position on minimap")]
    [SerializeField] private Transform playerMarker;
    [SerializeField] private float markerScale = 3f;

    private Camera cam;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        if (cam != null)
        {
            cam.orthographic = true;
            cam.orthographicSize = orthoSize;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.08f, 0.12f, 0.9f);
        }

        FindTarget();
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            FindTarget();
            if (target == null) return;
        }

        Vector3 pos;
        Quaternion rot;

        if (followPlayer)
        {
            pos = new Vector3(target.position.x, cameraHeight, target.position.z);
            rot = rotateWithPlayer
                ? Quaternion.Euler(90f, target.eulerAngles.y, 0f)
                : Quaternion.Euler(90f, 0f, 0f);
        }
        else
        {
            pos = new Vector3(trackCenter.x, cameraHeight, trackCenter.z);
            rot = Quaternion.Euler(90f, 0f, 0f);
        }

        transform.position = pos;
        transform.rotation = rot;

        // Update player marker
        if (playerMarker != null)
        {
            playerMarker.position = target.position + Vector3.up * (cameraHeight - 1f);
            playerMarker.rotation = Quaternion.Euler(90f, target.eulerAngles.y, 0f);
            playerMarker.localScale = Vector3.one * markerScale;
        }
    }

    private void FindTarget()
    {
        if (target == null)
        {
            var controller = Object.FindAnyObjectByType<F1CarController>();
            if (controller != null) target = controller.transform;
        }
    }

    /// <summary>Set the follow mode at runtime.</summary>
    public void SetFollowPlayer(bool follow)
    {
        followPlayer = follow;
    }

    /// <summary>Set zoom level at runtime.</summary>
    public void SetZoom(float size)
    {
        orthoSize = size;
        if (cam != null) cam.orthographicSize = size;
    }
}
