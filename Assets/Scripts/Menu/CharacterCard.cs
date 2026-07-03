using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class CharacterCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private Image iconImage;
    [SerializeField] private Image outlineBorder;
    
    private CharacterData _characterData;
    private CharacterSelectionPanel _panel;

    private void Awake()
    {
        UnityEngine.UI.Button btn = GetComponent<UnityEngine.UI.Button>();
        if (btn != null)
        {
            btn.onClick.AddListener(OnClick);
        }
    }

    public void Setup(CharacterData data, CharacterSelectionPanel selectionPanel)
    {
        _characterData = data;
        _panel = selectionPanel;

        if (nameText != null)
        {
            nameText.text = data.characterName;
        }

        if (iconImage != null)
        {
            iconImage.sprite = data.icon;
            iconImage.gameObject.SetActive(data.icon != null);
        }
        
        SetSelected(false);
    }

    public CharacterData GetCharacterData() => _characterData;

    public void SetSelected(bool isSelected)
    {
        if (outlineBorder != null)
        {
            outlineBorder.gameObject.SetActive(isSelected);
        }
    }

    public void OnClick()
    {
        if (_panel != null)
        {
            _panel.SelectCard(this);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_panel != null)
        {
            _panel.HoverCard(this);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (_panel != null)
        {
            _panel.RevertToSelectedInfo();
        }
    }
}
