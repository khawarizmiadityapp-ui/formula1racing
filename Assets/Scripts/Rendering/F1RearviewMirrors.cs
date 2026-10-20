using UnityEngine;

/// <summary>
/// Dual Real-Time Rearview Mirror System for Formula 1 Car (Left & Right Mirrors).
/// Renders the track behind the car using lightweight secondary cameras and RenderTextures.
/// 
/// Performance Optimizations for Mobile & PC:
/// - Alternating frame updates (Left mirror updates on even frames, Right on odd frames)
/// - Target refresh rate throttle (e.g. 30 or 60 FPS instead of rendering every sub-frame)
/// - Layer culling mask to exclude local player chassis/halo geometry
/// - Reduced far-clip distance (80-120m) and disabled post-processing
/// - Automatic horizontal matrix inversion so mirror reflection is physically accurate
/// </summary>
public class F1RearviewMirrors : MonoBehaviour
{
    #region ──── ENUMS ────
    public enum MirrorResolution
    {
        MobileLow_128x64 = 128,
        MobileStandard_256x128 = 256,
        PCHigh_512x256 = 512,
        PCUltra_1024x512 = 1024
    }
    #endregion

    #region ──── INSPECTOR FIELDS ────

    [Header("═══ MIRROR RENDERERS ═══")]
    [Tooltip("MeshRenderer of the left mirror glass")]
    [SerializeField] private Renderer leftMirrorRenderer;
    [Tooltip("MeshRenderer of the right mirror glass")]
    [SerializeField] private Renderer rightMirrorRenderer;
    [Tooltip("Material index on the renderer that represents the mirror surface (default 0)")]
    [SerializeField] private int mirrorMaterialIndex = 0;
    [Tooltip("Shader property name for the reflection texture (usually _BaseMap or _MainTex)")]
    [SerializeField] private string texturePropertyName = "_BaseMap";

    [Header("═══ CAMERA ANCHORS ═══")]
    [Tooltip("Transform position & orientation for left mirror camera (points backwards)")]
    [SerializeField] private Transform leftCameraAnchor;
    [Tooltip("Transform position & orientation for right mirror camera (points backwards)")]
    [SerializeField] private Transform rightCameraAnchor;

    [Header("═══ QUALITY & RESOLUTION ═══")]
    [SerializeField] private MirrorResolution resolution = MirrorResolution.MobileStandard_256x128;
    [Range(20f, 65f)]
    [SerializeField] private float fieldOfView = 40f;
    [SerializeField] private float nearClipPlane = 0.2f;
    [SerializeField] private float farClipPlane = 100f;
    [Tooltip("Layers to render in the mirrors (exclude player car/halo to prevent self-clipping)")]
    [SerializeField] private LayerMask cullingMask = ~0;

    [Header("═══ PERFORMANCE OPTIMIZATION ═══")]
    [Tooltip("Alternate rendering: Left mirror on even frames, Right mirror on odd frames (cuts GPU draw calls by 50%)")]
    [SerializeField] private bool alternateFrames = true;
    [Tooltip("Target mirror refresh rate in FPS (e.g. 30 for mobile, 60 for PC)")]
    [Range(15, 60)]
    [SerializeField] private int targetFPS = 30;
    [Tooltip("Disable shadow rendering in mirrors for huge mobile performance boost")]
    [SerializeField] private bool disableShadows = true;

    #endregion

    #region ──── RUNTIME FIELDS ────

    private Camera leftCam;
    private Camera rightCam;
    private RenderTexture leftRT;
    private RenderTexture rightRT;
    private Material leftMaterialInstance;
    private Material rightMaterialInstance;
    private float renderTimer;
    private int frameCounter;

    #endregion

    #region ──── LIFECYCLE ────

    private void Awake()
    {
        CreateRenderTextures();
        CreateMirrorCameras();
        ApplyMaterials();
    }

    private void OnDestroy()
    {
        Cleanup();
    }

    private void LateUpdate()
    {
        if (leftCam == null || rightCam == null) return;

        // Sync camera positions to anchors
        if (leftCameraAnchor != null)
        {
            leftCam.transform.position = leftCameraAnchor.position;
            leftCam.transform.rotation = leftCameraAnchor.rotation;
        }

        if (rightCameraAnchor != null)
        {
            rightCam.transform.position = rightCameraAnchor.position;
            rightCam.transform.rotation = rightCameraAnchor.rotation;
        }

        // Throttle updates based on target FPS
        renderTimer += Time.deltaTime;
        float frameInterval = 1f / targetFPS;

        if (renderTimer >= frameInterval)
        {
            renderTimer -= frameInterval;
            RenderMirrors();
        }
    }

    #endregion

    #region ──── INITIALIZATION ────

    private void CreateRenderTextures()
    {
        int width = (int)resolution;
        int height = width / 2; // 2:1 aspect ratio typical for F1 wing mirrors

        leftRT = new RenderTexture(width, height, 16, RenderTextureFormat.ARGB32)
        {
            name = "RT_LeftMirror",
            filterMode = FilterMode.Bilinear,
            antiAliasing = 1,
            useMipMap = false
        };
        leftRT.Create();

        rightRT = new RenderTexture(width, height, 16, RenderTextureFormat.ARGB32)
        {
            name = "RT_RightMirror",
            filterMode = FilterMode.Bilinear,
            antiAliasing = 1,
            useMipMap = false
        };
        rightRT.Create();
    }

