using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// DRS (Drag Reduction System) / ALA and ERS (Energy Recovery System) / OTK+SET controller.
///
/// DRS (ALA button): Opens rear wing flap in straight zones to reduce drag → +15-20 km/h top speed.
/// ERS (OTK button): Deploys battery energy for instant overtake boost (~160 HP extra).
/// ERS (SET button): Switches engine mode for energy recovery (harvest / balanced / deploy).
///
/// Attach to: Same GameObject as F1CarController.
/// Dependencies: F1CarController
/// </summary>
public class F1DRSERSSystem : MonoBehaviour
{
    #region ──── ENUMS ────

    public enum DRSState { Closed, Opening, Open, Closing }
    public enum ERSMode { Harvest, Balanced, Deploy }
    public enum EngineMode { Conserve, Normal, Aggressive, Overtake }

    #endregion

    #region ──── INSPECTOR FIELDS ────

    [Header("═══ DRS / ALA ═══")]
    [Tooltip("Additional speed multiplier when DRS is open")]
    [SerializeField] private float drsSpeedBoost = 8f;
    [Tooltip("Time to fully open DRS flap (seconds)")]
    [SerializeField] private float drsOpenTime = 0.3f;
    [Tooltip("Time to fully close DRS flap (seconds)")]
    [SerializeField] private float drsCloseTime = 0.2f;
    [Tooltip("Minimum speed (km/h) to activate DRS")]
    [SerializeField] private float drsMinSpeedKph = 100f;
    [Tooltip("DRS automatically closes when braking")]
    [SerializeField] private bool drsCloseOnBrake = true;
    [Tooltip("DRS only available in designated zones (controlled externally)")]
    [SerializeField] private bool drsRequireZone = false;

    [Header("═══ ERS / OTK ═══")]
    [Tooltip("Total battery energy in kJ (F1 = 4000 kJ per lap, scaled for gameplay)")]
    [SerializeField] private float maxBatteryEnergy = 4000f;
    [Tooltip("Energy deployment rate in kW when OTK is active (F1 = 120 kW)")]
    [SerializeField] private float deployPowerKW = 120f;
    [Tooltip("Energy harvest rate in kW during braking/coasting")]
    [SerializeField] private float harvestPowerKW = 60f;
    [Tooltip("ERS boost force applied to car")]
    [SerializeField] private float ersBoostForce = 12f;
    [Tooltip("Duration of OTK (overtake) boost in seconds")]
    [SerializeField] private float otkBoostDuration = 8f;
    [Tooltip("Cooldown after OTK use (seconds)")]
    [SerializeField] private float otkCooldown = 12f;

    [Header("═══ ENGINE MODES / SET ═══")]
    [Tooltip("Engine mode fuel consumption multiplier")]
    [SerializeField] private float conserveFuelRate = 0.7f;
    [SerializeField] private float normalFuelRate = 1.0f;
    [SerializeField] private float aggressiveFuelRate = 1.25f;
    [SerializeField] private float overtakeFuelRate = 1.5f;

    [Header("═══ VISUAL REFERENCES ═══")]
    [Tooltip("Rear wing flap transform for DRS animation")]
    [SerializeField] private Transform rearWingFlap;
    [Tooltip("DRS open rotation (local Euler)")]
    [SerializeField] private Vector3 drsOpenRotation = new Vector3(-25f, 0f, 0f);
    [Tooltip("DRS closed rotation (local Euler)")]
    [SerializeField] private Vector3 drsClosedRotation = Vector3.zero;

    #endregion

    #region ──── RUNTIME STATE ────

    private F1CarController carController;

    // DRS
    private DRSState drsState = DRSState.Closed;
    private float drsProgress; // 0 = closed, 1 = open
    private bool drsZoneActive = true;
    private bool drsButtonHeld;

    // ERS
    private float currentBatteryEnergy;
    private ERSMode ersMode = ERSMode.Balanced;
    private EngineMode engineMode = EngineMode.Normal;
    private bool otkActive;
    private float otkTimer;
    private float otkCooldownTimer;
    private bool otkButtonPressed;
    private bool setButtonPressed;

    // Mobile input flags
    private bool mobileALAPressed;
    private bool mobileOTKPressed;
    private bool mobileSETPressed;

    #endregion

    #region ──── PUBLIC PROPERTIES ────

