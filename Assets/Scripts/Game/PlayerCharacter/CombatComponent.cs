using UnityEngine;
using Photon.Pun;
using System.Collections.Generic;

public class CombatComponent : MonoBehaviourPun
{
    public static CombatComponent LocalInstance { get; private set; }

    public string characterClass = "Warrior";

    [Header("Combat Stats")]
    public float maxHealth = 200f;
    public float currentHealth = 200f;
    public float healthRegen = 1.5f; // HP per second

    [Header("Mana Stats")]
    public float maxMana = 100f;
    public float currentMana = 100f;
    public float manaRegen = 2f; // MP per second

    [Header("Attack Stats")]
    public float baseDamage = 20f;
    public float attackRange = 2f;
    public float attackCooldown = 1.5f;
    public bool isMelee = true;

    [Header("Database & Prefabs")]
    [SerializeField] private CharacterDatabase characterDatabase;
    [SerializeField] private UnitData[] buildableTroops;

    [Header("Visual Blueprint")]
    [SerializeField] private GameObject blueprintPrefab;

    private float _nextAttackTime = 0f;
    private bool _isPlacingTroop = false;
    private UnitData _selectedTroopData;
    private GameObject _spawnedBlueprint;
    private Camera _cam;
    private int _placementStartFrame = -1;

    // Cooldown trackers
    private float _qCooldownTimer = 0f;
    private float _eCooldownTimer = 0f;
    private float _rCooldownTimer = 0f;

    // Regen timer
    private float _regenTimer = 0f;

    // ResourceManager reference
    private ResourceManager _resources;

    public float QCooldownTimer => _qCooldownTimer;
    public float ECooldownTimer => _eCooldownTimer;
    public float RCooldownTimer => _rCooldownTimer;

    private List<AbilityData> _characterAbilities = new List<AbilityData>();
    public List<AbilityData> CharacterAbilities => _characterAbilities;

    public AbilityData GetAbilityData(AbilityInputKey key)
    {
        if (_characterAbilities == null) return null;
        return _characterAbilities.Find(a => a != null && a.inputKey == key);
    }

    public float GetRemainingCooldown(AbilityInputKey key)
    {
        switch (key)
        {
            case AbilityInputKey.Q: return _qCooldownTimer;
            case AbilityInputKey.E: return _eCooldownTimer;
            case AbilityInputKey.R: return _rCooldownTimer;
            default: return 0f;
        }
    }

    public float GetMaxCooldown(AbilityInputKey key)
    {
        AbilityData ability = GetAbilityData(key);
        return ability != null ? ability.cooldown : 1f;
    }

    private void Awake()
    {
        _cam = Camera.main;
        currentHealth = maxHealth;
        currentMana = maxMana;
        _resources = GetComponent<ResourceManager>();

        if (photonView.IsMine)
        {
            LocalInstance = this;
        }
    }

    private void Start()
    {
        // Dynamically load database if null
        if (characterDatabase == null)
        {
            characterDatabase = Resources.Load<CharacterDatabase>("CharacterDatabase");
        }

        if (characterDatabase != null && !string.IsNullOrEmpty(characterClass))
        {
            CharacterData data = characterDatabase.GetByName(characterClass);
            if (data != null && data.abilities != null)
            {
                _characterAbilities = data.abilities;
            }
        }

        if (!photonView.IsMine) return;

        // Retrieve character name from custom property
        if (PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("SelectedCharacter", out object charNameObj))
        {
            characterClass = (string)charNameObj;
            InitializeClassStats();

            // Re-read abilities if class changed
            if (characterDatabase != null)
            {
                CharacterData data = characterDatabase.GetByName(characterClass);
                if (data != null && data.abilities != null)
                {
                    _characterAbilities = data.abilities;
                }
            }
        }

        // Load buildable troops dynamically based on Selected Character's Kingdom
        if (buildableTroops == null || buildableTroops.Length == 0)
        {
            if (characterDatabase != null)
            {
                CharacterData data = characterDatabase.GetByName(characterClass);
                if (data != null)
                {
                    string folderPath = $"Characters/{data.kingdom}/Troops";
                    var loaded = Resources.LoadAll<UnitData>(folderPath);
                    buildableTroops = loaded;
                    Debug.Log($"Loaded {buildableTroops?.Length ?? 0} buildable troops for Kingdom: {data.kingdom} from path: Resources/{folderPath}");
                }
            }

            // Fallback if kingdom loading failed or returned nothing
            if (buildableTroops == null || buildableTroops.Length == 0)
            {
                var loaded = Resources.LoadAll<UnitData>("Characters");
                buildableTroops = loaded;
            }
        }
    }

