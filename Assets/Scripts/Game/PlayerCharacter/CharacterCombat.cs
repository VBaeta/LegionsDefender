using UnityEngine;
using Photon.Pun;
using System.Collections.Generic;

public class CharacterCombat : MonoBehaviourPun
{
    public string characterClass = "Warrior";

    [Header("Combat Stats")]
    public float maxHealth = 200f;
    public float currentHealth = 200f;
    public float baseDamage = 20f;
    public float attackRange = 2f;
    public float attackCooldown = 1.5f;

    [Header("Resource Reference")]
    public int gold = 1000;
    public int lumber = 100;

    [Header("Database & Prefabs")]
    [SerializeField] private CharacterDatabase characterDatabase;
    [SerializeField] private UnitData[] buildableTroops; // Set via Inspector or loaded dynamically

    [Header("Visual Blueprint")]
    [SerializeField] private GameObject blueprintPrefab; // Standard capsule/cube placeholder

    private float _nextAttackTime = 0f;
    private bool _isPlacingTroop = false;
    private UnitData _selectedTroopData;
    private GameObject _spawnedBlueprint;
    private Camera _cam;

    // Cooldown trackers
    private float _qCooldownTimer = 0f;
    private float _eCooldownTimer = 0f;
    private float _rCooldownTimer = 0f;

    // Passives/Abilities configuration
    private float _passiveTickTimer = 0f;

    private void Awake()
    {
        _cam = Camera.main;
        currentHealth = maxHealth;
    }

    private void Start()
    {
        if (!photonView.IsMine) return;

        // Retrieve character name from custom property
        if (PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("SelectedCharacter", out object charNameObj))
        {
            characterClass = (string)charNameObj;
            InitializeClassStats();
        }

        // Dynamically load database if null
        if (characterDatabase == null)
        {
            characterDatabase = Resources.Load<CharacterDatabase>("CharacterDatabase");
        }

        // Load buildable troops dynamically from Resources/Characters
        if (buildableTroops == null || buildableTroops.Length == 0)
        {
            var loaded = Resources.LoadAll<UnitData>("Characters");
            buildableTroops = loaded;
        }

        // Set starting resources from GameManager if present
        if (GameManager.Instance != null)
        {
            gold = GameManager.Instance.startingGold;
            lumber = GameManager.Instance.startingLumber;
        }
    }

    private void InitializeClassStats()
    {
        switch (characterClass)
        {
            case "Warrior":
                maxHealth = 250f;
                baseDamage = 25f;
                attackRange = 2f;
                attackCooldown = 1.2f;
                break;
            case "Mage":
                maxHealth = 150f;
                baseDamage = 35f;
                attackRange = 12f;
                attackCooldown = 1.8f;
                break;
            case "Archer":
                maxHealth = 180f;
                baseDamage = 20f;
                attackRange = 15f;
                attackCooldown = 1.0f;
                break;
            case "Tank":
                maxHealth = 350f;
                baseDamage = 15f;
                attackRange = 2f;
                attackCooldown = 1.5f;
                break;
        }
        currentHealth = maxHealth;
    }

    private void Update()
    {
        if (!photonView.IsMine) return;

        // Update ability cooldowns
        if (_qCooldownTimer > 0) _qCooldownTimer -= Time.deltaTime;
        if (_eCooldownTimer > 0) _eCooldownTimer -= Time.deltaTime;
        if (_rCooldownTimer > 0) _rCooldownTimer -= Time.deltaTime;

        // Process class passive skill
        UpdatePassive();

        bool isBuildingPhase = GameManager.Instance != null && GameManager.Instance.CurrentPhase == GamePhase.Building;

        if (isBuildingPhase)
        {
            HandleBuildingInputs();
        }
        else
        {
            HandleCombatInputs();
        }
    }

    #region Passives & Abilities

    private void UpdatePassive()
    {
        _passiveTickTimer += Time.deltaTime;
        if (_passiveTickTimer >= 1f)
        {
            _passiveTickTimer = 0f;
            ApplyPassiveEffect();
        }
    }

