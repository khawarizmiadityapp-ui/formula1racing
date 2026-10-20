using UnityEngine;

/// <summary>
/// F1 Race Timing & Lap Counter system.
/// Tracks lap count, lap times, sector times (S1, S2, S3),
/// best lap, delta times, and race position.
///
/// Usage: Place sector trigger colliders on the track with tags "Sector1", "Sector2", "FinishLine".
///
/// Attach to: Same GameObject as F1CarController.
/// Dependencies: F1CarController
/// </summary>
public class F1RaceTimingSystem : MonoBehaviour
{
    #region ──── INSPECTOR FIELDS ────

    [Header("═══ RACE SETTINGS ═══")]
    [SerializeField] private int totalLaps = 3;
    [SerializeField] private bool enableDeltaTime = true;

    #endregion

    #region ──── DATA STRUCTURES ────

    [System.Serializable]
    public class LapData
    {
        public float totalTime;
        public float sector1;
        public float sector2;
        public float sector3;
        public bool isComplete;

        public override string ToString()
        {
            return isComplete ? FormatTime(totalTime) : "--:--.---";
        }

        public static string FormatTime(float t)
        {
            if (t <= 0f) return "--:--.---";
            int minutes = (int)(t / 60f);
            float seconds = t - minutes * 60f;
            return $"{minutes}:{seconds:00.000}";
        }
    }

    #endregion

    #region ──── RUNTIME ────

    private int currentLap;
    private int currentSector; // 0=before S1, 1=after S1, 2=after S2
    private float lapTimer;
    private float sectorTimer;
    private float raceTimer;
    private bool raceStarted;
    private bool raceFinished;

    // Sector split times for current lap
    private float currentS1, currentS2, currentS3;

    // History
    private LapData[] lapHistory;
    private LapData bestLap;
    private float bestS1 = float.MaxValue;
    private float bestS2 = float.MaxValue;
    private float bestS3 = float.MaxValue;

    // Delta
    private float deltaTime; // + = slower than best, - = faster

    #endregion

    #region ──── PUBLIC PROPERTIES ────

    /// <summary>Current lap number (1-based)</summary>
    public int CurrentLap => currentLap;
    /// <summary>Total laps in race</summary>
    public int TotalLaps => totalLaps;
    /// <summary>Lap display string "1/3"</summary>
    public string LapDisplay => $"{Mathf.Max(1, currentLap)}/{totalLaps}";
    /// <summary>Current sector (1, 2, or 3)</summary>
    public int CurrentSector => currentSector + 1;
    /// <summary>Total race time elapsed</summary>
    public float RaceTime => raceTimer;
    /// <summary>Current lap time</summary>
    public float CurrentLapTime => lapTimer;
    /// <summary>Is race started</summary>
    public bool IsRaceStarted => raceStarted;
    /// <summary>Is race finished</summary>
    public bool IsRaceFinished => raceFinished;

    /// <summary>Current S1 time (0 if not yet reached)</summary>
    public float CurrentS1 => currentS1;
    /// <summary>Current S2 time (0 if not yet reached)</summary>
    public float CurrentS2 => currentS2;
    /// <summary>Current S3 time (0 if not yet reached)</summary>
    public float CurrentS3 => currentS3;

    /// <summary>Best S1 time</summary>
    public float BestS1 => bestS1 < float.MaxValue ? bestS1 : 0f;
    /// <summary>Best S2 time</summary>
    public float BestS2 => bestS2 < float.MaxValue ? bestS2 : 0f;
    /// <summary>Best S3 time</summary>
    public float BestS3 => bestS3 < float.MaxValue ? bestS3 : 0f;

    /// <summary>Best lap data</summary>
    public LapData BestLapData => bestLap;
    /// <summary>Best lap time</summary>
    public float BestLapTime => bestLap != null && bestLap.isComplete ? bestLap.totalTime : 0f;
    /// <summary>Last completed lap</summary>
    public LapData LastLap => currentLap > 1 && lapHistory != null && lapHistory[currentLap - 2] != null
        ? lapHistory[currentLap - 2] : null;

    /// <summary>Delta to best lap (+ = slower, - = faster)</summary>
    public float DeltaTime => deltaTime;
    /// <summary>Delta display string with +/- prefix</summary>
    public string DeltaDisplay
    {
        get
        {
            if (Mathf.Abs(deltaTime) < 0.001f) return "+0.000";
            string prefix = deltaTime > 0 ? "+" : "";
            return $"{prefix}{deltaTime:F3}";
        }
    }

    /// <summary>All lap history data</summary>
    public LapData[] LapHistory => lapHistory;

    #endregion

    #region ──── EVENTS ────

    public event System.Action<int> OnLapCompleted;    // lapNumber
    public event System.Action<int, float> OnSectorCompleted; // sector, time
    public event System.Action OnRaceFinished;
    public event System.Action<float> OnNewBestLap;    // bestTime

    #endregion

    #region ──── LIFECYCLE ────

    private void Awake()
    {
        lapHistory = new LapData[totalLaps + 1];
        for (int i = 0; i < lapHistory.Length; i++)
        {
            lapHistory[i] = new LapData();
        }
    }

