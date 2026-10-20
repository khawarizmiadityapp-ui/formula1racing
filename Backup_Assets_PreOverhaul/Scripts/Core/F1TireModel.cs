using UnityEngine;

/// <summary>
/// F1 tire physics and telemetry model. Tracks temperature, wear, and grip for each of the 4 tires.
/// Provides real-time data for the HUD tire condition indicators.
///
/// Tire compounds: Soft (merah), Medium (kuning), Hard (putih), Intermediate (hijau), Wet (biru).
///
/// Attach to: Same GameObject as F1CarController.
/// Dependencies: F1CarController
/// </summary>
public class F1TireModel : MonoBehaviour
{
    #region ──── ENUMS ────

    public enum TireCompound { Soft, Medium, Hard, Intermediate, Wet }

    public enum TireCondition { Optimal, Warm, Hot, Critical, Dead }

    #endregion

    #region ──── DATA STRUCTURES ────

    [System.Serializable]
    public class TireData
    {
        public string name;
        public float temperature = 80f;   // °C
        public float wear = 100f;          // 0-100%
        public float grip = 1f;            // 0-1 multiplier
        public float pressure = 23f;       // PSI (F1 ~20-25)
        public bool isLocked;              // lock-up under braking
        public bool isSlipping;            // wheelspin/slip

        public TireCondition GetCondition()
        {
            if (wear <= 5f) return TireCondition.Dead;
            if (temperature > 130f || wear < 20f) return TireCondition.Critical;
            if (temperature > 110f) return TireCondition.Hot;
            if (temperature > 85f) return TireCondition.Optimal;
            return TireCondition.Warm;
        }

        /// <summary>Get condition color for HUD display.</summary>
        public Color GetConditionColor()
        {
            return GetCondition() switch
            {
                TireCondition.Optimal => new Color(0.1f, 0.85f, 0.35f),  // Green
                TireCondition.Warm => new Color(0.1f, 0.7f, 0.3f),      // Dim Green
                TireCondition.Hot => new Color(1f, 0.85f, 0.1f),         // Yellow
                TireCondition.Critical => new Color(1f, 0.2f, 0.15f),    // Red
                TireCondition.Dead => new Color(0.4f, 0.4f, 0.4f),      // Grey
                _ => Color.white,
            };
        }
    }

    #endregion

    #region ──── INSPECTOR FIELDS ────

    [Header("═══ COMPOUND ═══")]
    [SerializeField] private TireCompound currentCompound = TireCompound.Medium;

    [Header("═══ THERMAL MODEL ═══")]
    [Tooltip("Base optimal temperature range")]
    [SerializeField] private float optimalTempMin = 85f;
    [SerializeField] private float optimalTempMax = 110f;
    [Tooltip("Heat generation per m/s of speed")]
    [SerializeField] private float heatRateFromSpeed = 0.015f;
    [Tooltip("Extra heat from lateral sliding")]
    [SerializeField] private float heatRateFromSlide = 0.08f;
    [Tooltip("Heat from braking")]
    [SerializeField] private float heatRateFromBrake = 0.12f;
    [Tooltip("Cooling rate (temperature decrease per second)")]
    [SerializeField] private float coolingRate = 2.5f;

    [Header("═══ WEAR MODEL ═══")]
    [Tooltip("Base wear rate per second at speed")]
    [SerializeField] private float baseWearRate = 0.008f;
    [Tooltip("Extra wear from sliding")]
    [SerializeField] private float slideWearRate = 0.04f;
    [Tooltip("Extra wear when overheated")]
    [SerializeField] private float overheatWearMultiplier = 2.5f;

    [Header("═══ GRIP MODEL ═══")]
    [Tooltip("Grip at optimal temperature")]
    [SerializeField] private float maxGrip = 1.0f;
    [Tooltip("Minimum grip when tires are dead")]
    [SerializeField] private float minGrip = 0.4f;

    #endregion

    #region ──── RUNTIME ────

    private F1CarController carController;

    // 0=FL, 1=FR, 2=RL, 3=RR
    private TireData[] tires;

    #endregion

    #region ──── PUBLIC PROPERTIES ────

    public TireData[] Tires => tires;
    public TireData FrontLeft => tires[0];
    public TireData FrontRight => tires[1];
    public TireData RearLeft => tires[2];
    public TireData RearRight => tires[3];
    public TireCompound CurrentCompound => currentCompound;
    /// <summary>Average grip across all tires (0-1)</summary>
    public float AverageGrip => (tires[0].grip + tires[1].grip + tires[2].grip + tires[3].grip) / 4f;

    /// <summary>Get compound display color.</summary>
    public Color GetCompoundColor()
    {
        return currentCompound switch
        {
            TireCompound.Soft => new Color(1f, 0.15f, 0.15f),       // Red
            TireCompound.Medium => new Color(1f, 0.85f, 0.1f),      // Yellow
            TireCompound.Hard => Color.white,                         // White
            TireCompound.Intermediate => new Color(0.1f, 0.8f, 0.3f), // Green
            TireCompound.Wet => new Color(0.2f, 0.5f, 1f),           // Blue
            _ => Color.white,
        };
    }

