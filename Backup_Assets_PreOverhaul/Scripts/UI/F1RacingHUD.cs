using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Complete F1 Racing HUD Manager (Unity Canvas).
/// Manages all UI elements visible during a race:
///
/// ┌─────────────────────────────────────────────────────────────┐
/// │ [LEADERBOARD]     [BATTERY/ENGINE]        [LAP/SECTOR]     │
/// │  kiri atas         tengah atas             kanan atas       │
/// │                                                             │
/// │ [ALA]                                     [MINIMAP]         │
/// │ [OTK]                                      kanan            │
/// │ [SET]                                                       │
/// │                                                             │
/// │ [TIRE STATUS]     [SPEEDOMETER]           [PEDALS]          │
/// │  bawah kiri        bawah tengah           bawah kanan       │
/// └─────────────────────────────────────────────────────────────┘
///
/// Attach to: Canvas GameObject in the scene.
/// Dependencies: F1CarController, F1DRSERSSystem, F1TireModel, F1RaceTimingSystem
/// </summary>
public class F1RacingHUD : MonoBehaviour
{
    #region ──── VEHICLE REFERENCES ────

    [Header("═══ VEHICLE REFERENCES (Auto-resolved) ═══")]
    [SerializeField] private F1CarController carController;
    [SerializeField] private F1DRSERSSystem drsErsSystem;
    [SerializeField] private F1TireModel tireModel;
    [SerializeField] private F1RaceTimingSystem timingSystem;

    #endregion

    #region ──── SPEEDOMETER CLUSTER (Bawah Tengah) ────

    [Header("═══ SPEEDOMETER / TELEMETRY ═══")]
    [SerializeField] private Text speedText;          // "209"
    [SerializeField] private Text speedUnitText;      // "KM/H"
    [SerializeField] private Text gearText;           // "6"
    [SerializeField] private Text rpmText;            // "11259"
    [SerializeField] private Text rpmLabelText;       // "RPM"
    [SerializeField] private Image rpmBarFill;        // Horizontal fill bar
    [SerializeField] private Text transmissionText;   // "AUTO" / "MANUAL"

    [Header("Shift Light LEDs")]
    [SerializeField] private Image[] shiftLEDs;       // Array of LED images (green→yellow→red→blue)
    [SerializeField] private Color ledOff = new Color(0.12f, 0.14f, 0.16f);
    [SerializeField] private Color ledGreen = new Color(0.1f, 0.85f, 0.35f);
    [SerializeField] private Color ledYellow = new Color(1f, 0.85f, 0.1f);
    [SerializeField] private Color ledRed = new Color(1f, 0.15f, 0.2f);
    [SerializeField] private Color ledBlue = new Color(0.2f, 0.5f, 1f);

    #endregion

    #region ──── DRS/ERS STATUS (Tengah Atas & Kiri) ────

    [Header("═══ DRS / ERS STATUS ═══")]
    [SerializeField] private Image batteryBarFill;
    [SerializeField] private Text batteryPercentText;
    [SerializeField] private Image batteryIcon;
    [SerializeField] private Text engineModeText;      // "NORMAL", "HEMAT", etc.
    [SerializeField] private Image engineModeBackground;
    [SerializeField] private Text ersDeployText;       // "OTK AKTIF" or remaining time
    [SerializeField] private Text drsStatusText;       // "ALA" indicator
    [SerializeField] private Image drsIndicator;       // DRS open/closed visual

    #endregion

    #region ──── LEADERBOARD (Kiri Atas) ────

    [Header("═══ LEADERBOARD ═══")]
    [SerializeField] private Transform leaderboardContainer;
    [SerializeField] private Text deltaText;           // "+00.124" / "-00.746"

    #endregion

    #region ──── TIRE STATUS (Bawah Kiri) ────

