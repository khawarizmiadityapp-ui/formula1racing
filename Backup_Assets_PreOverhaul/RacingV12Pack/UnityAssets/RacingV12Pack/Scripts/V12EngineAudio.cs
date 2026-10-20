using UnityEngine;

/// <summary>
/// Fictional V12 Turbo racing engine audio crossfader and effects processor.
/// Seamlessly blends idle, mid-range, and screaming high-RPM loops, with dynamic gear shift clicks,
/// turbo wastegate blowoff hisses, and tire scrub squeals during high-G cornering.
/// Automatically creates and configures required AudioSources cleanly.
/// </summary>
[DisallowMultipleComponent]
public class V12EngineAudio : MonoBehaviour
{
    [Header("Engine Loops")]
    [SerializeField] private AudioClip idleClip;
    [SerializeField] private AudioClip midClip;
    [SerializeField] private AudioClip highClip;

    [Header("Sound Effects")]
    [SerializeField] private AudioClip gearShiftClip;
    [SerializeField] private AudioClip turboWastegateClip;
    [SerializeField] private AudioClip tireScrubClip;

    [Header("Audio Sources (Auto-created if null)")]
    [SerializeField] private AudioSource idleSource;
    [SerializeField] private AudioSource midSource;
    [SerializeField] private AudioSource highSource;
    [SerializeField] private AudioSource effectsSource;
    [SerializeField] private AudioSource tireScrubSource;

    [Header("Vehicle Reference")]
    [SerializeField] private Rigidbody targetRigidbody;
    [SerializeField] private FormulaCarArcadeController carController;

    [Header("Tuning")]
    [SerializeField] private float idleRpm = 1400f;
    [SerializeField] private float maxRpm = 12500f;
    [SerializeField] private float shiftCooldown = 0.38f;
    [SerializeField] [Range(0f, 1f)] private float masterEngineVolume = 0.85f;

    // Public properties for HUD
    public int CurrentGear { get; private set; } = 1;
    public float CurrentRpm { get; private set; } = 1400f;
    public float Rpm01 => Mathf.InverseLerp(idleRpm, maxRpm, CurrentRpm);

    private float smoothedRpm;
    private float lastShiftTime = -10f;
    private int lastGear = 1;
    private float nextWastegateTime = 0f;
    private float lastThrottle = 0f;

    // Gear speed thresholds (km/h) for 8-speed sequential box (calibrated up to 375 km/h)
    private static readonly float[] GearTopSpeeds = { 0f, 65f, 115f, 168f, 218f, 268f, 312f, 348f, 375f };

    private void Awake()
    {
        ResolveReferences();
        SetupAudioSources();
    }

    private void OnValidate()
    {
        ResolveReferences();
    }

    private void ResolveReferences()
    {
        if (targetRigidbody == null) targetRigidbody = GetComponentInParent<Rigidbody>();
        if (carController == null) carController = GetComponentInParent<FormulaCarArcadeController>();
    }

    private void SetupAudioSources()
    {
        idleSource = GetOrCreateSource(idleSource, "Audio_IdleLoop", idleClip, true);
        midSource = GetOrCreateSource(midSource, "Audio_MidLoop", midClip, true);
        highSource = GetOrCreateSource(highSource, "Audio_HighLoop", highClip, true);
        effectsSource = GetOrCreateSource(effectsSource, "Audio_Effects", null, false);
        tireScrubSource = GetOrCreateSource(tireScrubSource, "Audio_TireScrub", tireScrubClip, true);
    }

    private AudioSource GetOrCreateSource(AudioSource existing, string childName, AudioClip clip, bool loop)
    {
        if (existing != null)
        {
            if (clip != null && existing.clip == null) existing.clip = clip;
            existing.loop = loop;
            existing.playOnAwake = false;
            return existing;
        }

        Transform child = transform.Find(childName);
        GameObject sourceGo;
        if (child != null)
        {
            sourceGo = child.gameObject;
        }
        else
        {
            sourceGo = new GameObject(childName);
            sourceGo.transform.SetParent(transform, false);
        }

        AudioSource src = sourceGo.GetComponent<AudioSource>();
        if (src == null) src = sourceGo.AddComponent<AudioSource>();

        src.clip = clip;
        src.loop = loop;
        src.playOnAwake = false;
        src.spatialBlend = 0.4f; // Semi-spatialized for rich cockpit feel
        src.dopplerLevel = 0.5f;
        src.minDistance = 3f;
        src.maxDistance = 60f;
        src.volume = 0f;
        return src;
    }

    private void Start()
    {
        StartPlayingLoops();
    }

    private void OnEnable()
    {
        StartPlayingLoops();
    }

    private void OnDisable()
    {
        StopPlayingLoops();
    }

    private void StartPlayingLoops()
    {
        PlaySourceSafe(idleSource);
        PlaySourceSafe(midSource);
        PlaySourceSafe(highSource);
        PlaySourceSafe(tireScrubSource);
    }

    private void StopPlayingLoops()
    {
        StopSourceSafe(idleSource);
        StopSourceSafe(midSource);
        StopSourceSafe(highSource);
        StopSourceSafe(tireScrubSource);
        StopSourceSafe(effectsSource);
    }

    private static void PlaySourceSafe(AudioSource src)
    {
        if (src != null && src.clip != null && !src.isPlaying)
        {
            src.Play();
        }
    }

