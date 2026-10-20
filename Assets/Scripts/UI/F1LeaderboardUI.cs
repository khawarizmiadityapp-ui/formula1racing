using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Leaderboard panel showing race positions with time gaps (selisih waktu).
/// Displays a scrollable list of drivers with position, name abbreviation,
/// and time delta relative to the leader or player.
///
/// Attach to: Canvas or HUD parent object.
/// Dependencies: F1RaceTimingSystem (optional)
/// </summary>
public class F1LeaderboardUI : MonoBehaviour
{
    #region ──── DATA ────

    [System.Serializable]
    public class DriverEntry
    {
        public int position;
        public string abbreviation; // e.g. "HAM", "VER", "SAI"
        public string teamColor;   // Hex color "#00D2BE"
        public float gapToLeader;  // Delta time in seconds
        public bool isPlayer;
    }

    #endregion

    #region ──── INSPECTOR ────

    [Header("═══ LEADERBOARD SETTINGS ═══")]
    [SerializeField] private int visibleEntries = 5;
    [Tooltip("Reference to the content container (vertical layout)")]
    [SerializeField] private Transform entryContainer;
    [Tooltip("Prefab for a single leaderboard row")]
    [SerializeField] private GameObject entryPrefab;

    [Header("═══ COLORS ═══")]
    [SerializeField] private Color playerHighlight = new Color(0f, 0.68f, 0.58f, 0.4f);
    [SerializeField] private Color normalBackground = new Color(0.04f, 0.06f, 0.08f, 0.75f);
    [SerializeField] private Color textColor = Color.white;

    #endregion

    #region ──── RUNTIME ────

    private DriverEntry[] entries;
    private GameObject[] rowObjects;
    private Text[] posTexts;
    private Text[] nameTexts;
    private Text[] gapTexts;
    private Image[] rowBgs;
    private Image[] teamColorBars;

    #endregion

    #region ──── PUBLIC API ────

    /// <summary>
    /// Initialize leaderboard with driver data.
    /// Call this at race start or when positions change.
    /// </summary>
    public void Initialize(DriverEntry[] driverEntries)
    {
        entries = driverEntries;
        BuildRows();
        UpdateDisplay();
    }

    /// <summary>
    /// Update position data (call when positions change).
    /// </summary>
    public void UpdatePositions(DriverEntry[] driverEntries)
    {
        entries = driverEntries;
        UpdateDisplay();
    }

    /// <summary>
    /// Quick update just the gap times without rebuilding.
    /// </summary>
    public void UpdateGaps(float[] gaps)
    {
        if (entries == null || gaps == null) return;
        for (int i = 0; i < Mathf.Min(entries.Length, gaps.Length); i++)
        {
            entries[i].gapToLeader = gaps[i];
        }
        UpdateDisplay();
    }

    #endregion

    #region ──── BUILD UI ────

    private void BuildRows()
    {
        // Clear existing
        if (rowObjects != null)
        {
            for (int i = 0; i < rowObjects.Length; i++)
            {
                if (rowObjects[i] != null) Destroy(rowObjects[i]);
            }
        }

        if (entries == null || entryContainer == null) return;

        int count = Mathf.Min(entries.Length, visibleEntries);
        rowObjects = new GameObject[count];
        posTexts = new Text[count];
        nameTexts = new Text[count];
        gapTexts = new Text[count];
        rowBgs = new Image[count];
        teamColorBars = new Image[count];

        for (int i = 0; i < count; i++)
        {
            GameObject row;
            if (entryPrefab != null)
            {
                row = Instantiate(entryPrefab, entryContainer);
            }
            else
            {
                row = CreateDefaultRow(entryContainer);
            }
            row.name = $"Row_{i}";
            rowObjects[i] = row;

            // Find child references
            posTexts[i] = FindChildText(row, "PosText");
            nameTexts[i] = FindChildText(row, "NameText");
            gapTexts[i] = FindChildText(row, "GapText");
            rowBgs[i] = row.GetComponent<Image>();
            Transform colorBar = row.transform.Find("TeamColor");
            if (colorBar != null) teamColorBars[i] = colorBar.GetComponent<Image>();
        }
    }

