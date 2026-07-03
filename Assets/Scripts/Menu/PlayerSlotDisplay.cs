using UnityEngine;
using TMPro;

public class PlayerSlotDisplay : MonoBehaviour
{
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private TMP_Text characterNameText;
    [SerializeField] private MeshFilter characterMeshFilter;
    [SerializeField] private MeshRenderer characterMeshRenderer;

    private void Awake()
    {
        // Ensure starting in empty state
        SetEmpty();
    }

    public void SetEmpty()
    {
        if (playerNameText != null)
        {
            playerNameText.text = "Waiting...";
            playerNameText.color = Color.gray;
        }

        if (characterNameText != null)
        {
            characterNameText.text = "";
        }

        if (characterMeshRenderer != null)
        {
            characterMeshRenderer.gameObject.SetActive(false);
        }
    }

    public void SetPlayer(string nickname, bool isReady)
    {
        if (playerNameText != null)
        {
            playerNameText.text = nickname;
            playerNameText.color = isReady ? Color.green : Color.red;
        }
    }

    public void SetCharacter(CharacterData data)
    {
        if (data == null)
        {
            ClearCharacter();
            return;
        }

        if (characterNameText != null)
        {
            characterNameText.text = data.characterName;
        }

        if (characterMeshFilter != null)
        {
            characterMeshFilter.mesh = data.characterMesh;
        }

        if (characterMeshRenderer != null)
        {
            if (data.characterMaterial != null)
            {
                characterMeshRenderer.material = data.characterMaterial;
            }
            characterMeshRenderer.gameObject.SetActive(true);
        }
    }

    public void ClearCharacter()
    {
        if (characterNameText != null)
        {
            characterNameText.text = "";
        }

        if (characterMeshRenderer != null)
        {
            characterMeshRenderer.gameObject.SetActive(false);
        }
    }
}
