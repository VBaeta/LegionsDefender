using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

[System.Serializable]
public class WaveEntry
{
    public UnitData enemyData;
    public int count;
}

[System.Serializable]
public class WaveConfig
{
    public List<WaveEntry> entries;
}

public class WaveManager : MonoBehaviourPun
{
    public static WaveManager Instance { get; private set; }

    [Header("Wave Configuration")]
    [SerializeField] private List<WaveConfig> waves;
    [SerializeField] private float timeBetweenSpawns = 1.0f;
    [SerializeField] private float spawnRadius = 3.0f; // Spread radius for spawn offset

    [Header("Battlefields Setup")]
    [SerializeField] private Transform[] spawnPortals;     // Spawner portals (one per player slot)
    [SerializeField] private Transform[][] waypointsPerPath; // Array of waypoint arrays (one per path)

    private int _currentWaveIndex = -1;
    private int _activeEnemiesCount = 0;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // Setup default waypoints if they weren't assigned
        if (waypointsPerPath == null || waypointsPerPath.Length == 0)
        {
            SetupDefaultWaypoints();
        }
    }

    private void SetupDefaultWaypoints()
    {
        // Simple fallback paths: from portal straight to a central castle position (0,0,0)
        waypointsPerPath = new Transform[spawnPortals.Length][];
        for (int i = 0; i < spawnPortals.Length; i++)
        {
            waypointsPerPath[i] = new Transform[1];
            
            GameObject castleGoal = GameObject.Find("CastleGoal");
            if (castleGoal != null)
            {
                waypointsPerPath[i][0] = castleGoal.transform;
            }
            else
            {
                GameObject temp = new GameObject($"DefaultGoal_{i}");
                temp.transform.position = Vector3.zero;
                waypointsPerPath[i][0] = temp.transform;
            }
        }
    }

    public void StartNextWave()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        _currentWaveIndex++;
        if (_currentWaveIndex < waves.Count)
        {
            StartCoroutine(SpawnWaveRoutine(waves[_currentWaveIndex]));
        }
        else
        {
            Debug.Log("All waves completed!");
            // Switch phase to Building or end game victory
        }
    }

    private IEnumerator SpawnWaveRoutine(WaveConfig wave)
    {
        Debug.Log($"Spawning Wave {_currentWaveIndex + 1}");
        _activeEnemiesCount = 0;

        // Calculate total enemies
        int totalToSpawn = 0;
        foreach (var entry in wave.entries)
        {
            totalToSpawn += entry.count * spawnPortals.Length;
        }

        // Spawn logic
        foreach (var entry in wave.entries)
        {
            for (int count = 0; count < entry.count; count++)
            {
                // Spawn one enemy for each active battlefield path
                for (int pathIndex = 0; pathIndex < spawnPortals.Length; pathIndex++)
                {
                    if (spawnPortals[pathIndex] == null) continue;

                    Transform portal = spawnPortals[pathIndex];
                    
                    // Spread out spawn position using a random offset within a circle
                    Vector2 randOffset = Random.insideUnitCircle * spawnRadius;
                    Vector3 spawnPos = portal.position + new Vector3(randOffset.x, 0f, randOffset.y);

                    // Prefab lookup path
                    string prefabPath = "Prefabs/Enemies/" + entry.enemyData.unitName;
                    
                    GameObject enemyGo = PhotonNetwork.Instantiate(prefabPath, spawnPos, portal.rotation);
                    UnitController controller = enemyGo.GetComponent<UnitController>();
                    
                    if (controller != null)
                    {
                        controller.isEnemy = true;
                        
                        // Extract waypoint positions
                        Vector3[] wps = GetWaypointPositions(pathIndex);
                        controller.SetupPath(wps);
                    }

                    _activeEnemiesCount++;
                }

                yield return new WaitForSeconds(timeBetweenSpawns);
            }
        }
    }

    private Vector3[] GetWaypointPositions(int pathIndex)
    {
        if (waypointsPerPath == null || pathIndex >= waypointsPerPath.Length)
        {
            return new Vector3[0];
        }

        Transform[] transforms = waypointsPerPath[pathIndex];
        Vector3[] positions = new Vector3[transforms.Length];
        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i] != null)
            {
                positions[i] = transforms[i].position;
            }
        }
        return positions;
    }

    // Called by UnitController when an enemy dies or reaches the castle
    public void OnEnemyDestroyed()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        _activeEnemiesCount--;
        if (_activeEnemiesCount <= 0)
        {
            // All enemies killed, switch back to building phase
            if (GameManager.Instance != null)
            {
                GameManager.Instance.RequestCastleUpgrade(PhotonNetwork.LocalPlayer, -1); // Triggers next building turn in GameMaster
            }
        }
    }
}