    private GameObject CreateDefaultRow(Transform parent)
    {
        // Create a minimal row if no prefab assigned
        GameObject row = new GameObject("LeaderboardRow", typeof(RectTransform));
        row.transform.SetParent(parent, false);
        RectTransform rt = row.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(200f, 28f);

        Image bg = row.AddComponent<Image>();
        bg.color = normalBackground;

        // Team color bar
        GameObject colorBar = new GameObject("TeamColor", typeof(RectTransform));
        colorBar.transform.SetParent(row.transform, false);
        RectTransform colorRT = colorBar.GetComponent<RectTransform>();
        colorRT.anchorMin = new Vector2(0f, 0f);
        colorRT.anchorMax = new Vector2(0f, 1f);
        colorRT.pivot = new Vector2(0f, 0.5f);
        colorRT.anchoredPosition = Vector2.zero;
        colorRT.sizeDelta = new Vector2(4f, 0f);
        colorBar.AddComponent<Image>().color = Color.white;

        // Position text
        CreateTextChild(row.transform, "PosText", new Vector2(20f, 0f), new Vector2(30f, 24f), 14, TextAnchor.MiddleCenter);
        // Name text
        CreateTextChild(row.transform, "NameText", new Vector2(52f, 0f), new Vector2(50f, 24f), 13, TextAnchor.MiddleLeft);
        // Gap text
        CreateTextChild(row.transform, "GapText", new Vector2(120f, 0f), new Vector2(70f, 24f), 12, TextAnchor.MiddleRight);

        return row;
    }

    private void CreateTextChild(Transform parent, string name, Vector2 pos, Vector2 size, int fontSize, TextAnchor align)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        Text txt = go.AddComponent<Text>();
        txt.fontSize = fontSize;
        txt.alignment = align;
        txt.color = textColor;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (txt.font == null) txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    private Text FindChildText(GameObject parent, string childName)
    {
        Transform child = parent.transform.Find(childName);
        if (child != null) return child.GetComponent<Text>();
        return null;
    }

    #endregion

    #region ──── UPDATE DISPLAY ────

    private void UpdateDisplay()
    {
        if (entries == null || rowObjects == null) return;

        int count = Mathf.Min(entries.Length, rowObjects.Length);
        for (int i = 0; i < count; i++)
        {
            var entry = entries[i];
            if (rowObjects[i] == null) continue;

            // Position
            if (posTexts[i] != null)
            {
                posTexts[i].text = entry.position.ToString();
                posTexts[i].fontStyle = entry.isPlayer ? FontStyle.Bold : FontStyle.Normal;
            }

            // Name
            if (nameTexts[i] != null)
            {
                nameTexts[i].text = entry.abbreviation;
                nameTexts[i].fontStyle = entry.isPlayer ? FontStyle.Bold : FontStyle.Normal;
            }

            // Gap
            if (gapTexts[i] != null)
            {
                if (entry.position == 1)
                {
                    gapTexts[i].text = "LEADER";
                    gapTexts[i].color = new Color(0.8f, 0.8f, 0.8f);
                }
                else
                {
                    string prefix = entry.gapToLeader >= 0 ? "+" : "";
                    gapTexts[i].text = $"{prefix}{entry.gapToLeader:F3}";
                    gapTexts[i].color = textColor;
                }
            }

            // Background
            if (rowBgs[i] != null)
            {
                rowBgs[i].color = entry.isPlayer ? playerHighlight : normalBackground;
            }

            // Team color bar
            if (teamColorBars[i] != null && !string.IsNullOrEmpty(entry.teamColor))
            {
                if (ColorUtility.TryParseHtmlString(entry.teamColor, out Color teamCol))
                {
                    teamColorBars[i].color = teamCol;
                }
            }
        }
    }

    #endregion
}