    /// <summary>Get compound display name.</summary>
    public string GetCompoundName()
    {
        return currentCompound switch
        {
            TireCompound.Soft => "LUNAK",
            TireCompound.Medium => "SEDANG",
            TireCompound.Hard => "KERAS",
            TireCompound.Intermediate => "INTER",
            TireCompound.Wet => "BASAH",
            _ => "?",
        };
    }

    #endregion

    #region ──── LIFECYCLE ────

    private void Awake()
    {
        carController = GetComponent<F1CarController>();
        InitializeTires();
    }

    private void Update()
    {
        if (carController == null) return;
        UpdateTirePhysics(Time.deltaTime);
    }

    #endregion

    #region ──── INITIALIZATION ────

    private void InitializeTires()
    {
        tires = new TireData[4];
        string[] names = { "FL", "FR", "RL", "RR" };
        for (int i = 0; i < 4; i++)
        {
            tires[i] = new TireData
            {
                name = names[i],
                temperature = 80f,
                wear = 100f,
                grip = maxGrip,
                pressure = 23f,
            };
        }
    }

    /// <summary>Reset all tires to fresh state (pit stop).</summary>
    public void ResetTires(TireCompound compound)
    {
        currentCompound = compound;
        for (int i = 0; i < 4; i++)
        {
            tires[i].temperature = 70f;
            tires[i].wear = 100f;
            tires[i].grip = maxGrip;
            tires[i].pressure = 23f;
            tires[i].isLocked = false;
            tires[i].isSlipping = false;
        }
    }

    #endregion

    #region ──── TIRE PHYSICS UPDATE ────

    private void UpdateTirePhysics(float dt)
    {
        float speedKph = carController.SpeedKph;
        float speedMs = speedKph / 3.6f;
        float lateralAbs = Mathf.Abs(carController.LateralSpeed);
        float brakeInput = carController.BrakeInput;
        float throttleInput = carController.ThrottleInput;

        // Compound modifiers
        float compoundWearMod = currentCompound switch
        {
            TireCompound.Soft => 1.6f,
            TireCompound.Medium => 1.0f,
            TireCompound.Hard => 0.65f,
            TireCompound.Intermediate => 1.1f,
            TireCompound.Wet => 1.2f,
            _ => 1f,
        };

        float compoundGripMod = currentCompound switch
        {
            TireCompound.Soft => 1.08f,
            TireCompound.Medium => 1.0f,
            TireCompound.Hard => 0.92f,
            TireCompound.Intermediate => 0.88f,
            TireCompound.Wet => 0.78f,
            _ => 1f,
        };

        for (int i = 0; i < 4; i++)
        {
            var tire = tires[i];
            bool isFront = (i < 2);
            bool isLeft = (i == 0 || i == 2);

            // ── Temperature ──
            // Heat from speed
            float heatGenerated = speedMs * heatRateFromSpeed;

            // Extra heat from lateral sliding
            heatGenerated += lateralAbs * heatRateFromSlide;

            // Front tires heat more from braking, rears from throttle
            if (isFront)
            {
                heatGenerated += brakeInput * heatRateFromBrake * speedMs * 0.1f;
            }
            else
            {
                heatGenerated += throttleInput * heatRateFromBrake * 0.5f;
            }

            // Asymmetric heating from cornering (outer tires get hotter)
            float steer = carController.CurrentSteer;
            bool isOuter = (steer > 0.1f && isLeft) || (steer < -0.1f && !isLeft);
            if (isOuter) heatGenerated *= 1.3f;

            // Cooling
            float cooling = coolingRate * dt;
            if (speedMs > 10f) cooling *= 1.2f; // Air cooling at speed

            tire.temperature += heatGenerated * dt;
            tire.temperature -= cooling;
            tire.temperature = Mathf.Clamp(tire.temperature, 30f, 160f);

            // ── Wear ──
            float wearAmount = baseWearRate * speedMs * 0.01f * compoundWearMod * dt;
            wearAmount += lateralAbs * slideWearRate * compoundWearMod * dt * 0.1f;

            // Overheat degrades tires faster
            if (tire.temperature > optimalTempMax)
            {
                float overheatFactor = (tire.temperature - optimalTempMax) / 30f;
                wearAmount *= 1f + overheatFactor * overheatWearMultiplier;
            }

            tire.wear -= wearAmount;
            tire.wear = Mathf.Clamp(tire.wear, 0f, 100f);

            // ── Grip calculation ──
            // Temperature-based grip curve (peak at optimal, drops off on both sides)
            float tempCenter = (optimalTempMin + optimalTempMax) / 2f;
            float tempRange = (optimalTempMax - optimalTempMin) / 2f;
            float tempFactor = 1f - Mathf.Clamp01(Mathf.Abs(tire.temperature - tempCenter) / (tempRange * 2f));

            // Wear-based grip
            float wearFactor = Mathf.Lerp(0.5f, 1f, tire.wear / 100f);

            tire.grip = Mathf.Lerp(minGrip, maxGrip * compoundGripMod, tempFactor * wearFactor);

            // ── Lock-up / Wheelspin detection ──
            tire.isLocked = isFront && brakeInput > 0.8f && speedKph > 50f && tire.grip < 0.7f;
            tire.isSlipping = !isFront && throttleInput > 0.8f && speedKph < 80f && tire.grip < 0.7f;

            // ── Pressure variation ──
            tire.pressure = 23f + (tire.temperature - 80f) * 0.03f;
        }
    }

    #endregion
}