    private void ApplyPassiveEffect()
    {
        switch (characterClass)
        {
            case "Warrior":
                // Passive: Health regen (3 HP per second)
                Heal(3f);
                break;
            case "Mage":
                // Passive: Ability cooldown reduction boost or simple health helper
                Heal(1f);
                break;
            case "Archer":
                // Passive: Movespeed boost is handled in ThirdPersonMovement, but let's give minor regen
                Heal(1.5f);
                break;
            case "Tank":
                // Passive: High health regen (6 HP per second)
                Heal(6f);
                break;
        }
    }

    public void Heal(float amount)
    {
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
    }

    public void TakeDamage(float amount)
    {
        currentHealth = Mathf.Max(currentHealth - amount, 0f);
        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    private void Die()
    {
        Debug.Log($"{characterClass} died!");
        // Placeholder respawn
        currentHealth = maxHealth;
        if (GameManager.Instance != null)
        {
            transform.position = GameManager.Instance.GetSpawnPosition(PhotonNetwork.LocalPlayer);
        }
    }

    #endregion

    #region Building Mode Input & Radial Menu

    private void HandleBuildingInputs()
    {
        // Cancel troop placement with Right Click
        if (_isPlacingTroop && Input.GetMouseButtonDown(1))
        {
            CancelPlacement();
        }

        // If in placement mode, update blueprint position
        if (_isPlacingTroop && _spawnedBlueprint != null)
        {
            UpdateBlueprintPosition();
        }

        // Radial Menu checks
        if (RadialMenu.Instance != null && !RadialMenu.Instance.IsOpen)
        {
            if (Input.GetKeyDown(KeyCode.Q))
            {
                OpenTroopMenu();
            }
            else if (Input.GetKeyDown(KeyCode.E))
            {
                OpenLumberjackMenu();
            }
            else if (Input.GetKeyDown(KeyCode.R))
            {
                OpenCastleMenu();
            }
        }
    }

    private void OpenTroopMenu()
    {
        List<RadialOption> options = new List<RadialOption>();
        foreach (var troop in buildableTroops)
        {
            if (troop == null) continue;
            options.Add(new RadialOption
            {
                name = troop.unitName,
                description = troop.description,
                icon = troop.icon,
                goldCost = troop.goldCost,
                lumberCost = troop.lumberCost,
                callback = () => StartTroopPlacement(troop)
            });
        }
        RadialMenu.Instance.Open(options);
    }

    private void OpenLumberjackMenu()
    {
        List<RadialOption> options = new List<RadialOption>();
        options.Add(new RadialOption
        {
            name = "Add Lumberjack",
            description = "Hire another lumberjack to harvest lumber periodically.",
            goldCost = GameManager.Instance != null ? GameManager.Instance.lumberjackGoldCost : 150,
            callback = () => UpgradeLumberjacks(true)
        });
        options.Add(new RadialOption
        {
            name = "Upgrade Efficiency",
            description = "Increase lumberjack harvest amount.",
            goldCost = GameManager.Instance != null ? GameManager.Instance.efficiencyGoldCost : 200,
            callback = () => UpgradeLumberjacks(false)
        });
        RadialMenu.Instance.Open(options);
    }

    private void OpenCastleMenu()
    {
        List<RadialOption> options = new List<RadialOption>();
        options.Add(new RadialOption
        {
            name = "Upgrade Castle Health",
            description = "Increase maximum health of your Castle.",
            lumberCost = 50,
            callback = () => UpgradeCastle(0)
        });
        options.Add(new RadialOption
        {
            name = "Upgrade Castle Damage",
            description = "Increase automated attack damage of the Castle.",
            lumberCost = 75,
            callback = () => UpgradeCastle(1)
        });
        options.Add(new RadialOption
        {
            name = "Upgrade Castle Regen",
            description = "Boost health regeneration of the Castle.",
            lumberCost = 60,
            callback = () => UpgradeCastle(2)
        });
        RadialMenu.Instance.Open(options);
    }

    private void StartTroopPlacement(UnitData troop)
    {
        if (gold < troop.goldCost || lumber < troop.lumberCost)
        {
            Debug.Log("Not enough resources!");
            return;
        }

        _selectedTroopData = troop;
        _isPlacingTroop = true;

        if (_spawnedBlueprint != null) Destroy(_spawnedBlueprint);
        
        // Spawn blueprint visual
        if (blueprintPrefab != null)
        {
            _spawnedBlueprint = Instantiate(blueprintPrefab);
        }
        else
        {
            // Fallback placeholder
            _spawnedBlueprint = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Destroy(_spawnedBlueprint.GetComponent<Collider>());
            var renderer = _spawnedBlueprint.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial.color = new Color(0, 1, 0, 0.4f); // semi-transparent green
            }
        }
    }