    /// <summary>DRS flap state</summary>
    public DRSState CurrentDRSState => drsState;
    /// <summary>DRS flap progress 0-1</summary>
    public float DRSProgress => drsProgress;
    /// <summary>Is DRS fully open</summary>
    public bool IsDRSOpen => drsState == DRSState.Open;
    /// <summary>Is DRS zone active</summary>
    public bool IsDRSZoneActive => drsZoneActive;
    /// <summary>Can DRS be activated right now</summary>
    public bool CanActivateDRS => (drsZoneActive || !drsRequireZone)
        && carController != null && carController.SpeedKph >= drsMinSpeedKph
        && carController.BrakeInput < 0.05f;

    /// <summary>Current battery energy (kJ)</summary>
    public float BatteryEnergy => currentBatteryEnergy;
    /// <summary>Max battery energy (kJ)</summary>
    public float MaxBatteryEnergy => maxBatteryEnergy;
    /// <summary>Battery charge 0-1</summary>
    public float BatteryPercent => currentBatteryEnergy / maxBatteryEnergy;
    /// <summary>Current ERS mode</summary>
    public ERSMode CurrentERSMode => ersMode;
    /// <summary>Current engine mode</summary>
    public EngineMode CurrentEngineMode => engineMode;
    /// <summary>Is OTK boost currently active</summary>
    public bool IsOTKActive => otkActive;
    /// <summary>OTK remaining time</summary>
    public float OTKRemainingTime => otkActive ? otkTimer : 0f;
    /// <summary>OTK cooldown remaining</summary>
    public float OTKCooldownRemaining => otkCooldownTimer;
    /// <summary>Can OTK be activated</summary>
    public bool CanActivateOTK => !otkActive && otkCooldownTimer <= 0f
        && currentBatteryEnergy > deployPowerKW * 0.5f;

    #endregion

    #region ──── EVENTS ────

    public event System.Action OnDRSOpened;
    public event System.Action OnDRSClosed;
    public event System.Action OnOTKActivated;
    public event System.Action OnOTKDeactivated;
    public event System.Action<EngineMode> OnEngineModeChanged;

    #endregion

    #region ──── LIFECYCLE ────

    private void Awake()
    {
        carController = GetComponent<F1CarController>();
        currentBatteryEnergy = maxBatteryEnergy * 0.8f; // Start at 80%
    }

    private void Update()
    {
        ReadDRSERSInputs();
        UpdateDRS();
        UpdateERS();
        UpdateEngineMode();
    }

    #endregion

    #region ──── INPUT ────

    /// <summary>
    /// Mobile HUD calls this for ALA/OTK/SET button presses.
    /// </summary>
    public void SetMobileALA(bool pressed) => mobileALAPressed = pressed;
    public void SetMobileOTK(bool pressed) => mobileOTKPressed = pressed;
    public void SetMobileSET(bool pressed) => mobileSETPressed = pressed;

    /// <summary>
    /// Called by DRS zone triggers in the track.
    /// </summary>
    public void SetDRSZone(bool active) => drsZoneActive = active;

    private void ReadDRSERSInputs()
    {
        drsButtonHeld = mobileALAPressed;
        otkButtonPressed = false;
        setButtonPressed = false;

#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            // F = DRS (ALA)
            if (kb.fKey.isPressed) drsButtonHeld = true;
            // G = OTK (Overtake)
            if (kb.gKey.wasPressedThisFrame) otkButtonPressed = true;
            // H = SET (Engine Mode)
            if (kb.hKey.wasPressedThisFrame) setButtonPressed = true;
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        try
        {
            if (Input.GetKey(KeyCode.F)) drsButtonHeld = true;
            if (Input.GetKeyDown(KeyCode.G)) otkButtonPressed = true;
            if (Input.GetKeyDown(KeyCode.H)) setButtonPressed = true;
        }
        catch { }
#endif

        if (mobileOTKPressed) { otkButtonPressed = true; mobileOTKPressed = false; }
        if (mobileSETPressed) { setButtonPressed = true; mobileSETPressed = false; }
    }

    #endregion

    #region ──── DRS LOGIC ────

