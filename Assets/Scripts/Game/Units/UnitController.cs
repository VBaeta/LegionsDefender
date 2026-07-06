using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using Photon.Pun;

public class UnitController : MonoBehaviourPun, IPunInstantiateMagicCallback
{
    [Header("Configuration Data")]
    [SerializeField] private UnitData unitData;
    public bool isEnemy = false;

    [Header("Movement & Target")]
    [SerializeField] private NavMeshAgent agent;
    
    private Transform _target;
    private float _currentHealth;
    private float _nextAttackTime = 0f;
    private Animator _animator;

    private Vector3 _originalSpawnPosition;
    private Quaternion _originalSpawnRotation;
    private bool _isDead = false;
    private Vector3 _lastPosition;

    // Enemy movement paths
    private Vector3[] _waypoints;
    private int _currentWaypointIndex = 0;

    public float CurrentHealth => _currentHealth;
    public UnitData Data => unitData;

    public void OnPhotonInstantiate(PhotonMessageInfo info)
    {
        // Automatically determine if this is an enemy unit based on prefab name
        if (gameObject.name.Contains("GenericEnemy"))
        {
            isEnemy = true;
        }

        object[] data = info.photonView.InstantiationData;
        if (data != null && data.Length > 0)
        {
            string unitName = (string)data[0];
            
            // Search all UnitData assets in Resources by matching the unitName field
            // This handles nested folder structures (e.g. Characters/Humans/Troops/1_Peasant)
            UnitData loadedData = null;
            UnitData[] allUnits = Resources.LoadAll<UnitData>("");
            foreach (var ud in allUnits)
            {
                if (ud != null && ud.unitName == unitName)
                {
                    loadedData = ud;
                    break;
                }
            }

            if (loadedData != null)
            {
                unitData = loadedData;
                _currentHealth = unitData.maxHealth;
                if (agent == null)
                {
                    agent = GetComponent<NavMeshAgent>();
                }
                if (agent != null)
                {
                    agent.enabled = false;
                    if (NavMesh.SamplePosition(transform.position, out NavMeshHit navHit, 5.0f, NavMesh.AllAreas))
                    {
                        transform.position = navHit.position;
                    }
                    agent.enabled = true;
                    agent.speed = unitData.moveSpeed;
                }

                SpawnVisualModel();
            }
            else
            {
                Debug.LogWarning($"[UnitController] Could not find UnitData with unitName '{unitName}' in Resources!");
            }
        }
    }

    private void SpawnVisualModel()
    {
        // Clear any existing visual model child to prevent overlap
        foreach (Transform child in transform)
        {
            if (child.name == "VisualModel")
            {
                Destroy(child.gameObject);
            }
        }

        if (unitData == null) return;

        if (unitData.visualPrefab != null)
        {
            GameObject visual = Instantiate(unitData.visualPrefab, transform);
            visual.name = "VisualModel";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one * unitData.modelScale;

            // Cache animator
            _animator = visual.GetComponent<Animator>();
            if (_animator == null)
            {
                _animator = visual.GetComponentInChildren<Animator>();
            }

            // If still null (e.g. if instantiating a raw FBX model asset directly), add one dynamically!
            if (_animator == null)
            {
                _animator = visual.AddComponent<Animator>();
            }

            // Assign the Animator Controller to make sure it plays!
            if (_animator != null)
            {
                _animator.enabled = true;
                if (unitData.animatorController != null)
                {
                    _animator.runtimeAnimatorController = unitData.animatorController;
                    _animator.Rebind();
                }
            }
        }
        else if (unitData.mesh != null)
        {
            GameObject visual = new GameObject("VisualModel");
            visual.transform.SetParent(transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one * unitData.modelScale;

            // Build mesh renderer from ScriptableObject data
            MeshFilter mf = visual.AddComponent<MeshFilter>();
            mf.sharedMesh = unitData.mesh;

            MeshRenderer mr = visual.AddComponent<MeshRenderer>();
            if (unitData.materials != null && unitData.materials.Length > 0)
            {
                mr.sharedMaterials = unitData.materials;
            }

            // Attach animator if an AnimatorController is assigned
            if (unitData.animatorController != null)
            {
                Animator anim = visual.AddComponent<Animator>();
                anim.runtimeAnimatorController = unitData.animatorController;
                if (unitData.animatorAvatar != null)
                {
                    anim.avatar = unitData.animatorAvatar;
                }
                _animator = anim;
                _animator.enabled = true;
                _animator.Rebind();
            }
        }
        else
        {
            // Fallback: spawn a capsule primitive as a placeholder visual
            GameObject visual = new GameObject("VisualModel");
            visual.transform.SetParent(transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;

            GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            fallback.transform.SetParent(visual.transform, false);
            fallback.transform.localPosition = Vector3.zero;
            Collider col = fallback.GetComponent<Collider>();
            if (col != null) Destroy(col);
        }
    }

    private void Start()
    {
        // Automatically determine if this is an enemy unit based on prefab name
        if (gameObject.name.Contains("GenericEnemy"))
        {
            isEnemy = true;
        }

        // Cache original spawn position and rotation for end-of-wave resets
        _originalSpawnPosition = transform.position;
        _originalSpawnRotation = transform.rotation;
        _lastPosition = transform.position;

        // If spawned locally or preset in editor, spawn visual model if missing
        if (unitData != null && transform.Find("VisualModel") == null)
        {
            SpawnVisualModel();
        }

        if (unitData == null)
        {
            return;
        }

        _currentHealth = unitData.maxHealth;

        if (agent == null)
        {
            agent = GetComponent<NavMeshAgent>();
        }

        if (agent != null)
        {
            agent.enabled = false;
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit navHit, 5.0f, NavMesh.AllAreas))
            {
                transform.position = navHit.position;
            }
            agent.enabled = true;
            agent.speed = unitData.moveSpeed;
        }

        // Initialize target loop
        InvokeRepeating("FindTarget", 0f, 0.5f);
    }

