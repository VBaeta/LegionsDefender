using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

public enum GamePhase { Building, Combat }

public class GameManager : MonoBehaviourPunCallbacks
{
    public static GameManager Instance { get; private set; }

    [Header("Phase Settings")]
    [SerializeField] private float buildingPhaseDuration = 60f;
    private GamePhase _currentPhase = GamePhase.Building;
    private float _phaseTimer = 0f;

    [Header("Resource Settings")]
    public int startingGold = 1000;
    public int startingLumber = 100;
    public int lumberjackGoldCost = 150;
    public int efficiencyGoldCost = 200;

    [Header("Lumberjack Production")]
    [SerializeField] private float lumberProductionInterval = 10f;
    private float _lumberjackTimer = 0f;

    [Header("Spawn Points")]
    [SerializeField] private Transform[] battlefieldSpawnPoints;
    [SerializeField] private BoxCollider[] battlefieldBounds; // Colliders marking placement bounds

    [Header("Player Prefab")]
    [SerializeField] private string playerPrefabName = "Prefabs/PlayerCharacter";



    // Castle Upgrade States
    private float _castleMaxHealth = 1000f;
    private float _castleCurrentHealth = 1000f;
    private float _castleDamage = 20f;
    private float _castleRegen = 2f;

    public GamePhase CurrentPhase => _currentPhase;
    public float PhaseTimer => _phaseTimer;
    public float CastleMaxHealth => _castleMaxHealth;
    public float CastleCurrentHealth => _castleCurrentHealth;
    public float CastleDamage => _castleDamage;
    public float CastleRegen => _castleRegen;
    public float CastleHealthPercentage => _castleCurrentHealth / _castleMaxHealth;

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
        if (PhotonNetwork.IsConnectedAndReady)
        {
            SpawnLocalPlayer();
        }

