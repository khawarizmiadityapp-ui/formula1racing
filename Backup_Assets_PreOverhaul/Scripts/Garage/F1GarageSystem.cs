using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 3D Garage & Team Selection menu system.
/// Features:
/// - Orbiting camera around the car with touch/mouse drag
/// - Team info panel (Tim, Mesin, 2 Pengemudi)
/// - Dynamic livery texture swapping
/// - Smooth car rotation & zoom
/// - Navigation between teams with arrows
///
/// Attach to: Garage scene manager GameObject.
/// </summary>
public class F1GarageSystem : MonoBehaviour
{
    #region ──── DATA STRUCTURES ────

    [System.Serializable]
    public class DriverInfo
    {
        public string name;
        public string nationality;       // "FIN", "ITA", etc.
        public Sprite nationalityFlag;
        public int number;
    }

    [System.Serializable]
    public class TeamData
    {
        public string teamName;          // "Mauser"
        public string engineName;        // "CarrariH18"
        public Sprite teamLogo;
        public Sprite nationalityFlag;
        public Color primaryColor;
        public Color secondaryColor;
        public DriverInfo driver1;
        public DriverInfo driver2;
        public GameObject carPrefab;     // 3D car model prefab
        public Texture2D[] liveryTextures; // Available livery skins
        public Material bodyMaterial;     // Material to swap texture on
    }

    #endregion

    #region ──── INSPECTOR FIELDS ────

    [Header("═══ TEAMS DATA ═══")]
    [SerializeField] private TeamData[] teams;