    [Header("═══ TIRE STATUS ═══")]
    [SerializeField] private Image tireFLIndicator;
    [SerializeField] private Image tireFRIndicator;
    [SerializeField] private Image tireRLIndicator;
    [SerializeField] private Image tireRRIndicator;
    [SerializeField] private Text tireFLTemp;
    [SerializeField] private Text tireFRTemp;
    [SerializeField] private Text tireRLTemp;
    [SerializeField] private Text tireRRTemp;
    [SerializeField] private Text tireCompoundText;    // "SEDANG"
    [SerializeField] private Image tireCompoundDot;    // Compound color dot
    [SerializeField] private Text tireWearText;        // "BAN: 78%"

    #endregion

    #region ──── LAP / SECTOR (Kanan Atas) ────

    [Header("═══ LAP & SECTOR TIMING ═══")]
    [SerializeField] private Text lapCounterText;      // "PUTARAN 1/3"
    [SerializeField] private Text currentLapTimeText;  // "SEKARANG 1:23.456"
    [SerializeField] private Text bestLapTimeText;     // "TERBAIK  1:22.123"
    [SerializeField] private Text fastestLapTimeText;  // "TERCEPAT 1:21.456"
    [SerializeField] private Text sector1Text;         // "S1"
    [SerializeField] private Text sector2Text;         // "S2"
    [SerializeField] private Text sector3Text;         // "S3"
    [SerializeField] private Image sector1Bar;
    [SerializeField] private Image sector2Bar;
    [SerializeField] private Image sector3Bar;
    [SerializeField] private Text deltaDisplayText;    // "+0.124"

    #endregion

    #region ──── MOBILE CONTROLS ────

    [Header("═══ MOBILE TOUCH CONTROLS ═══")]
    [SerializeField] private MobileTouchButton steerLeftButton;
    [SerializeField] private MobileTouchButton steerRightButton;
    [SerializeField] private MobileTouchButton accelerateButton;
    [SerializeField] private MobileTouchButton brakeButton;
    [SerializeField] private MobileTouchButton alaButton;   // DRS
    [SerializeField] private MobileTouchButton otkButton;   // ERS Overtake
    [SerializeField] private Button setButton;               // Engine Mode
    [SerializeField] private Button gearUpButton;            // Manual Shift Up (+)
    [SerializeField] private Button gearDownButton;          // Manual Shift Down (-)
    [SerializeField] private Button resetButton;
    [SerializeField] private Button cameraButton;

    #endregion

    #region ──── MINIMAP ────

    [Header("═══ MINIMAP (Kanan) ═══")]
    [SerializeField] private RawImage minimapDisplay;
    [SerializeField] private Camera minimapCamera;

    #endregion

    #region ──── SURFACE ALERT ────

    [Header("═══ ALERTS ═══")]
    [SerializeField] private Text surfaceAlertText;

    #endregion

    #region ──── COLORS ────

    [Header("═══ COLOR SCHEME ═══")]
    [SerializeField] private Color rpmGreen = new Color(0.1f, 0.85f, 0.35f, 1f);
    [SerializeField] private Color rpmYellow = new Color(1.0f, 0.85f, 0.1f, 1f);
    [SerializeField] private Color rpmRed = new Color(1.0f, 0.15f, 0.2f, 1f);
    [SerializeField] private Color batteryGreen = new Color(0.1f, 0.8f, 0.35f);
    [SerializeField] private Color batteryYellow = new Color(1f, 0.85f, 0.1f);
    [SerializeField] private Color batteryRed = new Color(1f, 0.15f, 0.2f);
    [SerializeField] private Color drsActiveColor = new Color(0.1f, 0.85f, 0.35f);
    [SerializeField] private Color drsInactiveColor = new Color(0.4f, 0.4f, 0.4f);

    #endregion

    #region ──── LIFECYCLE ────

    private void Awake()
    {
        ResolveReferences();
        BindButtons();
    }

    private void OnValidate()
    {
        ResolveReferences();
    }

    private void Update()
    {
        if (carController == null)
        {
            ResolveReferences();
            if (carController == null) return;
        }

        ProcessMobileInput();
        UpdateSpeedometer();
        UpdateShiftLEDs();
        UpdateDRSERSStatus();
        UpdateTireStatus();
        UpdateLapSector();
        UpdateAlerts();
    }

    #endregion

