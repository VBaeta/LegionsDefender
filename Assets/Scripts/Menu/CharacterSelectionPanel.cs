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
    private KingdomType _selectedKingdom = KingdomType.Humans;

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
        // On enable, check if local player already has a selected character, and default the tab filter to that character's kingdom
        string currentSelection = "";
        if (lobbyManager != null)
        {
            currentSelection = lobbyManager.GetLocalPlayerSelectedCharacter();
        }

        if (!string.IsNullOrEmpty(currentSelection) && characterDatabase != null)
        {
            CharacterData data = characterDatabase.GetByName(currentSelection);
            if (data != null)
            {
                _selectedKingdom = data.kingdom;
            }
        }

        CreateKingdomTabs();
        PopulateGrid();
    }

    private void CreateKingdomTabs()
    {
        // 1. Find or create tabs container
        Transform existingTabs = transform.Find("KingdomTabs");
        GameObject tabsGo;
        if (existingTabs != null)
        {
            tabsGo = existingTabs.gameObject;
        }
        else
        {
            tabsGo = new GameObject("KingdomTabs", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            tabsGo.transform.SetParent(transform, false);
        }

        RectTransform rt = tabsGo.GetComponent<RectTransform>();
        // Position it right above the CharacterGrid
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 1f); // top-left
        rt.anchoredPosition = new Vector2(105f, 378f); // Start at the top boundary of where grid used to start
        rt.sizeDelta = new Vector2(960f, 50f); // Width matches grid, height = 50

        HorizontalLayoutGroup hlg = tabsGo.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 10f;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;
        hlg.childAlignment = TextAnchor.MiddleCenter;

        // Shift the CharacterGrid down slightly and adjust height to prevent overlap
        Transform gridTrans = transform.Find("CharacterGrid");
        if (gridTrans != null)
        {
            RectTransform gridRt = gridTrans.GetComponent<RectTransform>();
            if (gridRt != null)
            {
                gridRt.anchoredPosition = new Vector2(105f, -30f); // Shifted down from 0
                gridRt.sizeDelta = new Vector2(960f, 676f); // Reduced height from 756
            }
        }

        // Clear existing tabs children if any
        foreach (Transform child in tabsGo.transform)
        {
            Destroy(child.gameObject);
        }

        // Dynamic tab buttons based on KingdomType enum values
        foreach (KingdomType kingdom in System.Enum.GetValues(typeof(KingdomType)))
        {
            GameObject btnGo = new GameObject(kingdom.ToString() + "Tab", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            btnGo.transform.SetParent(tabsGo.transform, false);

            Image img = btnGo.GetComponent<Image>();
            img.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

            Button btn = btnGo.GetComponent<Button>();

            GameObject txtGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            txtGo.transform.SetParent(btnGo.transform, false);

            TextMeshProUGUI txt = txtGo.GetComponent<TextMeshProUGUI>();
            txt.text = kingdom.ToString().ToUpper(); // Make it UPPERCASE for a premium feel
            txt.fontSize = 16;
            txt.alignment = TextAlignmentOptions.Center;
            txt.color = Color.white;
            txt.fontStyle = FontStyles.Bold;

            KingdomType k = kingdom;
            btn.onClick.AddListener(() => OnKingdomTabClicked(k));
        }

        UpdateTabSelectionVisuals();
    }

    private void OnKingdomTabClicked(KingdomType kingdom)
    {
        _selectedKingdom = kingdom;
        UpdateTabSelectionVisuals();
        PopulateGrid();
    }

    private void UpdateTabSelectionVisuals()
    {
        Transform tabs = transform.Find("KingdomTabs");
        if (tabs == null) return;

        for (int i = 0; i < tabs.childCount; i++)
        {
            Transform tab = tabs.GetChild(i);
            Image img = tab.GetComponent<Image>();
            TextMeshProUGUI txt = tab.GetComponentInChildren<TextMeshProUGUI>();
            
            if (tab.name == _selectedKingdom.ToString() + "Tab")
            {
                // Active tab color: Vibrant blue
                if (img != null) img.color = new Color(0.12f, 0.58f, 0.95f, 1f); 
                if (txt != null) txt.color = Color.white;
            }
            else
            {
                // Inactive tab color: Dark charcoal grey
                if (img != null) img.color = new Color(0.15f, 0.15f, 0.15f, 0.85f);
                if (txt != null) txt.color = new Color(0.7f, 0.7f, 0.7f, 1f);
            }
        }
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

            // Filter by Kingdom type
            if (data.kingdom != _selectedKingdom) continue;

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