    private void Update()
    {
        if (_isDead) return;

        // Update locomotion animation for all clients based on actual movement speed
        if (_animator != null)
        {
            float movementThisFrame = Vector3.Distance(transform.position, _lastPosition);
            float actualSpeed = movementThisFrame / Mathf.Max(Time.deltaTime, 0.0001f);
            float maxSpeed = (unitData != null && unitData.moveSpeed > 0) ? unitData.moveSpeed : 3f;
            float normSpeed = Mathf.Clamp01(actualSpeed / maxSpeed);
            _animator.SetFloat("Speed", normSpeed);
        }
        _lastPosition = transform.position;

        // Combat target and movement updates are processed on the owner/master client to prevent race conditions
        if (PhotonNetwork.IsConnectedAndReady && !PhotonNetwork.IsMasterClient) return;

        if (_target != null)
        {
            // Fight target
            float distance = Vector3.Distance(transform.position, _target.position);
            
            if (distance <= unitData.attackRange)
            {
                if (agent != null && agent.enabled && agent.isOnNavMesh) agent.isStopped = true;
                
                if (Time.time >= _nextAttackTime)
                {
                    AttackTarget();
                }
            }
            else
            {
                // Chase target
                if (agent != null && agent.enabled && agent.isOnNavMesh)
                {
                    agent.isStopped = false;
                    agent.SetDestination(_target.position);
                }
                else
                {
                    // Fallback direct movement (e.g. if NavMesh isn't baked)
                    Vector3 dir = (_target.position - transform.position).normalized;
                    dir.y = 0;
                    transform.position = Vector3.MoveTowards(transform.position, _target.position, unitData.moveSpeed * Time.deltaTime);
                    if (dir.sqrMagnitude > 0.001f)
                    {
                        transform.rotation = Quaternion.LookRotation(dir);
                    }
                }
            }
        }
        else
        {
            // No target, proceed to waypoints (Enemies only)
            if (isEnemy)
            {
                FollowPath();
            }
            else
            {
                // Idle troop
                if (agent != null && agent.enabled && agent.isOnNavMesh) agent.isStopped = true;
            }
        }
    }

    public void SetupPath(Vector3[] waypoints)
    {
        _waypoints = waypoints;
        _currentWaypointIndex = 0;
    }

    private void FollowPath()
    {
        if (_waypoints == null || _waypoints.Length == 0) return;

        Vector3 targetDest = _waypoints[_currentWaypointIndex];

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.SetDestination(targetDest);
        }
        else
        {
            // Fallback direct movement (e.g. if NavMesh isn't baked)
            Vector3 dir = (targetDest - transform.position).normalized;
            dir.y = 0;
            transform.position = Vector3.MoveTowards(transform.position, targetDest, unitData.moveSpeed * Time.deltaTime);
            if (dir.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(dir);
            }
        }