    #region ──── REFERENCE RESOLUTION ────

    private void ResolveReferences()
    {
        if (carController == null) carController = Object.FindAnyObjectByType<F1CarController>();
        if (drsErsSystem == null) drsErsSystem = Object.FindAnyObjectByType<F1DRSERSSystem>();
        if (tireModel == null) tireModel = Object.FindAnyObjectByType<F1TireModel>();
        if (timingSystem == null) timingSystem = Object.FindAnyObjectByType<F1RaceTimingSystem>();
    }

    #endregion

    #region ──── BUTTON BINDINGS ────

    private void BindButtons()
    {
        if (resetButton != null) resetButton.onClick.AddListener(OnResetClicked);
        if (setButton != null) setButton.onClick.AddListener(OnSETClicked);
        if (cameraButton != null) cameraButton.onClick.AddListener(OnCameraClicked);
        if (gearUpButton != null) gearUpButton.onClick.AddListener(() => { if (carController != null) carController.ShiftUp(); });
        if (gearDownButton != null) gearDownButton.onClick.AddListener(() => { if (carController != null) carController.ShiftDown(); });
    }

    public void OnResetClicked()
    {
        if (carController != null) carController.RespawnOnTrack();
    }

    public void OnSETClicked()
    {
        if (drsErsSystem != null) drsErsSystem.SetMobileSET(true);
    }

    public void OnCameraClicked()
    {
        var cam = Object.FindAnyObjectByType<F1CockpitCamera>();
        if (cam != null) cam.CycleMode();
    }

    #endregion

    #region ──── MOBILE INPUT PROCESSING ────

    private void ProcessMobileInput()
    {
        // Steering
        float steer = 0f;
        if (steerLeftButton != null && steerLeftButton.IsPressed) steer -= 1f;
        if (steerRightButton != null && steerRightButton.IsPressed) steer += 1f;

        float throttle = (accelerateButton != null && accelerateButton.IsPressed) ? 1f : 0f;
        float brake = (brakeButton != null && brakeButton.IsPressed) ? 1f : 0f;

        carController.SetMobileInput(steer, throttle, brake, false);

        // DRS/ERS mobile inputs
        if (drsErsSystem != null)
        {
            if (alaButton != null) drsErsSystem.SetMobileALA(alaButton.IsPressed);
            if (otkButton != null && otkButton.IsPressed)
            {
                drsErsSystem.SetMobileOTK(true);
            }
        }
    }

    #endregion

    #region ──── SPEEDOMETER UPDATE ────

    private void UpdateSpeedometer()
    {
        int speed = Mathf.RoundToInt(carController.SpeedKph);
        float rpm = carController.CurrentRPM;
        float rpm01 = carController.RPM01;
        int gear = carController.CurrentGear;

        // Speed display
        if (speedText != null)
        {
            speedText.text = speed.ToString();
        }

        // Gear display
        if (gearText != null)
        {
            if (carController.IsReversing)
            {
                gearText.text = "R";
                gearText.color = rpmRed;
            }
            else
            {
                gearText.text = (speed < 1 && carController.ThrottleInput < 0.05f) ? "N" : gear.ToString();
                gearText.color = Color.white;
            }
        }

        // RPM display
        if (rpmText != null)
        {
            rpmText.text = Mathf.RoundToInt(rpm).ToString("N0");
        }

        // RPM bar
        if (rpmBarFill != null)
        {
            rpmBarFill.fillAmount = rpm01;
            if (rpm01 < 0.60f) rpmBarFill.color = rpmGreen;
            else if (rpm01 < 0.82f) rpmBarFill.color = Color.Lerp(rpmGreen, rpmYellow, (rpm01 - 0.60f) / 0.22f);
            else rpmBarFill.color = Color.Lerp(rpmYellow, rpmRed, (rpm01 - 0.82f) / 0.18f);
        }

        // Transmission mode
        if (transmissionText != null)
        {
            transmissionText.text = carController.CurrentTransmissionMode == F1CarController.TransmissionMode.Automatic
                ? "OTO" : "MANUAL";
        }
    }

    #endregion

