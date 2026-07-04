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

    // Enemy movement paths
    private Vector3[] _waypoints;
    private int _currentWaypointIndex = 0;

    public float CurrentHealth => _currentHealth;
    public UnitData Data => unitData;

    public void OnPhotonInstantiate(PhotonMessageInfo info)
    {
        object[] data = info.photonView.InstantiationData;
        if (data != null && data.Length > 0)
        {
            string unitName = (string)data[0];
            
            // Check in Characters (Allies) or Enemies folder
            UnitData loadedData = Resources.Load<UnitData>($"Characters/{unitName}");
            if (loadedData == null)
            {
                loadedData = Resources.Load<UnitData>($"Enemies/{unitName}");
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
                    agent.speed = unitData.moveSpeed;
                }

                SpawnVisualModel();
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

        if (unitData != null && unitData.prefab != null)
        {
            GameObject visual = Instantiate(unitData.prefab, transform);
            visual.name = "VisualModel";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;

            // Cache animator
            _animator = visual.GetComponent<Animator>();
            if (_animator == null)
            {
                _animator = visual.GetComponentInChildren<Animator>();
            }
        }
    }

    private void Start()
    {
        // Cache original spawn position and rotation for end-of-wave resets
        _originalSpawnPosition = transform.position;
        _originalSpawnRotation = transform.rotation;

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
            agent.speed = unitData.moveSpeed;
        }

        // Initialize target loop
        InvokeRepeating("FindTarget", 0f, 0.5f);
    }

    private void Update()
    {
        if (_isDead) return;

        // Update locomotion animation for all clients
        if (_animator != null && agent != null && agent.isOnNavMesh)
        {
            float normSpeed = agent.velocity.magnitude / agent.speed;
            _animator.SetFloat("Speed", normSpeed);
        }

        // Combat target and movement updates are processed on the owner/master client to prevent race conditions
        if (PhotonNetwork.IsConnectedAndReady && !PhotonNetwork.IsMasterClient) return;

        if (_target != null)
        {
            // Fight target
            float distance = Vector3.Distance(transform.position, _target.position);
            
            if (distance <= unitData.attackRange)
            {
                if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
                
                if (Time.time >= _nextAttackTime)
                {
                    AttackTarget();
                }
            }
            else
            {
                // Chase target
                if (agent != null && agent.isOnNavMesh)
                {
                    agent.isStopped = false;
                    agent.SetDestination(_target.position);
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
                if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
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
        if (agent == null || !agent.isOnNavMesh || _waypoints == null || _waypoints.Length == 0) return;

        agent.isStopped = false;
        Vector3 targetDest = _waypoints[_currentWaypointIndex];
        agent.SetDestination(targetDest);

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

        // Reset if target is dead
        if (_target != null)
        {
            var targetUnit = _target.GetComponent<UnitController>();
            var targetPlayer = _target.GetComponent<CombatComponent>();
            if ((targetUnit != null && targetUnit.CurrentHealth <= 0) || 
                (targetPlayer != null && targetPlayer.currentHealth <= 0))
            {
                _target = null;
            }
        }

        if (_target != null) return;

        Collider[] hits = Physics.OverlapSphere(transform.position, 10f); // 10m search radius
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
            targetUnit.TakeDamage(unitData.attackDamage, unitData.damageType);
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

    public void TakeDamage(float rawDamage, DamageType type)
    {
        if (PhotonNetwork.IsConnectedAndReady)
        {
            photonView.RPC("RPC_TakeDamage", RpcTarget.All, rawDamage, (int)type);
        }
        else
        {
            ProcessDamage(rawDamage, type);
        }
    }

    [PunRPC]
    private void RPC_TakeDamage(float rawDamage, int typeIndex)
    {
        ProcessDamage(rawDamage, (DamageType)typeIndex);
    }

    private void ProcessDamage(float rawDamage, DamageType type)
    {
        float mult = GetDamageMultiplier(type, unitData.armorType);
        float damage = rawDamage * mult;

        _currentHealth = Mathf.Max(_currentHealth - damage, 0f);
        Debug.Log($"{unitData.unitName} took {damage} damage ({type} vs {unitData.armorType}). Remaining HP: {_currentHealth}/{unitData.maxHealth}");

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

        // Reset position and rotation
        transform.position = _originalSpawnPosition;
        transform.rotation = _originalSpawnRotation;

        // Re-enable visual model
        Transform visual = transform.Find("VisualModel");
        if (visual != null)
        {
            visual.gameObject.SetActive(true);
        }

        // Re-enable collider
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = true;

        // Re-enable agent
        if (agent != null)
        {
            agent.enabled = true;
            if (agent.isOnNavMesh)
            {
                agent.Warp(_originalSpawnPosition);
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