        // Check if reached waypoint
        if (Vector3.Distance(transform.position, targetDest) < 2.0f)
        {
            _currentWaypointIndex++;
            if (_currentWaypointIndex >= _waypoints.Length)
            {
                // Reached Castle! Damage castle and destroy unit
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.DamageCastle(unitData.attackDamage);
                }
                Despawn();
            }
        }
    }

    private void FindTarget()
    {
        if (PhotonNetwork.IsConnectedAndReady && !PhotonNetwork.IsMasterClient) return;

        // Reset if target is dead or leaves search/attack range
        if (_target != null)
        {
            float currentDist = Vector3.Distance(transform.position, _target.position);
            // Defocus range: at least 12 meters, or slightly larger than the unit's attack range
            float defocusRange = Mathf.Max(unitData.attackRange + 3f, 12f);

            var targetUnit = _target.GetComponent<UnitController>();
            var targetPlayer = _target.GetComponent<CombatComponent>();
            if ((targetUnit != null && targetUnit.CurrentHealth <= 0) || 
                (targetPlayer != null && targetPlayer.currentHealth <= 0) ||
                currentDist > defocusRange)
            {
                _target = null;
            }
        }

        if (_target != null) return;

        float searchRadius = Mathf.Max(unitData.attackRange + 2f, 10f);
        Collider[] hits = Physics.OverlapSphere(transform.position, searchRadius);
        float minDistance = float.MaxValue;
        Transform closest = null;

        foreach (var hit in hits)
        {
            if (isEnemy)
            {
                // Enemies target PlayerCharacters or Troops
                var troop = hit.GetComponent<UnitController>();
                if (troop != null && !troop.isEnemy && troop.CurrentHealth > 0)
                {
                    float dist = Vector3.Distance(transform.position, hit.transform.position);
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        closest = hit.transform;
                    }
                }

                var player = hit.GetComponent<CombatComponent>();
                if (player != null && player.currentHealth > 0)
                {
                    float dist = Vector3.Distance(transform.position, hit.transform.position);
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        closest = hit.transform;
                    }
                }
            }
            else
            {
                // Troops target Enemies
                var enemy = hit.GetComponent<UnitController>();
                if (enemy != null && enemy.isEnemy && enemy.CurrentHealth > 0)
                {
                    float dist = Vector3.Distance(transform.position, hit.transform.position);
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        closest = hit.transform;
                    }
                }
            }
        }

        if (closest != null)
        {
            _target = closest;
        }
    }

    private void AttackTarget()
    {
        _nextAttackTime = Time.time + (1f / unitData.attackRate);

        if (PhotonNetwork.IsConnectedAndReady)
        {
            photonView.RPC("RPC_PlayAttackAnimation", RpcTarget.All);
        }
        else
        {
            PlayAttackAnimation();
        }

        var targetUnit = _target.GetComponent<UnitController>();
        if (targetUnit != null)
        {
            targetUnit.TakeDamage(unitData.attackDamage, unitData.damageType, gameObject);
        }

        var targetPlayer = _target.GetComponent<CombatComponent>();
        if (targetPlayer != null)
        {
            targetPlayer.TakeDamage(unitData.attackDamage);
        }
    }

    [PunRPC]
    private void RPC_PlayAttackAnimation()
    {
        PlayAttackAnimation();
    }

    private void PlayAttackAnimation()
    {
        if (_animator != null)
        {
            _animator.SetTrigger("Attack");
        }
    }

    public void TakeDamage(float rawDamage, DamageType type, GameObject attacker = null)
    {
        int attackerId = -1;
        if (attacker != null)
        {
            PhotonView pv = attacker.GetComponent<PhotonView>();
            if (pv != null) attackerId = pv.ViewID;
        }

        if (PhotonNetwork.IsConnectedAndReady)
        {
            photonView.RPC("RPC_TakeDamage", RpcTarget.All, rawDamage, (int)type, attackerId);
        }
        else
        {
            ProcessDamage(rawDamage, type, attacker);
        }
    }

    [PunRPC]
    private void RPC_TakeDamage(float rawDamage, int typeIndex, int attackerId)
    {
        GameObject attacker = null;
        if (attackerId != -1)
        {
            PhotonView pv = PhotonView.Find(attackerId);
            if (pv != null) attacker = pv.gameObject;
        }
        ProcessDamage(rawDamage, (DamageType)typeIndex, attacker);
    }

    private void ProcessDamage(float rawDamage, DamageType type, GameObject attacker = null)
    {
        float mult = GetDamageMultiplier(type, unitData.armorType);
        float damage = rawDamage * mult;

        _currentHealth = Mathf.Max(_currentHealth - damage, 0f);
        Debug.Log($"{unitData.unitName} took {damage} damage ({type} vs {unitData.armorType}). Remaining HP: {_currentHealth}/{unitData.maxHealth}");

        // Only MasterClient / local offline processes combat state modifications
        if (!PhotonNetwork.IsConnectedAndReady || PhotonNetwork.IsMasterClient)
        {
            if (_currentHealth > 0f && attacker != null)
            {
                // Retaliate if no target
                if (_target == null)
                {
                    _target = attacker.transform;
                }
            }
        }

        if (_currentHealth <= 0f)
        {
            Die();
        }
    }

    private float GetDamageMultiplier(DamageType damageType, ArmorType armorType)
    {
        switch (damageType)
        {
            case DamageType.Normal:
                if (armorType == ArmorType.Medium) return 1.5f;
                if (armorType == ArmorType.Fortified) return 0.7f;
                break;
            case DamageType.Pierce:
                if (armorType == ArmorType.Light) return 1.5f;
                if (armorType == ArmorType.Heavy) return 0.5f;
                break;
            case DamageType.Magic:
                if (armorType == ArmorType.Heavy) return 1.5f;
                if (armorType == ArmorType.Light) return 0.5f;
                break;
            case DamageType.Chaos:
                return 1.0f;
        }
        return 1.0f;
    }

    private void Die()
    {
        // If enemy, give bounty to all players or the local player that killed them
        if (isEnemy && GameManager.Instance != null)
        {
            if (ResourceManager.LocalInstance != null)
            {
                ResourceManager.LocalInstance.AddGold(unitData.goldBounty);
                Debug.Log($"Enemy Defeated: +{unitData.goldBounty} Gold!");
            }
        }

        if (PhotonNetwork.IsConnectedAndReady)
        {
            photonView.RPC("RPC_PlayDieAnimation", RpcTarget.All);
        }
        else
        {
            PlayDieAnimation();
        }

        if (!PhotonNetwork.IsConnectedAndReady || PhotonNetwork.IsMasterClient)
        {
            StartCoroutine(DelayDespawn(1.5f));
        }
    }

    [PunRPC]
    private void RPC_PlayDieAnimation()
    {
        PlayDieAnimation();
    }

    private void PlayDieAnimation()
    {
        if (_animator != null)
        {
            _animator.SetTrigger("Die");
        }
    }

    private IEnumerator DelayDespawn(float delay)
    {
        // Disable agent & collider immediately upon death
        if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
        
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        yield return new WaitForSeconds(delay);
        DespawnOrDisable();
    }

    private void DespawnOrDisable()
    {
        if (isEnemy)
        {
            Despawn();
        }
        else
        {
            if (PhotonNetwork.IsConnectedAndReady)
            {
                photonView.RPC("RPC_DisableUnit", RpcTarget.All);
            }
            else
            {
                DisableUnit();
            }
        }
    }

    [PunRPC]
    private void RPC_DisableUnit()
    {
        DisableUnit();
    }

    private void DisableUnit()
    {
        _isDead = true;
        
        // Hide visual model
        Transform visual = transform.Find("VisualModel");
        if (visual != null)
        {
            visual.gameObject.SetActive(false);
        }

        // Disable agent and collider
        if (agent != null) agent.enabled = false;
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;
    }

    public void ResetToSpawnPoint()
    {
        _isDead = false;

        // Re-enable visual model
        Transform visual = transform.Find("VisualModel");
        if (visual != null)
        {
            visual.gameObject.SetActive(true);
        }

        // Re-enable collider
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = true;

        // Reset position and rotation
        transform.position = _originalSpawnPosition;
        transform.rotation = _originalSpawnRotation;

        // Re-enable agent
        if (agent != null)
        {
            agent.enabled = false;
            if (NavMesh.SamplePosition(_originalSpawnPosition, out NavMeshHit navHit, 5.0f, NavMesh.AllAreas))
            {
                transform.position = navHit.position;
            }
            agent.enabled = true;
            if (agent.isOnNavMesh)
            {
                agent.isStopped = true;
            }
        }

        // Reset health
        if (unitData != null)
        {
            _currentHealth = unitData.maxHealth;
        }

        // Reset target
        _target = null;

        // Reset animator state
        if (_animator != null)
        {
            _animator.Play("Idle");
            _animator.Rebind();
        }
    }

    private void Despawn()
    {
        if (isEnemy && WaveManager.Instance != null)
        {
            WaveManager.Instance.OnEnemyDestroyed();
        }

        if (PhotonNetwork.IsConnectedAndReady)
        {
            if (PhotonNetwork.IsMasterClient)
            {
                PhotonNetwork.Destroy(gameObject);
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