    #region ──── SHIFT LIGHT LEDs ────

    private void UpdateShiftLEDs()
    {
        if (shiftLEDs == null || shiftLEDs.Length == 0) return;

        float rpm01 = carController.RPM01;
        int totalLEDs = shiftLEDs.Length;
        int activeLEDs = Mathf.FloorToInt(rpm01 * totalLEDs);

        // Blink all blue when at redline (shift light)
        bool blinkState = carController.ShiftLightActive && (Time.time * 12f) % 1f > 0.5f;

        for (int i = 0; i < totalLEDs; i++)
        {
            if (shiftLEDs[i] == null) continue;

            if (carController.ShiftLightActive)
            {
                shiftLEDs[i].color = blinkState ? ledBlue : ledOff;
            }
            else if (i < activeLEDs)
            {
                float ledProgress = (float)i / totalLEDs;
                if (ledProgress < 0.4f) shiftLEDs[i].color = ledGreen;
                else if (ledProgress < 0.7f) shiftLEDs[i].color = ledYellow;
                else shiftLEDs[i].color = ledRed;
            }
            else
            {
                shiftLEDs[i].color = ledOff;
            }
        }
    }

    #endregion

    #region ──── DRS/ERS STATUS UPDATE ────

    private void UpdateDRSERSStatus()
    {
        if (drsErsSystem == null) return;

        // Battery bar
        if (batteryBarFill != null)
        {
            float percent = drsErsSystem.BatteryPercent;
            batteryBarFill.fillAmount = percent;

            if (percent > 0.5f) batteryBarFill.color = batteryGreen;
            else if (percent > 0.2f) batteryBarFill.color = batteryYellow;
            else batteryBarFill.color = batteryRed;
        }

        if (batteryPercentText != null)
        {
            batteryPercentText.text = $"{Mathf.RoundToInt(drsErsSystem.BatteryPercent * 100)}";
        }

        // Engine mode
        if (engineModeText != null)
        {
            engineModeText.text = drsErsSystem.GetEngineModeDisplayName();
        }

        if (engineModeBackground != null)
        {
            engineModeBackground.color = drsErsSystem.CurrentEngineMode switch
            {
                F1DRSERSSystem.EngineMode.Conserve => new Color(0.2f, 0.5f, 1f, 0.85f),
                F1DRSERSSystem.EngineMode.Normal => new Color(0.1f, 0.8f, 0.35f, 0.85f),
                F1DRSERSSystem.EngineMode.Aggressive => new Color(1f, 0.6f, 0f, 0.85f),
                F1DRSERSSystem.EngineMode.Overtake => new Color(1f, 0.15f, 0.2f, 0.85f),
                _ => new Color(0.3f, 0.3f, 0.3f, 0.85f),
            };
        }

        // ERS OTK status
        if (ersDeployText != null)
        {
            if (drsErsSystem.IsOTKActive)
            {
                ersDeployText.text = $"OTK {drsErsSystem.OTKRemainingTime:F1}s";
                ersDeployText.color = rpmRed;
            }
            else if (drsErsSystem.OTKCooldownRemaining > 0f)
            {
                ersDeployText.text = $"CD {drsErsSystem.OTKCooldownRemaining:F0}s";
                ersDeployText.color = rpmYellow;
            }
            else if (drsErsSystem.CanActivateOTK)
            {
                ersDeployText.text = "OTK SIAP";
                ersDeployText.color = new Color(0.1f, 0.85f, 0.35f);
            }
            else
            {
                ersDeployText.text = "OTK";
                ersDeployText.color = new Color(0.5f, 0.5f, 0.5f);
            }
        }

        // DRS status
        if (drsStatusText != null)
        {
            drsStatusText.text = drsErsSystem.IsDRSOpen ? "ALA BUKA" : "ALA";
            drsStatusText.color = drsErsSystem.IsDRSOpen ? drsActiveColor : Color.white;
        }

        if (drsIndicator != null)
        {
            drsIndicator.color = drsErsSystem.IsDRSOpen ? drsActiveColor : drsInactiveColor;
        }
    }

    #endregion

