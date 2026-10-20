using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Dynamic Post-Processing & Speed Effects Controller for Formula 1 Racing.
/// Handles:
/// - Dynamic Motion Blur that scales up smoothly when car exceeds 200 KM/H
/// - High-speed tunnel vision (subtle Chromatic Aberration & Lens Distortion)
/// - ACES Tonemapping, Cinematic Bloom, and Vignette
/// - Golden Hour lighting environment integration (Baku street circuit style)
/// - Bulletproof null and destroyed-reference handling to eliminate MissingReferenceException.
/// </summary>
[RequireComponent(typeof(Volume))]
public class F1DynamicPostProcessing : MonoBehaviour
{
    #region ──── INSPECTOR FIELDS ────

    [Header("═══ CAR REFERENCE ═══")]
    [Tooltip("Reference to the player car controller (auto-resolved if null)")]
    [SerializeField] private FormulaCarArcadeController arcadeCar;
    [SerializeField] private F1CarController f1Car;

    [Header("═══ SPEED THRESHOLDS ═══")]
    [Tooltip("Speed in KM/H where Motion Blur starts to kick in (Requested: 200 KM/H)")]
    [SerializeField] private float motionBlurStartSpeedKph = 200f;
    [Tooltip("Speed in KM/H where Motion Blur reaches maximum intensity")]
    [SerializeField] private float maxEffectSpeedKph = 360f;

    [Header("═══ MOTION BLUR SETTINGS ═══")]
    [Range(0f, 1f)]
    [SerializeField] private float baseMotionBlurIntensity = 0.05f;
    [Range(0f, 1f)]
    [SerializeField] private float highSpeedMotionBlurIntensity = 0.85f;
    [SerializeField] private MotionBlurQuality motionBlurQuality = MotionBlurQuality.Medium;

    [Header("═══ SPEED TUNNEL VISION (LENS EFFECTS) ═══")]
    [Tooltip("Subtle chromatic aberration at extreme speeds")]
    [SerializeField] private bool enableSpeedChromaticAberration = true;
    [Range(0f, 1f)]
    [SerializeField] private float maxChromaticAberration = 0.45f;

    [Tooltip("Slight dynamic FOV lens distortion for sense of speed")]
    [SerializeField] private bool enableSpeedLensDistortion = true;
    [Range(-0.4f, 0.4f)]
    [SerializeField] private float maxLensDistortion = -0.15f;

    [Header("═══ VIGNETTE SPEED CONTRACTION ═══")]
    [Range(0.1f, 0.6f)]
    [SerializeField] private float baseVignette = 0.20f;
    [Range(0.2f, 0.8f)]
    [SerializeField] private float highSpeedVignette = 0.36f;

    [Header("═══ SMOOTHING ═══")]
    [SerializeField] private float effectSmoothing = 6.0f;

    #endregion

    #region ──── RUNTIME FIELDS ────

    private Volume volume;
    private VolumeProfile profile;

    // Volume Overrides
    private MotionBlur motionBlur;
    private ChromaticAberration chromaticAberration;
    private LensDistortion lensDistortion;
    private Vignette vignette;
    private Bloom bloom;
    private Tonemapping tonemapping;

    // Smoothed internal values
    private float currentSpeedKph;
    private float currentBlurIntensity;
    private float currentChromaIntensity;
    private float currentDistortionIntensity;
    private float currentVignetteIntensity;

    #endregion

    #region ──── LIFECYCLE ────

    private void Awake()
    {
        volume = GetComponent<Volume>();
        if (volume == null) volume = gameObject.AddComponent<Volume>();
        volume.isGlobal = true;

        // Use sharedProfile when available to prevent creating transient leaked copies
        profile = volume.sharedProfile != null ? volume.sharedProfile : volume.profile;
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            volume.profile = profile;
        }