    [Header("═══ CAMERA ORBIT ═══")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private Transform carSpawnPoint;
    [SerializeField] private float orbitDistance = 5.5f;
    [SerializeField] private float orbitHeight = 1.8f;
    [SerializeField] private float orbitSpeed = 80f;
    [SerializeField] private float autoRotateSpeed = 8f;
    [SerializeField] private float zoomMin = 3.5f;
    [SerializeField] private float zoomMax = 8f;
    [SerializeField] private float zoomSpeed = 2f;
    [SerializeField] private float cameraSmoothSpeed = 8f;

    [Header("═══ UI REFERENCES ═══")]
    [SerializeField] private Text teamNameText;
    [SerializeField] private Text engineNameText;
    [SerializeField] private Text driver1NameText;
    [SerializeField] private Text driver2NameText;
    [SerializeField] private Image driver1FlagImage;
    [SerializeField] private Image driver2FlagImage;
    [SerializeField] private Image teamLogoImage;
    [SerializeField] private Image teamFlagImage;
    [SerializeField] private Button prevTeamButton;
    [SerializeField] private Button nextTeamButton;
    [SerializeField] private Button changeLiveryButton;
    [SerializeField] private Button selectButton;
    [SerializeField] private Text liveryButtonText;
    [SerializeField] private Text groundEffectLabel;

    [Header("═══ TRANSITION ═══")]
    [SerializeField] private float carSwapDuration = 0.5f;
    [SerializeField] private CanvasGroup infoPanel;

    #endregion

    #region ──── RUNTIME ────

    private int currentTeamIndex;
    private int currentLiveryIndex;
    private GameObject currentCarInstance;
    private float orbitAngle;
    private float currentZoom;
    private float targetZoom;
    private bool isDragging;
    private float dragStartX;
    private float dragVelocity;
    private float idleTimer;
    private bool isTransitioning;

    #endregion

    #region ──── PUBLIC PROPERTIES ────

    public TeamData CurrentTeam => teams != null && currentTeamIndex < teams.Length
        ? teams[currentTeamIndex] : null;
    public int CurrentTeamIndex => currentTeamIndex;
    public int CurrentLiveryIndex => currentLiveryIndex;
    public int TotalTeams => teams != null ? teams.Length : 0;

    #endregion

    #region ──── EVENTS ────

    public event System.Action<TeamData> OnTeamSelected;
    public event System.Action<int> OnTeamChanged;
    public event System.Action<int> OnLiveryChanged;

    #endregion

    #region ──── LIFECYCLE ────

    private void Awake()
    {
        currentZoom = orbitDistance;
        targetZoom = orbitDistance;

        if (prevTeamButton != null) prevTeamButton.onClick.AddListener(PreviousTeam);
        if (nextTeamButton != null) nextTeamButton.onClick.AddListener(NextTeam);
        if (changeLiveryButton != null) changeLiveryButton.onClick.AddListener(CycleLivery);
        if (selectButton != null) selectButton.onClick.AddListener(SelectTeam);
    }

    private void Start()
    {
        if (teams != null && teams.Length > 0)
        {
            LoadTeam(0, true);
        }
    }

    private void Update()
    {
        HandleInput();
        UpdateCameraOrbit();
    }

    #endregion

    #region ──── INPUT HANDLING ────

    private void HandleInput()
    {
        // Mouse / Touch drag for orbit
        if (Input.GetMouseButtonDown(0))
        {
            isDragging = true;
            dragStartX = Input.mousePosition.x;
            idleTimer = 0f;
        }
        if (Input.GetMouseButtonUp(0))
        {
            isDragging = false;
        }
        if (isDragging)
        {
            float delta = (Input.mousePosition.x - dragStartX) * 0.15f;
            dragStartX = Input.mousePosition.x;
            dragVelocity = delta;
            orbitAngle += delta;
            idleTimer = 0f;
        }
        else
        {
            // Momentum decay
            orbitAngle += dragVelocity;
            dragVelocity = Mathf.Lerp(dragVelocity, 0f, Time.deltaTime * 5f);
        }

        // Mouse scroll zoom
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.01f)
        {
            targetZoom -= scroll * zoomSpeed;
            targetZoom = Mathf.Clamp(targetZoom, zoomMin, zoomMax);
            idleTimer = 0f;
        }

        // Pinch zoom (mobile)
        if (Input.touchCount == 2)
        {
            Touch t0 = Input.GetTouch(0);
            Touch t1 = Input.GetTouch(1);
            float prevDist = (t0.position - t0.deltaPosition - t1.position + t1.deltaPosition).magnitude;
            float currDist = (t0.position - t1.position).magnitude;
            float diff = prevDist - currDist;
            targetZoom += diff * 0.01f;
            targetZoom = Mathf.Clamp(targetZoom, zoomMin, zoomMax);
            idleTimer = 0f;
        }

        // Auto rotate when idle
        idleTimer += Time.deltaTime;
        if (idleTimer > 3f && !isDragging)
        {
            orbitAngle += autoRotateSpeed * Time.deltaTime;
        }

        // Keyboard navigation
        if (Input.GetKeyDown(KeyCode.LeftArrow)) PreviousTeam();
        if (Input.GetKeyDown(KeyCode.RightArrow)) NextTeam();
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) SelectTeam();
        if (Input.GetKeyDown(KeyCode.L)) CycleLivery();
    }

    #endregion

    #region ──── CAMERA ORBIT ────

    private void UpdateCameraOrbit()
    {
        if (cameraTransform == null || carSpawnPoint == null) return;

        currentZoom = Mathf.Lerp(currentZoom, targetZoom, Time.deltaTime * cameraSmoothSpeed);

        float rad = orbitAngle * Mathf.Deg2Rad;
        Vector3 targetPos = carSpawnPoint.position + new Vector3(
            Mathf.Sin(rad) * currentZoom,
            orbitHeight,
            Mathf.Cos(rad) * currentZoom
        );

        cameraTransform.position = Vector3.Lerp(
            cameraTransform.position,
            targetPos,
            Time.deltaTime * cameraSmoothSpeed
        );

        // Always look at car center
        Vector3 lookAt = carSpawnPoint.position + Vector3.up * 0.6f;
        Quaternion targetRot = Quaternion.LookRotation(lookAt - cameraTransform.position, Vector3.up);
        cameraTransform.rotation = Quaternion.Slerp(
            cameraTransform.rotation,
            targetRot,
            Time.deltaTime * cameraSmoothSpeed * 1.5f
        );
    }

    #endregion

    #region ──── TEAM NAVIGATION ────

    public void NextTeam()
    {
        if (teams == null || teams.Length == 0 || isTransitioning) return;
        int next = (currentTeamIndex + 1) % teams.Length;
        LoadTeam(next, false);
    }

    public void PreviousTeam()
    {
        if (teams == null || teams.Length == 0 || isTransitioning) return;
        int prev = (currentTeamIndex - 1 + teams.Length) % teams.Length;
        LoadTeam(prev, false);
    }

    private void LoadTeam(int index, bool instant)
    {
        if (teams == null || index < 0 || index >= teams.Length) return;

        currentTeamIndex = index;
        currentLiveryIndex = 0;
        var team = teams[index];

        // Spawn car
        SpawnCar(team, instant);

        // Update UI
        UpdateTeamInfoUI(team);

        OnTeamChanged?.Invoke(index);
    }

    #endregion

    #region ──── CAR SPAWNING ────

    private void SpawnCar(TeamData team, bool instant)
    {
        if (isTransitioning && !instant) return;

        if (instant)
        {
            DoSpawnCar(team);
        }
        else
        {
            StartCoroutine(TransitionCar(team));
        }
    }

    private System.Collections.IEnumerator TransitionCar(TeamData team)
    {
        isTransitioning = true;

        // Fade out info panel
        if (infoPanel != null)
        {
            float t = 0f;
            while (t < carSwapDuration * 0.5f)
            {
                t += Time.deltaTime;
                infoPanel.alpha = 1f - (t / (carSwapDuration * 0.5f));
                yield return null;
            }
        }

        // Swap car
        DoSpawnCar(team);

        // Fade in info panel
        if (infoPanel != null)
        {
            float t = 0f;
            while (t < carSwapDuration * 0.5f)
            {
                t += Time.deltaTime;
                infoPanel.alpha = t / (carSwapDuration * 0.5f);
                yield return null;
            }
            infoPanel.alpha = 1f;
        }

        isTransitioning = false;
    }

    private void DoSpawnCar(TeamData team)
    {
        // Destroy old
        if (currentCarInstance != null)
        {
            Destroy(currentCarInstance);
        }

        // Instantiate new
        if (team.carPrefab != null && carSpawnPoint != null)
        {
            currentCarInstance = Instantiate(team.carPrefab, carSpawnPoint.position,
                carSpawnPoint.rotation, carSpawnPoint);
            currentCarInstance.name = $"GarageCar_{team.teamName}";

            // Disable physics components for garage display
            DisablePhysicsComponents(currentCarInstance);

            // Apply initial livery
            ApplyLivery(team, 0);
        }
    }

    private void DisablePhysicsComponents(GameObject car)
    {
        // Disable rigidbody
        var rb = car.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;

        // Disable colliders
        var colliders = car.GetComponentsInChildren<Collider>();
        foreach (var col in colliders) col.enabled = false;

        // Disable car controller if present
        var controller = car.GetComponent<F1CarController>();
        if (controller != null) controller.enabled = false;
    }

    #endregion

    #region ──── LIVERY SYSTEM ────

    public void CycleLivery()
    {
        var team = CurrentTeam;
        if (team == null || team.liveryTextures == null || team.liveryTextures.Length == 0) return;

        currentLiveryIndex = (currentLiveryIndex + 1) % team.liveryTextures.Length;
        ApplyLivery(team, currentLiveryIndex);

        OnLiveryChanged?.Invoke(currentLiveryIndex);
    }

    public void SetLivery(int index)
    {
        var team = CurrentTeam;
        if (team == null || team.liveryTextures == null) return;
        if (index < 0 || index >= team.liveryTextures.Length) return;

        currentLiveryIndex = index;
        ApplyLivery(team, index);
        OnLiveryChanged?.Invoke(index);
    }

    private void ApplyLivery(TeamData team, int liveryIndex)
    {
        if (team.liveryTextures == null || liveryIndex >= team.liveryTextures.Length) return;
        if (currentCarInstance == null) return;

        Texture2D newTexture = team.liveryTextures[liveryIndex];
        if (newTexture == null) return;

        // Apply to body material
        if (team.bodyMaterial != null)
        {
            // Create instance to avoid modifying shared material
            Material matInstance = new Material(team.bodyMaterial);
            if (matInstance.HasProperty("_BaseMap"))
                matInstance.SetTexture("_BaseMap", newTexture);
            else if (matInstance.HasProperty("_MainTex"))
                matInstance.SetTexture("_MainTex", newTexture);

            // Find renderers on the car and apply
            Renderer[] renderers = currentCarInstance.GetComponentsInChildren<Renderer>();
            foreach (var rend in renderers)
            {
                Material[] mats = rend.materials;
                for (int m = 0; m < mats.Length; m++)
                {
                    if (mats[m].name.Contains(team.bodyMaterial.name))
                    {
                        mats[m] = matInstance;
                    }
                }
                rend.materials = mats;
            }
        }
        else
        {
            // Fallback: apply to first renderer's main material
            Renderer rend = currentCarInstance.GetComponentInChildren<Renderer>();
            if (rend != null)
            {
                Material mat = rend.material; // Creates instance
                if (mat.HasProperty("_BaseMap"))
                    mat.SetTexture("_BaseMap", newTexture);
                else if (mat.HasProperty("_MainTex"))
                    mat.SetTexture("_MainTex", newTexture);
            }
        }

        if (liveryButtonText != null)
        {
            liveryButtonText.text = $"LIVERY {liveryIndex + 1}/{team.liveryTextures.Length}";
        }
    }

    #endregion

    #region ──── UI UPDATE ────

    private void UpdateTeamInfoUI(TeamData team)
    {
        if (teamNameText != null) teamNameText.text = team.teamName;
        if (engineNameText != null) engineNameText.text = team.engineName;

        if (team.driver1 != null)
        {
            if (driver1NameText != null) driver1NameText.text = team.driver1.name;
            if (driver1FlagImage != null && team.driver1.nationalityFlag != null)
                driver1FlagImage.sprite = team.driver1.nationalityFlag;
        }

        if (team.driver2 != null)
        {
            if (driver2NameText != null) driver2NameText.text = team.driver2.name;
            if (driver2FlagImage != null && team.driver2.nationalityFlag != null)
                driver2FlagImage.sprite = team.driver2.nationalityFlag;
        }

        if (teamLogoImage != null && team.teamLogo != null)
            teamLogoImage.sprite = team.teamLogo;

        if (teamFlagImage != null && team.nationalityFlag != null)
            teamFlagImage.sprite = team.nationalityFlag;

        if (liveryButtonText != null)
        {
            int totalLiveries = team.liveryTextures != null ? team.liveryTextures.Length : 0;
            liveryButtonText.text = totalLiveries > 0 ? $"LIVERY 1/{totalLiveries}" : "UBAH LIVERY";
        }
    }

    #endregion

    #region ──── SELECTION ────

    public void SelectTeam()
    {
        if (CurrentTeam != null)
        {
            OnTeamSelected?.Invoke(CurrentTeam);
        }
    }

    #endregion
}
