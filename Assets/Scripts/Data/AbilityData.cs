using UnityEngine;

[CreateAssetMenu(fileName = "NewAbility", menuName = "LegionsDefender/Ability Data")]
public class AbilityData : ScriptableObject
{
    public string abilityName;
    [TextArea(3, 10)]
    public string description;
    public Sprite icon;
    public AbilityInputKey inputKey;
    public float manaCost;
    public float cooldown;
}