        InitializeVolumeOverrides();
        ResolveCar();
    }

    private void Update()
    {
        if (arcadeCar == null && f1Car == null)
        {
            ResolveCar();
        }

        if (arcadeCar != null)
        {
            currentSpeedKph = arcadeCar.SpeedKph;
        }
        else if (f1Car != null)
        {
            currentSpeedKph = f1Car.SpeedKph;
        }
        else
        {
            currentSpeedKph = 0f;
        }

        UpdateSpeedBasedEffects();
    }

    #endregion

    #region ──── INITIALIZATION ────

    private void ResolveCar()
    {
        if (arcadeCar == null)
        {
            arcadeCar = Object.FindFirstObjectByType<FormulaCarArcadeController>();
        }

        if (arcadeCar == null && f1Car == null)
        {
            f1Car = Object.FindFirstObjectByType<F1CarController>();
        }
    }

    private T SafeGetOrAddOverride<T>() where T : VolumeComponent
    {
        if (profile == null) return null;

        try
        {
            if (profile.TryGet(out T comp))
            {
                // In Unity, destroyed ScriptableObjects return true for obj == null
                if (comp != null && !comp.Equals(null))
                {
                    return comp;
                }
            }

            // Clean any destroyed instances in the components list
            for (int i = profile.components.Count - 1; i >= 0; i--)
            {
                if (profile.components[i] == null || profile.components[i].Equals(null) || profile.components[i] is T)
                {
                    profile.components.RemoveAt(i);
                }
            }

            return profile.Add<T>(true);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[F1DynamicPostProcessing] Failed to get or add override of type {typeof(T).Name}: {ex.Message}");
            return null;
        }
    }

    private void InitializeVolumeOverrides()
    {
        if (profile == null) return;

        // 1. Motion Blur
        motionBlur = SafeGetOrAddOverride<MotionBlur>();
        if (motionBlur != null)
        {
            motionBlur.intensity.overrideState = true;
            motionBlur.intensity.value = baseMotionBlurIntensity;
            motionBlur.quality.overrideState = true;
            motionBlur.quality.value = motionBlurQuality;
        }

        // 2. Chromatic Aberration
        chromaticAberration = SafeGetOrAddOverride<ChromaticAberration>();
        if (chromaticAberration != null)
        {
            chromaticAberration.intensity.overrideState = true;
            chromaticAberration.intensity.value = 0f;
        }

        // 3. Lens Distortion
        lensDistortion = SafeGetOrAddOverride<LensDistortion>();
        if (lensDistortion != null)
        {
            lensDistortion.intensity.overrideState = true;
            lensDistortion.intensity.value = 0f;
        }

        // 4. Vignette
        vignette = SafeGetOrAddOverride<Vignette>();
        if (vignette != null)
        {
            vignette.intensity.overrideState = true;
            vignette.intensity.value = baseVignette;
            vignette.smoothness.overrideState = true;
            vignette.smoothness.value = 0.38f;
        }

        // 5. ACES Tonemapping
        tonemapping = SafeGetOrAddOverride<Tonemapping>();
        if (tonemapping != null)
        {
            tonemapping.mode.overrideState = true;
            tonemapping.mode.value = TonemappingMode.ACES;
        }

        // 6. Cinematic Bloom for sun glints and reflections
        bloom = SafeGetOrAddOverride<Bloom>();
        if (bloom != null)
        {
            bloom.threshold.overrideState = true;
            bloom.threshold.value = 1.0f;
            bloom.intensity.overrideState = true;
            bloom.intensity.value = 0.75f;
            bloom.scatter.overrideState = true;
            bloom.scatter.value = 0.70f;
        }
    }

    #endregion

    #region ──── DYNAMIC SPEED PROCESSING ────

    private void UpdateSpeedBasedEffects()
    {
        // Calculate speed factor between 200 KM/H and max speed
        float speedFactor = Mathf.Clamp01(
            (currentSpeedKph - motionBlurStartSpeedKph) / Mathf.Max(10f, maxEffectSpeedKph - motionBlurStartSpeedKph)
        );

        // Target values
        float targetBlur = Mathf.Lerp(baseMotionBlurIntensity, highSpeedMotionBlurIntensity, speedFactor);
        float targetChroma = enableSpeedChromaticAberration ? Mathf.Lerp(0f, maxChromaticAberration, speedFactor) : 0f;
        float targetDistortion = enableSpeedLensDistortion ? Mathf.Lerp(0f, maxLensDistortion, speedFactor) : 0f;
        float targetVignette = Mathf.Lerp(baseVignette, highSpeedVignette, speedFactor);

        // Smooth transitions
        float dt = Time.deltaTime * effectSmoothing;
        currentBlurIntensity = Mathf.Lerp(currentBlurIntensity, targetBlur, dt);
        currentChromaIntensity = Mathf.Lerp(currentChromaIntensity, targetChroma, dt);
        currentDistortionIntensity = Mathf.Lerp(currentDistortionIntensity, targetDistortion, dt);
        currentVignetteIntensity = Mathf.Lerp(currentVignetteIntensity, targetVignette, dt);

        // Apply safely to Volume overrides
        if (motionBlur != null && !motionBlur.Equals(null))
        {
            motionBlur.intensity.value = currentBlurIntensity;
        }

        if (chromaticAberration != null && !chromaticAberration.Equals(null))
        {
            chromaticAberration.intensity.value = currentChromaIntensity;
        }

        if (lensDistortion != null && !lensDistortion.Equals(null))
        {
            lensDistortion.intensity.value = currentDistortionIntensity;
        }

        if (vignette != null && !vignette.Equals(null))
        {
            vignette.intensity.value = currentVignetteIntensity;
        }
    }

    #endregion

    #region ──── PUBLIC CONFIGURATION API ────

    public void SetCarController(FormulaCarArcadeController car)
    {
        arcadeCar = car;
    }

    public void SetCarController(F1CarController car)
    {
        f1Car = car;
    }

    public void SetSpeedThresholds(float startSpeedKph, float maxSpeedKph)
    {
        motionBlurStartSpeedKph = startSpeedKph;
        maxEffectSpeedKph = maxSpeedKph;
    }

    #endregion
}
