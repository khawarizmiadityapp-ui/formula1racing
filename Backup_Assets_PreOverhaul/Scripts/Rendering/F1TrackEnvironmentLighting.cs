using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Golden Hour (Baku Street Circuit sunset style) Environment & Lighting Setup.
/// Sets up:
/// - Warm Directional Light with low sun angle and soft shadow cascades
/// - Box Projection Reflection Probes along the track for real-time metallic reflections
/// - Ambient lighting and sky atmosphere
/// </summary>
[ExecuteAlways]
public class F1TrackEnvironmentLighting : MonoBehaviour
{
    #region ──── INSPECTOR FIELDS ────

    [Header("═══ DIRECTIONAL LIGHT (GOLDEN HOUR SUN) ═══")]
    [SerializeField] private Light sunLight;
    [Tooltip("Warm golden sunset sunlight color (Baku 6 PM vibes)")]
    [SerializeField] private Color goldenSunColor = new Color(1.0f, 0.82f, 0.62f, 1.0f);
    [Tooltip("Sunlight intensity in URP lux")]
    [SerializeField] private float sunIntensity = 1.6f;
    [Tooltip("Sun elevation angle (low angle for dramatic shadows across track)")]
    [Range(10f, 40f)]
    [SerializeField] private float sunElevationAngle = 19.5f;
    [Tooltip("Sun azimuth / horizontal rotation angle")]
    [Range(0f, 360f)]
    [SerializeField] private float sunAzimuthAngle = 240f;
    [Tooltip("Shadow strength")]
    [Range(0f, 1f)]
    [SerializeField] private float shadowStrength = 0.90f;

    [Header("═══ AMBIENT LIGHTING ═══")]
    [SerializeField] private Color skyAmbientColor = new Color(0.42f, 0.52f, 0.68f);
    [SerializeField] private Color equatorAmbientColor = new Color(0.68f, 0.55f, 0.45f);
    [SerializeField] private Color groundAmbientColor = new Color(0.18f, 0.16f, 0.15f);

    [Header("═══ REFLECTION PROBES CONFIGURATION ═══")]
    [Tooltip("Reflection Probe resolution (128 for Mobile, 256 for PC)")]
    [SerializeField] private int reflectionProbeResolution = 256;
    [Tooltip("Enable Box Projection for accurate street circuit reflections")]
    [SerializeField] private bool useBoxProjection = true;
    [Tooltip("Blend distance between reflection probe volumes (meters)")]
    [SerializeField] private float probeBlendDistance = 8f;

    #endregion

    #region ──── PUBLIC SETUP METHODS ────

    /// <summary>
    /// Apply Baku Golden Hour lighting and shadow setup.
    /// </summary>
    [ContextMenu("Apply Golden Hour Lighting")]
    public void ApplyLighting()
    {
        // 1. Configure Sun Directional Light
        if (sunLight == null)
        {
            var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            foreach (var l in lights)
            {
                if (l.type == LightType.Directional)
                {
                    sunLight = l;
                    break;
                }
            }
        }

        if (sunLight != null)
        {
            sunLight.color = goldenSunColor;
            sunLight.intensity = sunIntensity;
            sunLight.transform.rotation = Quaternion.Euler(sunElevationAngle, sunAzimuthAngle, 0f);
            sunLight.shadows = LightShadows.Soft;
            sunLight.shadowStrength = shadowStrength;
            sunLight.shadowNormalBias = 0.4f;
            sunLight.shadowBias = 0.05f;
        }

        // 2. Ambient Trilight
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = skyAmbientColor;
        RenderSettings.ambientEquatorColor = equatorAmbientColor;
        RenderSettings.ambientGroundColor = groundAmbientColor;

        // 3. Configure all Reflection Probes on track
        var probes = Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None);
        foreach (var probe in probes)
        {
            probe.resolution = reflectionProbeResolution;
            probe.boxProjection = useBoxProjection;
            probe.blendDistance = probeBlendDistance;
            probe.clearFlags = ReflectionProbeClearFlags.Skybox;
            probe.importance = 2;
        }

        Debug.Log($"[F1 Lighting] Baku Golden Hour applied to {sunLight?.name} & {probes.Length} Reflection Probes.");
    }

    #endregion
}
