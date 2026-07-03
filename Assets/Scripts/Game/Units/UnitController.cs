using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using Photon.Pun;

public class UnitController : MonoBehaviourPun
{
    [Header("Configuration Data")]
    [SerializeField] private UnitData unitData;
    public bool isEnemy = false;

    [Header("Movement & Target")]
    [SerializeField] private NavMeshAgent agent;
    
    private Transform _target;
    private float _currentHealth;
    private float _nextAttackTime = 0f;

    // Enemy movement paths
    private Vector3[] _waypoints;
    private int _currentWaypointIndex = 0;

    public float CurrentHealth => _currentHealth;
    public UnitData Data => unitData;

    private void Start()
    {
        if (unitData == null)
        {
            Debug.LogError("No UnitData asset assigned to UnitController!");
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
            var targetPlayer = _target.GetComponent<CharacterCombat>();
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

                var player = hit.GetComponent<CharacterCombat>();
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

        var targetUnit = _target.GetComponent<UnitController>();
        if (targetUnit != null)
        {
            targetUnit.TakeDamage(unitData.attackDamage, unitData.damageType);
        }

        var targetPlayer = _target.GetComponent<CharacterCombat>();
        if (targetPlayer != null)
        {
            targetPlayer.TakeDamage(unitData.attackDamage);
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
            var combat = FindObjectOfType<CharacterCombat>();
            if (combat != null)
            {
                combat.gold += unitData.goldBounty;
                Debug.Log($"Enemy Defeated: +{unitData.goldBounty} Gold!");
            }
        }

        Despawn();
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
