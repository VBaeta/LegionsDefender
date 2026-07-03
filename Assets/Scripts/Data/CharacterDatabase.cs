using System;
using UnityEngine;

[CreateAssetMenu(fileName = "CharacterDatabase", menuName = "LegionsDefender/Character Database")]
public class CharacterDatabase : ScriptableObject
{
    public CharacterData[] characters;

    public CharacterData GetByName(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        
        foreach (var character in characters)
        {
            if (character != null && character.characterName == name)
            {
                return character;
            }
        }
        return null;
    }

    public int GetIndexByName(string name)
    {
        if (string.IsNullOrEmpty(name)) return -1;
        
        for (int i = 0; i < characters.Length; i++)
        {
            if (characters[i] != null && characters[i].characterName == name)
            {
                return i;
            }
        }
        return -1;
    }
}