        // Initialize timers
        _phaseTimer = buildingPhaseDuration;
        _lumberjackTimer = lumberProductionInterval;

    }

    private void Update()
    {
        // Only master client updates timers and syncs them
        if (PhotonNetwork.IsMasterClient)
        {
            UpdatePhaseTimer();
            UpdateLumberjackProduction();
        }
    }

    private void SpawnLocalPlayer()
    {
        // Find player join index (0 to 3)
        List<Player> players = new List<Player>(PhotonNetwork.PlayerList);
        players.Sort((p1, p2) => p1.ActorNumber.CompareTo(p2.ActorNumber));
        int index = players.IndexOf(PhotonNetwork.LocalPlayer);

        if (index >= 0 && index < battlefieldSpawnPoints.Length)
        {
            Transform spawn = battlefieldSpawnPoints[index];
            PhotonNetwork.Instantiate(playerPrefabName, spawn.position, spawn.rotation);
        }
        else
        {
            // Fallback spawn
            PhotonNetwork.Instantiate(playerPrefabName, Vector3.zero, Quaternion.identity);
        }
    }

    public Vector3 GetSpawnPosition(Player player)
    {
        List<Player> players = new List<Player>(PhotonNetwork.PlayerList);
        players.Sort((p1, p2) => p1.ActorNumber.CompareTo(p2.ActorNumber));
        int index = players.IndexOf(player);

        if (index >= 0 && index < battlefieldSpawnPoints.Length)
        {
            return battlefieldSpawnPoints[index].position;
        }
        return Vector3.zero;
    }

    public bool IsInsidePlayerBattlefield(Player player, Vector3 point)
    {
        List<Player> players = new List<Player>(PhotonNetwork.PlayerList);
        players.Sort((p1, p2) => p1.ActorNumber.CompareTo(p2.ActorNumber));
        int index = players.IndexOf(player);

        if (index >= 0 && index < battlefieldBounds.Length && battlefieldBounds[index] != null)
        {
            return battlefieldBounds[index].bounds.Contains(point);
        }

        // Fallback: If no boundaries are defined, allow placement anywhere
        return true;
    }

    private void UpdatePhaseTimer()
    {
        if (_currentPhase != GamePhase.Building) return;

        _phaseTimer -= Time.deltaTime;
        if (_phaseTimer <= 0)
        {
            photonView.RPC("RPC_SetPhase", RpcTarget.All, (int)GamePhase.Combat);
        }
    }

    public void EndCombatPhase()
    {
        if (PhotonNetwork.IsMasterClient && _currentPhase == GamePhase.Combat)
        {
            photonView.RPC("RPC_SetPhase", RpcTarget.All, (int)GamePhase.Building);
        }
    }

    [PunRPC]
    private void RPC_SetPhase(int phase)
    {
        _currentPhase = (GamePhase)phase;
        if (_currentPhase == GamePhase.Building)
        {
            _phaseTimer = buildingPhaseDuration;
            Debug.Log("Game Phase Switched to: Building");

            // Reset all friendly troops' stats and positions when a wave ends
            UnitController[] units = Object.FindObjectsByType<UnitController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var unit in units)
            {
                if (unit != null && !unit.isEnemy)
                {
                    unit.ResetToSpawnPoint();
                }
            }
        }
        else
        {
            _phaseTimer = 0f;
            Debug.Log("Game Phase Switched to: Combat");
            if (PhotonNetwork.IsMasterClient && WaveManager.Instance != null)
            {
                WaveManager.Instance.StartNextWave();
            }
        }
    }

    private void UpdateLumberjackProduction()
    {
        _lumberjackTimer -= Time.deltaTime;
        if (_lumberjackTimer <= 0)
        {
            _lumberjackTimer = lumberProductionInterval;
            photonView.RPC("RPC_ProduceLumber", RpcTarget.All);
        }
    }

    [PunRPC]
    private void RPC_ProduceLumber()
    {
        if (ResourceManager.LocalInstance != null)
        {
            int count = ResourceManager.LocalInstance.LumberjackCount;
            float eff = ResourceManager.LocalInstance.LumberjackEfficiency;
            int amount = Mathf.RoundToInt(count * 5 * eff);
            ResourceManager.LocalInstance.AddLumber(amount);
            Debug.Log($"Lumberjack Production: Received +{amount} Lumber!");
        }
    }

    public void SpawnTroopNetwork(string troopName, Vector3 position, Quaternion rotation)
    {
        // Instantiates the generic troop prefab, passing the troopName as custom initialization data
        object[] initData = new object[] { troopName };
        PhotonNetwork.Instantiate("Prefabs/Troops/GenericTroop", position, rotation, 0, initData);
    }

    public void RequestCastleUpgrade(Player player, int statIndex)
    {
        photonView.RPC("RPC_UpgradeCastle", RpcTarget.All, statIndex);
    }

    [PunRPC]
    private void RPC_UpgradeCastle(int statIndex)
    {
        switch (statIndex)
        {
            case 0: // Health
                _castleMaxHealth += 500f;
                _castleCurrentHealth += 500f;
                Debug.Log($"Castle Max Health Upgraded to: {_castleMaxHealth}");
                break;
            case 1: // Damage
                _castleDamage += 10f;
                Debug.Log($"Castle Damage Upgraded to: {_castleDamage}");
                break;
            case 2: // Regen
                _castleRegen += 1f;
                Debug.Log($"Castle Regen Upgraded to: {_castleRegen}");
                break;
        }
    }

    public void DamageCastle(float amount)
    {
        if (PhotonNetwork.IsMasterClient)
        {
            photonView.RPC("RPC_DamageCastle", RpcTarget.All, amount);
        }
    }

    [PunRPC]
    private void RPC_DamageCastle(float amount)
    {
        _castleCurrentHealth = Mathf.Max(_castleCurrentHealth - amount, 0f);
        Debug.Log($"Castle attacked! Current Health: {_castleCurrentHealth}/{_castleMaxHealth}");
        if (_castleCurrentHealth <= 0)
        {
            EndGame(false);
        }
    }

    private void EndGame(bool victory)
    {
        Debug.Log(victory ? "Victory!" : "Defeat!");
        PhotonNetwork.LeaveRoom();
    }
}