    #region ──── TIRE STATUS UPDATE ────

    private void UpdateTireStatus()
    {
        if (tireModel == null) return;

        UpdateSingleTire(tireFLIndicator, tireFLTemp, tireModel.FrontLeft);
        UpdateSingleTire(tireFRIndicator, tireFRTemp, tireModel.FrontRight);
        UpdateSingleTire(tireRLIndicator, tireRLTemp, tireModel.RearLeft);
        UpdateSingleTire(tireRRIndicator, tireRRTemp, tireModel.RearRight);

        if (tireCompoundText != null)
        {
            tireCompoundText.text = tireModel.GetCompoundName();
        }

        if (tireCompoundDot != null)
        {
            tireCompoundDot.color = tireModel.GetCompoundColor();
        }

        if (tireWearText != null)
        {
            float avgWear = (tireModel.FrontLeft.wear + tireModel.FrontRight.wear
                + tireModel.RearLeft.wear + tireModel.RearRight.wear) / 4f;
            tireWearText.text = $"BAN {Mathf.RoundToInt(avgWear)}%";
        }
    }

    private void UpdateSingleTire(Image indicator, Text tempText, F1TireModel.TireData tire)
    {
        if (indicator != null)
        {
            indicator.color = tire.GetConditionColor();
        }
        if (tempText != null)
        {
            tempText.text = $"{Mathf.RoundToInt(tire.temperature)}°";
        }
    }

    #endregion

    #region ──── LAP / SECTOR UPDATE ────

    private void UpdateLapSector()
    {
        if (timingSystem == null) return;

        if (lapCounterText != null)
        {
            lapCounterText.text = $"PUTARAN {timingSystem.LapDisplay}";
        }

        if (currentLapTimeText != null)
        {
            currentLapTimeText.text = F1RaceTimingSystem.FormatTime(timingSystem.CurrentLapTime);
        }

        if (bestLapTimeText != null)
        {
            bestLapTimeText.text = timingSystem.BestLapTime > 0f
                ? F1RaceTimingSystem.FormatTime(timingSystem.BestLapTime)
                : "--:--.---";
        }

        // Delta
        if (deltaDisplayText != null)
        {
            deltaDisplayText.text = $"DELTA\n{timingSystem.DeltaDisplay}";
            deltaDisplayText.color = timingSystem.DeltaTime < -0.01f
                ? new Color(0.1f, 0.85f, 0.35f)
                : timingSystem.DeltaTime > 0.01f
                    ? rpmRed
                    : Color.white;
        }

        // Sector bars
        UpdateSectorBar(sector1Text, sector1Bar, 1, timingSystem.CurrentS1, timingSystem.BestS1);
        UpdateSectorBar(sector2Text, sector2Bar, 2, timingSystem.CurrentS2, timingSystem.BestS2);
        UpdateSectorBar(sector3Text, sector3Bar, 3, timingSystem.CurrentS3, timingSystem.BestS3);
    }

    private void UpdateSectorBar(Text label, Image bar, int sectorNum, float time, float best)
    {
        if (bar == null) return;

        bool isCurrentSector = timingSystem != null && timingSystem.CurrentSector == sectorNum;
        bool isCompleted = time > 0f;

        if (isCompleted)
        {
            bar.color = timingSystem.GetSectorColor(sectorNum, time);
            if (label != null) label.text = $"S{sectorNum}\n{F1RaceTimingSystem.FormatTime(time)}";
        }
        else if (isCurrentSector)
        {
            // Pulsing white for active sector
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 4f);
            bar.color = new Color(1f, 1f, 1f, pulse);
            if (label != null) label.text = $"S{sectorNum}";
        }
        else
        {
            bar.color = new Color(0.3f, 0.3f, 0.3f, 0.5f);
            if (label != null) label.text = $"S{sectorNum}";
        }
    }

    #endregion

    #region ──── ALERTS ────

    private void UpdateAlerts()
    {
        if (surfaceAlertText != null)
        {
            surfaceAlertText.gameObject.SetActive(carController.IsOnGrass);
        }
    }

    #endregion
}