    private void CreateMirrorCameras()
    {
        // Setup Left Camera
        GameObject leftCamObj = new GameObject("F1_LeftMirrorCamera");
        leftCamObj.transform.SetParent(leftCameraAnchor != null ? leftCameraAnchor : transform, false);
        leftCam = leftCamObj.AddComponent<Camera>();
        ConfigureCamera(leftCam, leftRT);

        // Setup Right Camera
        GameObject rightCamObj = new GameObject("F1_RightMirrorCamera");
        rightCamObj.transform.SetParent(rightCameraAnchor != null ? rightCameraAnchor : transform, false);
        rightCam = rightCamObj.AddComponent<Camera>();
        ConfigureCamera(rightCam, rightRT);
    }

    private void ConfigureCamera(Camera cam, RenderTexture targetTexture)
    {
        cam.targetTexture = targetTexture;
        cam.fieldOfView = fieldOfView;
        cam.nearClipPlane = nearClipPlane;
        cam.farClipPlane = farClipPlane;
        cam.cullingMask = cullingMask;
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.depth = -10; // Render before main camera
        cam.enabled = false; // We manually trigger cam.Render() for maximum control

        // Mirror inversion matrix so reflections are physically mirrored horizontally
        Matrix4x4 mat = cam.projectionMatrix;
        mat *= Matrix4x4.Scale(new Vector3(-1, 1, 1));
        cam.projectionMatrix = mat;

        // Optimization: Disable expensive URP features on mirror cameras
        var urpCamData = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        if (urpCamData == null)
        {
            urpCamData = cam.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        }
        if (urpCamData != null)
        {
            urpCamData.renderPostProcessing = false;
            urpCamData.renderShadows = !disableShadows;
            urpCamData.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.None;
            urpCamData.requiresColorOption = UnityEngine.Rendering.Universal.CameraOverrideOption.Off;
            urpCamData.requiresDepthOption = UnityEngine.Rendering.Universal.CameraOverrideOption.Off;
        }
    }

    private void ApplyMaterials()
    {
        Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
        if (litShader == null) litShader = Shader.Find("Standard");

        // Apply to left mirror
        if (leftMirrorRenderer != null)
        {
            leftMaterialInstance = new Material(litShader)
            {
                name = "Mat_LeftMirror_Runtime"
            };
            if (leftMaterialInstance.HasProperty(texturePropertyName))
                leftMaterialInstance.SetTexture(texturePropertyName, leftRT);
            if (leftMaterialInstance.HasProperty("_Smoothness"))
                leftMaterialInstance.SetFloat("_Smoothness", 0.98f);
            if (leftMaterialInstance.HasProperty("_Metallic"))
                leftMaterialInstance.SetFloat("_Metallic", 0.05f);

            var mats = leftMirrorRenderer.materials;
            if (mirrorMaterialIndex >= 0 && mirrorMaterialIndex < mats.Length)
            {
                mats[mirrorMaterialIndex] = leftMaterialInstance;
                leftMirrorRenderer.materials = mats;
            }
        }

        // Apply to right mirror
        if (rightMirrorRenderer != null)
        {
            rightMaterialInstance = new Material(litShader)
            {
                name = "Mat_RightMirror_Runtime"
            };
            if (rightMaterialInstance.HasProperty(texturePropertyName))
                rightMaterialInstance.SetTexture(texturePropertyName, rightRT);
            if (rightMaterialInstance.HasProperty("_Smoothness"))
                rightMaterialInstance.SetFloat("_Smoothness", 0.98f);
            if (rightMaterialInstance.HasProperty("_Metallic"))
                rightMaterialInstance.SetFloat("_Metallic", 0.05f);

            var mats = rightMirrorRenderer.materials;
            if (mirrorMaterialIndex >= 0 && mirrorMaterialIndex < mats.Length)
            {
                mats[mirrorMaterialIndex] = rightMaterialInstance;
                rightMirrorRenderer.materials = mats;
            }
        }
    }

    #endregion

    #region ──── RENDERING LOOP ────

    private void RenderMirrors()
    {
        frameCounter++;

        if (alternateFrames)
        {
            // Alternate frames between left and right to maintain stable mobile framerates
            if (frameCounter % 2 == 0)
            {
                if (leftCam != null) leftCam.Render();
            }
            else
            {
                if (rightCam != null) rightCam.Render();
            }
        }
        else
        {
            if (leftCam != null) leftCam.Render();
            if (rightCam != null) rightCam.Render();
        }
    }

    #endregion

    #region ──── CLEANUP ────

    private void Cleanup()
    {
        if (leftRT != null)
        {
            leftRT.Release();
            Destroy(leftRT);
        }
        if (rightRT != null)
        {
            rightRT.Release();
            Destroy(rightRT);
        }
        if (leftMaterialInstance != null) Destroy(leftMaterialInstance);
        if (rightMaterialInstance != null) Destroy(rightMaterialInstance);
        if (leftCam != null) Destroy(leftCam.gameObject);
        if (rightCam != null) Destroy(rightCam.gameObject);
    }

    #endregion
}
