using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CharacterSelectionPanel : MonoBehaviour
{
    [Header("Database")]
    [SerializeField] private CharacterDatabase characterDatabase;

    [Header("UI References")]
    [SerializeField] private GameObject characterCardPrefab;
    [SerializeField] private Transform gridParent;
    
    [Header("Info Panel References")]
    [SerializeField] private Image largePortraitImage;
    [SerializeField] private TMP_Text largeNameText;
    [SerializeField] private TMP_Text largeDescriptionText;
    [SerializeField] private Button confirmSelectionButton;

    [Header("Lobby Integration")]
    [SerializeField] private LobbyManager lobbyManager;

    private List<CharacterCard> _instantiatedCards = new List<CharacterCard>();
    private CharacterCard _selectedCard;

    private void Awake()
    {
        if (confirmSelectionButton != null)
        {
            confirmSelectionButton.onClick.AddListener(OnConfirmClicked);
        }
        
        // Hide large info preview elements initially
        ClearInfoPanel();
    }

    private void OnEnable()
    {
        PopulateGrid();
    }

    public void PopulateGrid()
    {
        // Clear old cards
        foreach (var card in _instantiatedCards)
        {
            Destroy(card.gameObject);
        }
        _instantiatedCards.Clear();
        _selectedCard = null;
        if (confirmSelectionButton != null) confirmSelectionButton.interactable = false;
        
        ClearInfoPanel();

        if (characterDatabase == null || characterDatabase.characters == null) return;

        // Retrieve current local player selected character (if any)
        string currentSelection = "";
        if (lobbyManager != null)
        {
            currentSelection = lobbyManager.GetLocalPlayerSelectedCharacter();
        }

        foreach (var data in characterDatabase.characters)
        {
            if (data == null) continue;

            GameObject cardGo = Instantiate(characterCardPrefab, gridParent);
            CharacterCard card = cardGo.GetComponent<CharacterCard>();
            if (card != null)
            {
                card.Setup(data, this);
                _instantiatedCards.Add(card);

                // If this is the currently selected character, highlight it
                if (!string.IsNullOrEmpty(currentSelection) && data.characterName == currentSelection)
                {
                    SelectCard(card);
                    HoverCard(card); // Update preview info panel too
                }
            }
        }
    }

    public void SelectCard(CharacterCard card)
    {
        if (_selectedCard != null)
        {
            _selectedCard.SetSelected(false);
        }

        _selectedCard = card;
        
        if (_selectedCard != null)
        {
            _selectedCard.SetSelected(true);
            if (confirmSelectionButton != null) confirmSelectionButton.interactable = true;
            HoverCard(_selectedCard); // Lock preview to selected card
        }
        else
        {
            if (confirmSelectionButton != null) confirmSelectionButton.interactable = false;
        }
    }

    public void HoverCard(CharacterCard card)
    {
        if (card == null || card.GetCharacterData() == null) return;
        ShowCharacterInfo(card.GetCharacterData());
    }

    public void RevertToSelectedInfo()
    {
        if (_selectedCard != null && _selectedCard.GetCharacterData() != null)
        {
            ShowCharacterInfo(_selectedCard.GetCharacterData());
        }
        else
        {
            ClearInfoPanel();
        }
    }

    private void ShowCharacterInfo(CharacterData data)
    {
        if (largeNameText != null) largeNameText.text = data.characterName;
        if (largeDescriptionText != null) largeDescriptionText.text = data.description;
        
        if (largePortraitImage != null)
        {
            largePortraitImage.sprite = data.icon;
            largePortraitImage.gameObject.SetActive(data.icon != null);
        }
    }

    private void ClearInfoPanel()
    {
        if (largeNameText != null) largeNameText.text = "Select a character";
        if (largeDescriptionText != null) largeDescriptionText.text = "Hover over characters to view their details.";
        if (largePortraitImage != null) largePortraitImage.gameObject.SetActive(false);
    }

    private void OnConfirmClicked()
    {
        if (_selectedCard != null && lobbyManager != null)
        {
            lobbyManager.SelectCharacter(_selectedCard.GetCharacterData().characterName);
        }
        gameObject.SetActive(false);
    }

    public void ClosePanel()
    {
        gameObject.SetActive(false);
    }
}
