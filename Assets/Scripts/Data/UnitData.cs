using UnityEngine;

[CreateAssetMenu(fileName = "NewUnit", menuName = "LegionsDefender/Unit Data")]
public class UnitData : ScriptableObject
{
    [Header("General Info")]
    public string unitName;
    [TextArea(3, 10)]
    public string description;
    public Sprite icon;
    public GameObject prefab;

    [Header("Costs & Bounty")]
    public int goldCost;
    public int lumberCost;
    public int goldBounty;

    [Header("Stats")]
    public float maxHealth = 100f;
    public float moveSpeed = 3f;
    public float attackDamage = 10f;
    public float attackRange = 1.5f;
    public float attackRate = 1f; // Attacks per second
    public bool isMelee = true;

    [Header("Types")]
    public DamageType damageType = DamageType.Normal;
    public ArmorType armorType = ArmorType.Light;
}