    private static void StopSourceSafe(AudioSource src)
    {
        if (src != null && src.isPlaying)
        {
            src.Stop();
        }
    }

    private void Update()
    {
        float speedKph = targetRigidbody != null ? targetRigidbody.linearVelocity.magnitude * 3.6f : 0f;
        float throttle = carController != null ? carController.ThrottleInput : 0f;
        bool isReversing = carController != null && carController.IsReversing;

        // Gear calculation
        int gear = 1;
        if (isReversing)
        {
            gear = 0; // Reverse indicator
        }
        else
        {
            for (int g = 1; g < GearTopSpeeds.Length; g++)
            {
                if (speedKph <= GearTopSpeeds[g] || g == GearTopSpeeds.Length - 1)
                {
                    gear = g;
                    break;
                }
            }
        }
        CurrentGear = gear;

        // Realistic RPM modeling within gear ratios
        float gearMinSpeed = gear > 1 ? GearTopSpeeds[gear - 1] : 0f;
        float gearMaxSpeed = gear > 0 ? GearTopSpeeds[gear] : 25f;
        float gearRatioProgress = Mathf.Clamp01((speedKph - gearMinSpeed) / Mathf.Max(5f, gearMaxSpeed - gearMinSpeed));

        float baseGearRpm = Mathf.Lerp(gear == 1 ? idleRpm : 6500f, maxRpm, gearRatioProgress);
        float targetRpm = Mathf.Clamp(baseGearRpm + throttle * 1200f, idleRpm, maxRpm);
        if (speedKph < 2f && throttle < 0.05f)
        {
            targetRpm = idleRpm + Mathf.Sin(Time.time * 2.5f) * 60f; // Subtle idle pulse
        }

        // Smooth RPM transition
        float rpmSmoothing = (targetRpm > smoothedRpm) ? 8f : 5f;
        smoothedRpm = Mathf.Lerp(smoothedRpm, targetRpm, 1f - Mathf.Exp(-Time.deltaTime * rpmSmoothing));
        CurrentRpm = smoothedRpm;

        float normRpm = Rpm01;

        // Crossfade loops
        // Idle loop: highest at idle, fades out by mid RPM
        float idleBlend = Mathf.Clamp01(1f - Mathf.InverseLerp(0.05f, 0.45f, normRpm));
        float idlePitch = Mathf.Lerp(0.85f, 1.25f, normRpm);
        SetLayer(idleSource, idlePitch, 0.90f * masterEngineVolume, idleBlend);

        // Mid loop: peaks in the middle range (35% to 75% RPM)
        float midBlend = Mathf.Clamp01(1f - Mathf.Abs(normRpm - 0.52f) / 0.38f);
        float midPitch = Mathf.Lerp(0.80f, 1.15f, normRpm);
        SetLayer(midSource, midPitch, 0.95f * masterEngineVolume, midBlend);

        // High loop: screams in above 50% RPM and dominates at redline
        float highBlend = Mathf.Clamp01(Mathf.InverseLerp(0.40f, 0.85f, normRpm));
        float highPitch = Mathf.Lerp(0.78f, 1.22f, normRpm);
        SetLayer(highSource, highPitch, 1.0f * masterEngineVolume, highBlend);

        // Gear shift click effect
        if (gear != lastGear && Time.time - lastShiftTime > shiftCooldown && speedKph > 15f)
        {
            if (gearShiftClip != null && effectsSource != null)
            {
                effectsSource.PlayOneShot(gearShiftClip, 0.75f);
            }
            lastGear = gear;
            lastShiftTime = Time.time;
        }

        // Turbo wastegate hiss when lifting off full throttle at speed
        bool throttleLift = (lastThrottle > 0.65f && throttle < 0.25f);
        if (throttleLift && speedKph > 60f && Time.time > nextWastegateTime)
        {
            if (turboWastegateClip != null && effectsSource != null)
            {
                effectsSource.pitch = Random.Range(0.92f, 1.08f);
                effectsSource.PlayOneShot(turboWastegateClip, 0.55f);
                nextWastegateTime = Time.time + 1.2f;
            }
        }
        lastThrottle = throttle;

        // Tire scrub squeal during lateral slide or heavy braking
        if (tireScrubSource != null && targetRigidbody != null)
        {
            Vector3 localVel = transform.InverseTransformDirection(targetRigidbody.linearVelocity);
            float lateralSpeed = Mathf.Abs(localVel.x);
            bool heavyBrake = carController != null && carController.BrakeInput > 0.5f && speedKph > 40f;
            float scrubFactor = Mathf.Clamp01((lateralSpeed - 1.8f) / 5.5f) + (heavyBrake ? 0.35f : 0f);

            if (scrubFactor > 0.05f)
            {
                tireScrubSource.volume = Mathf.Lerp(tireScrubSource.volume, Mathf.Clamp01(scrubFactor * 0.75f), Time.deltaTime * 10f);
                tireScrubSource.pitch = Mathf.Lerp(0.90f, 1.25f, scrubFactor);
            }
            else
            {
                tireScrubSource.volume = Mathf.Lerp(tireScrubSource.volume, 0f, Time.deltaTime * 8f);
            }
        }
    }

    private static void SetLayer(AudioSource source, float pitch, float volume, float blend)
    {
        if (source == null) return;
        source.pitch = pitch;
        source.volume = Mathf.Clamp01(volume * blend);
    }
}