    private void UpdateDRS()
    {
        bool wantOpen = drsButtonHeld && CanActivateDRS;

        // Auto-close on brake
        if (drsCloseOnBrake && carController != null && carController.BrakeInput > 0.1f)
        {
            wantOpen = false;
        }

        switch (drsState)
        {
            case DRSState.Closed:
                if (wantOpen)
                {
                    drsState = DRSState.Opening;
                }
                break;

            case DRSState.Opening:
                drsProgress += Time.deltaTime / drsOpenTime;
                if (drsProgress >= 1f)
                {
                    drsProgress = 1f;
                    drsState = DRSState.Open;
                    OnDRSOpened?.Invoke();
                }
                if (!wantOpen)
                {
                    drsState = DRSState.Closing;
                }
                break;

            case DRSState.Open:
                if (!wantOpen)
                {
                    drsState = DRSState.Closing;
                    OnDRSClosed?.Invoke();
                }
                // Apply speed boost
                carController?.ApplyDRSBoost(drsSpeedBoost);
                break;

            case DRSState.Closing:
                drsProgress -= Time.deltaTime / drsCloseTime;
                if (drsProgress <= 0f)
                {
                    drsProgress = 0f;
                    drsState = DRSState.Closed;
                }
                if (wantOpen)
                {
                    drsState = DRSState.Opening;
                }
                break;
        }

        // Animate rear wing flap
        if (rearWingFlap != null)
        {
            rearWingFlap.localRotation = Quaternion.Euler(
                Vector3.Lerp(drsClosedRotation, drsOpenRotation, drsProgress)
            );
        }
    }

    #endregion

    #region ──── ERS / OTK LOGIC ────

    private void UpdateERS()
    {
        float dt = Time.deltaTime;

        // OTK cooldown
        if (otkCooldownTimer > 0f)
        {
            otkCooldownTimer -= dt;
        }

        // OTK button press
        if (otkButtonPressed && CanActivateOTK)
        {
            otkActive = true;
            otkTimer = otkBoostDuration;
            OnOTKActivated?.Invoke();
        }

        // OTK active: deploy energy
        if (otkActive)
        {
            otkTimer -= dt;
            float energyCost = deployPowerKW * dt;
            currentBatteryEnergy -= energyCost;

            // Apply boost
            carController?.ApplyERSBoost(ersBoostForce);

            if (otkTimer <= 0f || currentBatteryEnergy <= 0f)
            {
                otkActive = false;
                otkCooldownTimer = otkCooldown;
                OnOTKDeactivated?.Invoke();
            }
        }

        // Energy recovery (braking/coasting)
        if (!otkActive && carController != null)
        {
            float harvestRate = 0f;

            switch (ersMode)
            {
                case ERSMode.Harvest:
                    harvestRate = harvestPowerKW;
                    break;
                case ERSMode.Balanced:
                    harvestRate = harvestPowerKW * 0.5f;
                    break;
                case ERSMode.Deploy:
                    harvestRate = harvestPowerKW * 0.15f;
                    break;
            }

            // Harvest more during braking
            if (carController.BrakeInput > 0.1f)
            {
                currentBatteryEnergy += harvestRate * carController.BrakeInput * dt;
            }
            // Harvest slightly during coasting
            else if (carController.ThrottleInput < 0.05f && carController.SpeedKph > 30f)
            {
                currentBatteryEnergy += harvestRate * 0.3f * dt;
            }
        }

        currentBatteryEnergy = Mathf.Clamp(currentBatteryEnergy, 0f, maxBatteryEnergy);
    }

    #endregion

    #region ──── ENGINE MODE / SET ────

    private void UpdateEngineMode()
    {
        if (setButtonPressed)
        {
            // Cycle through engine modes
            engineMode = engineMode switch
            {
                EngineMode.Conserve => EngineMode.Normal,
                EngineMode.Normal => EngineMode.Aggressive,
                EngineMode.Aggressive => EngineMode.Overtake,
                EngineMode.Overtake => EngineMode.Conserve,
                _ => EngineMode.Normal,
            };

            // Update ERS mode to match
            ersMode = engineMode switch
            {
                EngineMode.Conserve => ERSMode.Harvest,
                EngineMode.Normal => ERSMode.Balanced,
                EngineMode.Aggressive => ERSMode.Deploy,
                EngineMode.Overtake => ERSMode.Deploy,
                _ => ERSMode.Balanced,
            };

            OnEngineModeChanged?.Invoke(engineMode);
        }
    }

    /// <summary>Get the fuel rate multiplier for the current engine mode.</summary>
    public float GetFuelRateMultiplier()
    {
        return engineMode switch
        {
            EngineMode.Conserve => conserveFuelRate,
            EngineMode.Normal => normalFuelRate,
            EngineMode.Aggressive => aggressiveFuelRate,
            EngineMode.Overtake => overtakeFuelRate,
            _ => normalFuelRate,
        };
    }

    /// <summary>Get display name for current engine mode (Indonesian).</summary>
    public string GetEngineModeDisplayName()
    {
        return engineMode switch
        {
            EngineMode.Conserve => "HEMAT",
            EngineMode.Normal => "NORMAL",
            EngineMode.Aggressive => "AGRESIF",
            EngineMode.Overtake => "SERANG",
            _ => "NORMAL",
        };
    }

    #endregion
}