    private void UpdateBlueprintPosition()
    {
        Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f))
        {
            _spawnedBlueprint.transform.position = hit.point;

            // Check if within battlefield bounds
            bool isValid = true;
            if (GameManager.Instance != null)
            {
                isValid = GameManager.Instance.IsInsidePlayerBattlefield(PhotonNetwork.LocalPlayer, hit.point);
            }

            var renderer = _spawnedBlueprint.GetComponentInChildren<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial.color = isValid ? new Color(0, 1, 0, 0.4f) : new Color(1, 0, 0, 0.4f);
            }

            // Click LMB to place
            if (Input.GetMouseButtonDown(0) && isValid)
            {
                PlaceTroop(hit.point);
            }
        }
    }

    private void PlaceTroop(Vector3 position)
    {
        if (gold >= _selectedTroopData.goldCost && lumber >= _selectedTroopData.lumberCost)
        {
            gold -= _selectedTroopData.goldCost;
            lumber -= _selectedTroopData.lumberCost;

            // Instantiation via Photon Network so all clients see it
            // Troop prefab name must reside under "Assets/Resources/" or dynamically spawned via GameManager RPC
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SpawnTroopNetwork(_selectedTroopData.unitName, position);
            }
        }

        CancelPlacement();
    }

    private void CancelPlacement()
    {
        _isPlacingTroop = false;
        _selectedTroopData = null;
        if (_spawnedBlueprint != null)
        {
            Destroy(_spawnedBlueprint);
            _spawnedBlueprint = null;
        }
    }

    private void UpgradeLumberjacks(bool hireNew)
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.RequestLumberjackUpgrade(PhotonNetwork.LocalPlayer, hireNew);
        }
    }

    private void UpgradeCastle(int statIndex)
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.RequestCastleUpgrade(PhotonNetwork.LocalPlayer, statIndex);
        }
    }

    #endregion

    #region Combat Mode Input & Attacks

    private void HandleCombatInputs()
    {
        // Basic Attack (LMB)
        if (Input.GetMouseButtonDown(0) && Time.time >= _nextAttackTime)
        {
            PerformBasicAttack();
        }

        // Q Skill
        if (Input.GetKeyDown(KeyCode.Q) && _qCooldownTimer <= 0)
        {
            TriggerAbilityQ();
        }

        // E Skill
        if (Input.GetKeyDown(KeyCode.E) && _eCooldownTimer <= 0)
        {
            TriggerAbilityE();
        }

        // R Skill
        if (Input.GetKeyDown(KeyCode.R) && _rCooldownTimer <= 0)
        {
            TriggerAbilityR();
        }
    }

    private void PerformBasicAttack()
    {
        _nextAttackTime = Time.time + attackCooldown;
        Debug.Log($"{characterClass} performed a basic attack!");

        // SphereCast/Raycast target logic
        Collider[] hits = Physics.OverlapSphere(transform.position + transform.forward * (attackRange / 2f), attackRange / 2f);
        foreach (var hit in hits)
        {
            // Make sure not targeting self/ally
            var controller = hit.GetComponent<UnitController>();
            if (controller != null && controller.isEnemy)
            {
                controller.TakeDamage(baseDamage, DamageType.Normal);
                break; // Attack single target
            }
        }
    }

    private void TriggerAbilityQ()
    {
        _qCooldownTimer = 5f; // 5s CD placeholder
        Debug.Log($"{characterClass} used Ability Q!");
        // Placeholders for customization later
    }

    private void TriggerAbilityE()
    {
        _eCooldownTimer = 10f; // 10s CD placeholder
        Debug.Log($"{characterClass} used Ability E!");
    }

    private void TriggerAbilityR()
    {
        _rCooldownTimer = 20f; // 20s CD placeholder
        Debug.Log($"{characterClass} used Ability R!");
    }

    #endregion
}
