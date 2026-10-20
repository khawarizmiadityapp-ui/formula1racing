using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns Formula AI opponents on the Baku starting grid at race start.
/// Configures custom liveries and registers cars with the Race Manager.
/// </summary>
public class FormulaAIGridSpawner : MonoBehaviour
{
    [System.Serializable]
    public struct AIDriverConfig
    {
        public string name;
        public Color primaryColor;
        public Color accentColor;
        public float difficulty; // 0.88 to 1.05
    }

    [Header("Opponent Roster")]
    [SerializeField] private AIDriverConfig[] opponents = new AIDriverConfig[]
    {
        new AIDriverConfig { name = "SAI", primaryColor = new Color(0.85f, 0.08f, 0.08f), accentColor = Color.yellow, difficulty = 0.98f }, // Red / Scuderia
        new AIDriverConfig { name = "VER", primaryColor = new Color(0.08f, 0.15f, 0.35f), accentColor = Color.red, difficulty = 1.02f },    // Navy / Bull
        new AIDriverConfig { name = "NOR", primaryColor = new Color(0.98f, 0.52f, 0.05f), accentColor = Color.cyan, difficulty = 0.96f },   // Papaya Orange
        new AIDriverConfig { name = "LEC", primaryColor = new Color(0.75f, 0.05f, 0.05f), accentColor = Color.white, difficulty = 0.97f },  // Crimson
        new AIDriverConfig { name = "ALO", primaryColor = new Color(0.02f, 0.45f, 0.35f), accentColor = Color.green, difficulty = 0.99f }   // British Racing Green
    };

    [Header("Car Prefab")]
    [SerializeField] private GameObject carPrefab;

    private readonly List<FormulaAICarController> spawnedOpponents = new List<FormulaAICarController>();
    public IReadOnlyList<FormulaAICarController> SpawnedOpponents => spawnedOpponents;

    private void Start()
    {
        SpawnGridOpponents();
    }

    public void SpawnGridOpponents()
    {
        var wpMgr = BakuWaypointManager.Instance;
        if (wpMgr == null) wpMgr = Object.FindAnyObjectByType<BakuWaypointManager>();
        if (wpMgr == null) return;

        if (carPrefab == null)
        {
            carPrefab = Resources.Load<GameObject>("PlayerCar_Mercedes_F1");
            if (carPrefab == null)
            {
                var player = GameObject.Find("PlayerCar");
                if (player != null) carPrefab = player;
            }
        }

        if (carPrefab == null) return;

        // Clean any old AI
        foreach (var ai in spawnedOpponents)
        {
            if (ai != null) Destroy(ai.gameObject);
        }
        spawnedOpponents.Clear();

        // Spawn each AI at grid slots (Slot 1 to 5, behind player at Slot 0)
        for (int i = 0; i < opponents.Length; i++)
        {
            int slotIdx = i + 1; // Slot 1, 2, 3, 4, 5
            Vector3 spawnPos = wpMgr.GetGridSlotPosition(slotIdx);
            Quaternion spawnRot = wpMgr.GetGridSlotRotation();

            GameObject aiObj = Instantiate(carPrefab, spawnPos, spawnRot);
            aiObj.name = $"AI_Opponent_{opponents[i].name}";
            aiObj.tag = "Untagged"; // Ensure not tagged Player

            // Remove player components if present on instance
            var playerCtrl = aiObj.GetComponent<FormulaCarArcadeController>();
            if (playerCtrl != null) DestroyImmediate(playerCtrl);

            var audio = aiObj.GetComponent<V12EngineAudio>();
            if (audio != null) audio.enabled = false; // Silence secondary engines for performance

            // Add and configure AI controller
            var aiCtrl = aiObj.AddComponent<FormulaAICarController>();
            float laneOffset = (i % 2 == 0) ? -1.8f : 1.8f;
            aiCtrl.Setup(opponents[i].name, opponents[i].primaryColor, opponents[i].difficulty, laneOffset);

            // Apply unique livery paint
            ApplyAILivery(aiObj, opponents[i].primaryColor, opponents[i].accentColor);

            spawnedOpponents.Add(aiCtrl);
        }

        Debug.Log($"<color=#00D2BE>[APEX V12] Spawned {spawnedOpponents.Count} AI racing opponents on the starting grid!</color>");
    }

    private static void ApplyAILivery(GameObject carGo, Color mainColor, Color accentColor)
    {
        Transform chassis = carGo.transform.Find("Chassis");
        if (chassis != null)
        {
            Transform amgBlack = chassis.Find("AMGBlack");
            if (amgBlack != null)
            {
                Renderer r = amgBlack.GetComponent<Renderer>();
                if (r != null)
                {
                    Material m = new Material(r.sharedMaterial);
                    m.SetColor("_BaseColor", mainColor);
                    r.material = m;
                }
            }

            Transform teal = chassis.Find("PetronasTeal");
            if (teal != null)
            {
                Renderer r = teal.GetComponent<Renderer>();
                if (r != null)
                {
                    Material m = new Material(r.sharedMaterial);
                    m.SetColor("_BaseColor", accentColor);
                    r.material = m;
                }
            }
        }
    }
}
