using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Clean, high-performance racing HUD designed for both Android touch devices and Windows Editor.
/// Displays digital speedometer (km/h), sequential gear indicator, dynamic multi-color RPM bar,
/// on-screen touch pedals/steering, and an instant Track Reset button.
/// </summary>
public class RacingHUD : MonoBehaviour
{
    [Header("Vehicle References (Auto-resolved if null)")]
    [SerializeField] private FormulaCarArcadeController carController;
    [SerializeField] private V12EngineAudio engineAudio;

    [Header("Telemetry UI Elements")]
    [SerializeField] private Text speedText;
    [SerializeField] private Text gearText;
    [SerializeField] private Text rpmText;
    [SerializeField] private Image rpmBarFill;
    [SerializeField] private Text surfaceAlertText;

    [Header("Touch HUD Buttons")]
    [SerializeField] private MobileTouchButton steerLeftButton;
    [SerializeField] private MobileTouchButton steerRightButton;
    [SerializeField] private MobileTouchButton accelerateButton;
    [SerializeField] private MobileTouchButton brakeButton;
    [SerializeField] private Button resetButton;

    [Header("Colors")]
    [SerializeField] private Color rpmGreen = new Color(0.1f, 0.85f, 0.35f, 1f);
    [SerializeField] private Color rpmYellow = new Color(1.0f, 0.85f, 0.1f, 1f);
    [SerializeField] private Color rpmRed = new Color(1.0f, 0.15f, 0.2f, 1f);

    private void Awake()
    {
        ResolveReferences();
        if (resetButton != null)
        {
            resetButton.onClick.AddListener(OnResetClicked);
        }
    }

    private void OnValidate()
    {
        ResolveReferences();
    }

    private void ResolveReferences()
    {
        if (carController == null) carController = Object.FindAnyObjectByType<FormulaCarArcadeController>();
        if (engineAudio == null) engineAudio = Object.FindAnyObjectByType<V12EngineAudio>();
    }

    public void OnResetClicked()
    {
        if (carController != null)
        {
            carController.RespawnOnTrack();
        }
    }

    private void Update()
    {
        if (carController == null)
        {
            ResolveReferences();
            if (carController == null) return;
        }

        // Process mobile touch inputs
        float steer = 0f;
        if (steerLeftButton != null && steerLeftButton.IsPressed) steer -= 1f;
        if (steerRightButton != null && steerRightButton.IsPressed) steer += 1f;

        float throttle = (accelerateButton != null && accelerateButton.IsPressed) ? 1f : 0f;
        float brake = (brakeButton != null && brakeButton.IsPressed) ? 1f : 0f;

        carController.SetMobileInput(steer, throttle, brake, false);

        // Update Speed Display
        int speed = Mathf.RoundToInt(carController.SpeedKph);
        if (speedText != null)
        {
            speedText.text = $"{speed} <size=24>KM/H</size>";
        }

        // Update Gear Display
        if (gearText != null)
        {
            if (carController.IsReversing)
            {
                gearText.text = "R";
                gearText.color = rpmRed;
            }
            else
            {
                int gear = engineAudio != null ? engineAudio.CurrentGear : 1;
                gearText.text = (speed < 1 && carController.ThrottleInput < 0.05f) ? "N" : $"{gear}";
                gearText.color = Color.white;
            }
        }

        // Update RPM Display & Fill Bar
        float rpm = engineAudio != null ? engineAudio.CurrentRpm : 1400f;
        float rpm01 = engineAudio != null ? engineAudio.Rpm01 : 0.1f;

        if (rpmText != null)
        {
            rpmText.text = $"{Mathf.RoundToInt(rpm):N0} RPM";
        }

        if (rpmBarFill != null)
        {
            rpmBarFill.fillAmount = rpm01;
            if (rpm01 < 0.65f)
            {
                rpmBarFill.color = rpmGreen;
            }
            else if (rpm01 < 0.85f)
            {
                rpmBarFill.color = Color.Lerp(rpmGreen, rpmYellow, (rpm01 - 0.65f) / 0.20f);
            }
            else
            {
                rpmBarFill.color = Color.Lerp(rpmYellow, rpmRed, (rpm01 - 0.85f) / 0.15f);
            }
        }

        // Off-track Grass Warning indicator
        if (surfaceAlertText != null)
        {
            surfaceAlertText.gameObject.SetActive(carController.IsOnGrass);
        }
    }
}
