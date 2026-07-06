using UnityEngine;

[CreateAssetMenu(fileName = "NewUnit", menuName = "LegionsDefender/Unit Data")]
public class UnitData : ScriptableObject
{
    [Header("General Info")]
    public string unitName;
    [TextArea(3, 10)]
    public string description;
    public Sprite icon;

    [Header("Visual Model")]
    [Tooltip("The visual prefab (FBX model or prefab containing the mesh and animator). If assigned, this is used in preference to Mesh/Materials fields to support full animations.")]
    public GameObject visualPrefab;
    [Tooltip("The 3D mesh to display for this unit (e.g., a character body mesh).")]
    public Mesh mesh;
    [Tooltip("Materials applied to the mesh. Element order matches submesh indices.")]
    public Material[] materials;
    [Tooltip("Scale of the visual model. Adjust to fit the unit's intended size.")]
    public float modelScale = 1f;
    [Tooltip("Animator Controller with Idle/Walk/Attack/Die states. Requires parameters: Speed (float), Attack (trigger), Die (trigger).")]
    public RuntimeAnimatorController animatorController;
    [Tooltip("Avatar for humanoid animation retargeting. Leave empty for generic rigs.")]
    public Avatar animatorAvatar;

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