    private void InitializeClassStats()
    {
        switch (characterClass)
        {
            case "Warrior":
                maxHealth = 250f;
                healthRegen = 3.0f;
                maxMana = 100f;
                manaRegen = 2.0f;
                baseDamage = 25f;
                attackRange = 2f;
                attackCooldown = 1.2f;
                isMelee = true;
                break;
            case "Mage":
                maxHealth = 150f;
                healthRegen = 1.0f;
                maxMana = 200f;
                manaRegen = 8.0f;
                baseDamage = 35f;
                attackRange = 12f;
                attackCooldown = 1.8f;
                isMelee = false;
                break;
            case "Archer":
                maxHealth = 180f;
                healthRegen = 1.5f;
                maxMana = 120f;
                manaRegen = 4.0f;
                baseDamage = 20f;
                attackRange = 15f;
                attackCooldown = 1.0f;
                isMelee = false;
                break;
            case "Tank":
                maxHealth = 350f;
                healthRegen = 6.0f;
                maxMana = 80f;
                manaRegen = 1.5f;
                baseDamage = 15f;
                attackRange = 2f;
                attackCooldown = 1.5f;
                isMelee = true;
                break;
        }
        currentHealth = maxHealth;
        currentMana = maxMana;
    }

    private void Update()
    {
        if (!photonView.IsMine) return;

        // Update ability cooldowns
        if (_qCooldownTimer > 0) _qCooldownTimer -= Time.deltaTime;
        if (_eCooldownTimer > 0) _eCooldownTimer -= Time.deltaTime;
        if (_rCooldownTimer > 0) _rCooldownTimer -= Time.deltaTime;

        // Apply health and mana regeneration
        _regenTimer += Time.deltaTime;
        if (_regenTimer >= 1f)
        {
            _regenTimer = 0f;
            RegenerateStats();
        }

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

    private void RegenerateStats()
    {
        currentHealth = Mathf.Min(currentHealth + healthRegen, maxHealth);
        currentMana = Mathf.Min(currentMana + manaRegen, maxMana);
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
        // Reset stats
        currentHealth = maxHealth;
        currentMana = maxMana;
        if (GameManager.Instance != null)
        {
            transform.position = GameManager.Instance.GetSpawnPosition(PhotonNetwork.LocalPlayer);
        }
    }

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
        if (_resources == null) return;

        if (_resources.Gold < troop.goldCost || _resources.Lumber < troop.lumberCost)
        {
            Debug.Log("Not enough resources!");
            return;
        }

        _selectedTroopData = troop;
        _isPlacingTroop = true;
        _placementStartFrame = Time.frameCount;

        if (_spawnedBlueprint != null) Destroy(_spawnedBlueprint);
        
        if (troop.visualPrefab != null)
        {
            _spawnedBlueprint = Instantiate(troop.visualPrefab);
            _spawnedBlueprint.name = "TroopBlueprint";
            _spawnedBlueprint.transform.localScale = Vector3.one * troop.modelScale;

            // Disable all colliders on preview so it doesn't block raycasts or physics
            foreach (var col in _spawnedBlueprint.GetComponentsInChildren<Collider>())
            {
                col.enabled = false;
            }

            // Clone materials so we can tint them without modifying the originals
            foreach (var renderer in _spawnedBlueprint.GetComponentsInChildren<Renderer>())
            {
                if (renderer != null && renderer.sharedMaterials != null)
                {
                    Material[] clonedMats = new Material[renderer.sharedMaterials.Length];
                    for (int i = 0; i < renderer.sharedMaterials.Length; i++)
                    {
                        if (renderer.sharedMaterials[i] != null)
                        {
                            clonedMats[i] = new Material(renderer.sharedMaterials[i]);
                            clonedMats[i].color = new Color(0.5f, 1f, 0.5f, 0.6f); // semi-transparent green
                        }
                    }
                    renderer.materials = clonedMats;
                }
            }
        }
        else if (troop.mesh != null)
        {
            _spawnedBlueprint = new GameObject("TroopBlueprint");
            _spawnedBlueprint.transform.localScale = Vector3.one * troop.modelScale;

            MeshFilter mf = _spawnedBlueprint.AddComponent<MeshFilter>();
            mf.sharedMesh = troop.mesh;

            MeshRenderer mr = _spawnedBlueprint.AddComponent<MeshRenderer>();
            if (troop.materials != null && troop.materials.Length > 0)
            {
                Material[] clonedMats = new Material[troop.materials.Length];
                for (int i = 0; i < troop.materials.Length; i++)
                {
                    if (troop.materials[i] != null)
                    {
                        clonedMats[i] = new Material(troop.materials[i]);
                        clonedMats[i].color = new Color(0.5f, 1f, 0.5f, 0.6f);
                    }
                }
                mr.materials = clonedMats;
            }
        }
        else
        {
            // Fallback capsule placeholder
            _spawnedBlueprint = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Destroy(_spawnedBlueprint.GetComponent<Collider>());
            var renderer = _spawnedBlueprint.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.material.color = new Color(0, 1, 0, 0.4f);
            }
        }
    }

    private void UpdateBlueprintPosition()
    {
        if (_spawnedBlueprint == null) return;

        // Lazy re-cache camera if it wasn't available during Awake
        if (_cam == null)
        {
            _cam = Camera.main;
            if (_cam == null) return;
        }

        // Right-click cancels placement
        if (Input.GetMouseButtonDown(1))
        {
            CancelPlacement();
            return;
        }

        // Define default fallback position in front of character
        Vector3 forward = transform.forward;
        forward.y = 0;
        forward.Normalize();
        Vector3 targetPos = transform.position + forward * 3f;

        // Perform line trace from character chest/eye level in direction of camera
        Vector3 rayOrigin = transform.position + Vector3.up * 1.5f;
        Vector3 rayDirection = _cam.transform.forward;
        Ray ray = new Ray(rayOrigin, rayDirection);
        int ignoreMask = ~LayerMask.GetMask("Player");

        if (Physics.Raycast(ray, out RaycastHit hit, 100f, ignoreMask))
        {
            // Only accept hits that aren't on ourselves
            if (hit.collider.transform.root != transform.root)
            {
                targetPos = hit.point;
            }
        }

        // Snap target position to the ground
        Ray groundRay = new Ray(targetPos + Vector3.up * 5f, Vector3.down);
        if (Physics.Raycast(groundRay, out RaycastHit groundHit, 20f, ignoreMask))
        {
            targetPos = groundHit.point;
        }

        _spawnedBlueprint.SetActive(true);
        _spawnedBlueprint.transform.position = targetPos;
        _spawnedBlueprint.transform.rotation = Quaternion.LookRotation(forward);

        // Check if within battlefield bounds
        bool isValid = true;
        if (GameManager.Instance != null)
        {
            isValid = GameManager.Instance.IsInsidePlayerBattlefield(PhotonNetwork.LocalPlayer, targetPos);
        }

        // Color-code materials on the preview blueprint (green for valid, red for invalid)
        foreach (var renderer in _spawnedBlueprint.GetComponentsInChildren<Renderer>())
        {
            if (renderer != null)
            {
                foreach (var mat in renderer.materials)
                {
                    mat.color = isValid ? new Color(0.5f, 1f, 0.5f, 0.6f) : new Color(1f, 0.5f, 0.5f, 0.6f);
                }
            }
        }

        // Click LMB to place (ignore click on the same frame that placement started)
        if (Input.GetMouseButtonDown(0) && isValid && Time.frameCount > _placementStartFrame)
        {
            PlaceTroop(targetPos);
        }
    }

    private void PlaceTroop(Vector3 position)
    {
        if (_resources == null || _selectedTroopData == null) return;

        Quaternion rotation = Quaternion.identity;
        if (_spawnedBlueprint != null)
        {
            rotation = _spawnedBlueprint.transform.rotation;
        }

        if (_resources.Gold >= _selectedTroopData.goldCost && _resources.Lumber >= _selectedTroopData.lumberCost)
        {
            _resources.SpendGold(_selectedTroopData.goldCost);
            _resources.SpendLumber(_selectedTroopData.lumberCost);

            // Instantiation via Photon Network so all clients see it
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SpawnTroopNetwork(_selectedTroopData.unitName, position, rotation);
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
        if (_resources == null) return;

        int cost = hireNew 
            ? (GameManager.Instance != null ? GameManager.Instance.lumberjackGoldCost : 150)
            : (GameManager.Instance != null ? GameManager.Instance.efficiencyGoldCost : 200);

        if (_resources.SpendGold(cost))
        {
            if (hireNew)
            {
                _resources.AddLumberjack();
            }
            else
            {
                _resources.UpgradeEfficiency(1);
            }
        }
        else
        {
            Debug.Log("Not enough gold to hire or upgrade lumberjacks!");
        }
    }

    private void UpgradeCastle(int statIndex)
    {
        if (_resources == null) return;

        int cost = 0;
        switch (statIndex)
        {
            case 0: cost = 50; break;
            case 1: cost = 75; break;
            case 2: cost = 60; break;
        }

        if (_resources.SpendLumber(cost))
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.RequestCastleUpgrade(PhotonNetwork.LocalPlayer, statIndex);
            }
        }
        else
        {
            Debug.Log("Not enough lumber to upgrade Castle!");
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
            var controller = hit.GetComponent<UnitController>();
            if (controller != null && controller.isEnemy)
            {
                controller.TakeDamage(baseDamage, DamageType.Normal, gameObject);
                break; // Attack single target
            }
        }
    }

    private void TriggerAbilityQ()
    {
        AbilityData ability = GetAbilityData(AbilityInputKey.Q);
        if (ability == null) return;

        if (currentMana < ability.manaCost)
        {
            Debug.Log($"Not enough mana for {ability.abilityName}!");
            return;
        }
        currentMana -= ability.manaCost;
        _qCooldownTimer = ability.cooldown;
        Debug.Log($"{characterClass} used {ability.abilityName}!");
    }

    private void TriggerAbilityE()
    {
        AbilityData ability = GetAbilityData(AbilityInputKey.E);
        if (ability == null) return;

        if (currentMana < ability.manaCost)
        {
            Debug.Log($"Not enough mana for {ability.abilityName}!");
            return;
        }
        currentMana -= ability.manaCost;
        _eCooldownTimer = ability.cooldown;
        Debug.Log($"{characterClass} used {ability.abilityName}!");
    }

    private void TriggerAbilityR()
    {
        AbilityData ability = GetAbilityData(AbilityInputKey.R);
        if (ability == null) return;

        if (currentMana < ability.manaCost)
        {
            Debug.Log($"Not enough mana for {ability.abilityName}!");
            return;
        }
        currentMana -= ability.manaCost;
        _rCooldownTimer = ability.cooldown;
        Debug.Log($"{characterClass} used {ability.abilityName}!");
    }

    #endregion
}
