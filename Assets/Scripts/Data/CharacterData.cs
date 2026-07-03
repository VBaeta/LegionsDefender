using UnityEngine;

[CreateAssetMenu(fileName = "NewCharacter", menuName = "LegionsDefender/Character Data")]
public class CharacterData : ScriptableObject
{
    public string characterName;
    [TextArea(3, 10)]
    public string description;
    public Sprite icon;
    public Mesh characterMesh;
    public Material characterMaterial;
}