    private void Update()
    {
        if (!raceStarted || raceFinished) return;

        float dt = Time.deltaTime;
        raceTimer += dt;
        lapTimer += dt;
        sectorTimer += dt;

        // Calculate delta to best lap
        if (enableDeltaTime && bestLap != null && bestLap.isComplete)
        {
            float bestLapAtThisPoint = 0f;
            float currentLapAtThisPoint = 0f;

            if (currentSector == 0)
            {
                // In S1: compare current sector timer to best S1 progress
                bestLapAtThisPoint = bestS1 < float.MaxValue ? bestS1 * (sectorTimer / Mathf.Max(0.01f, bestS1)) : 0f;
                currentLapAtThisPoint = sectorTimer;
            }
            else if (currentSector == 1)
            {
                bestLapAtThisPoint = (bestS1 < float.MaxValue ? bestS1 : 0f) +
                    (bestS2 < float.MaxValue ? bestS2 * (sectorTimer / Mathf.Max(0.01f, bestS2)) : 0f);
                currentLapAtThisPoint = currentS1 + sectorTimer;
            }
            else
            {
                bestLapAtThisPoint = (bestS1 < float.MaxValue ? bestS1 : 0f) +
                    (bestS2 < float.MaxValue ? bestS2 : 0f) +
                    (bestS3 < float.MaxValue ? bestS3 * (sectorTimer / Mathf.Max(0.01f, bestS3)) : 0f);
                currentLapAtThisPoint = currentS1 + currentS2 + sectorTimer;
            }

            deltaTime = currentLapAtThisPoint - bestLapAtThisPoint;
        }
    }

    #endregion

    #region ──── TRIGGER DETECTION ────

    private void OnTriggerEnter(Collider other)
    {
        if (!raceStarted && !raceFinished)
        {
            // Race starts on first crossing of finish line
            if (other.CompareTag("FinishLine"))
            {
                StartRace();
            }
            return;
        }

        if (raceFinished) return;

        if (other.CompareTag("Sector1") && currentSector == 0)
        {
            CompleteSector1();
        }
        else if (other.CompareTag("Sector2") && currentSector == 1)
        {
            CompleteSector2();
        }
        else if (other.CompareTag("FinishLine") && currentSector == 2)
        {
            CompleteLap();
        }
    }

    #endregion

    #region ──── RACE CONTROL ────

    public void StartRace()
    {
        raceStarted = true;
        raceFinished = false;
        currentLap = 1;
        currentSector = 0;
        raceTimer = 0f;
        lapTimer = 0f;
        sectorTimer = 0f;
        currentS1 = currentS2 = currentS3 = 0f;
        deltaTime = 0f;
    }

    private void CompleteSector1()
    {
        currentS1 = sectorTimer;
        sectorTimer = 0f;
        currentSector = 1;

        if (currentS1 < bestS1) bestS1 = currentS1;
        OnSectorCompleted?.Invoke(1, currentS1);
    }

    private void CompleteSector2()
    {
        currentS2 = sectorTimer;
        sectorTimer = 0f;
        currentSector = 2;

        if (currentS2 < bestS2) bestS2 = currentS2;
        OnSectorCompleted?.Invoke(2, currentS2);
    }

    private void CompleteLap()
    {
        currentS3 = sectorTimer;
        if (currentS3 < bestS3) bestS3 = currentS3;
        OnSectorCompleted?.Invoke(3, currentS3);

        // Record lap
        int lapIndex = currentLap - 1;
        if (lapIndex >= 0 && lapIndex < lapHistory.Length)
        {
            lapHistory[lapIndex].sector1 = currentS1;
            lapHistory[lapIndex].sector2 = currentS2;
            lapHistory[lapIndex].sector3 = currentS3;
            lapHistory[lapIndex].totalTime = lapTimer;
            lapHistory[lapIndex].isComplete = true;

            // Check best lap
            if (bestLap == null || lapTimer < bestLap.totalTime)
            {
                bestLap = lapHistory[lapIndex];
                OnNewBestLap?.Invoke(lapTimer);
            }
        }

        OnLapCompleted?.Invoke(currentLap);

        // Next lap or finish
        currentLap++;
        if (currentLap > totalLaps)
        {
            raceFinished = true;
            OnRaceFinished?.Invoke();
        }
        else
        {
            lapTimer = 0f;
            sectorTimer = 0f;
            currentSector = 0;
            currentS1 = currentS2 = currentS3 = 0f;
        }
    }

    /// <summary>Format time for display.</summary>
    public static string FormatTime(float t)
    {
        return LapData.FormatTime(t);
    }

    /// <summary>Get color for sector comparison (green = faster, yellow = equal, red = slower).</summary>
    public Color GetSectorColor(int sector, float time)
    {
        float best = sector switch
        {
            1 => bestS1,
            2 => bestS2,
            3 => bestS3,
            _ => float.MaxValue,
        };

        if (best >= float.MaxValue || time <= 0f) return Color.white;
        if (time < best - 0.01f) return new Color(0.5f, 0f, 0.8f); // Purple = all-time best
        if (time < best + 0.05f) return new Color(0.1f, 0.85f, 0.35f); // Green = matching best
        if (time < best + 0.5f) return new Color(1f, 0.85f, 0.1f); // Yellow = close
        return new Color(1f, 0.2f, 0.15f); // Red = slower
    }

    #endregion
}
