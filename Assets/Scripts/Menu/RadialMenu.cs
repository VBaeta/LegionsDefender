using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[System.Serializable]
public class RadialOption
{
    public string name;
    public string description;
    public Sprite icon;
    public int goldCost;
    public int lumberCost;
    public Action callback;
}

public class RadialMenu : MonoBehaviour
{
    public static RadialMenu Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private Transform optionsContainer;
    [SerializeField] private GameObject optionPrefab;
    [SerializeField] private TMP_Text centerTitleText;
    [SerializeField] private TMP_Text centerDescriptionText;

    private List<RadialOption> _currentOptions = new List<RadialOption>();
    private List<GameObject> _instantiatedItems = new List<GameObject>();
    private int _hoveredIndex = -1;
    private Action<int> _onSelectionMade;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        if (menuPanel != null)
        {
            menuPanel.SetActive(false);
        }
    }

    public bool IsOpen => menuPanel != null && menuPanel.activeSelf;

    public void Open(List<RadialOption> options, Action<int> onSelectionMade = null)
    {
        if (menuPanel == null) return;

        _currentOptions = options;
        _onSelectionMade = onSelectionMade;
        _hoveredIndex = -1;

        // Clear old items
        foreach (var item in _instantiatedItems)
        {
            Destroy(item);
        }
        _instantiatedItems.Clear();

        if (centerTitleText != null) centerTitleText.text = "";
        if (centerDescriptionText != null) centerDescriptionText.text = "Select an option";

        int count = options.Count;
        if (count == 0) return;

        float angleStep = 360f / count;

        for (int i = 0; i < count; i++)
        {
            GameObject itemGo = Instantiate(optionPrefab, optionsContainer);
            _instantiatedItems.Add(itemGo);

            // Position radially
            float angle = i * angleStep;
            float rad = angle * Mathf.Deg2Rad;
            // Radius of radial menu positioning in pixels (e.g. 150)
            float radius = 150f;
            Vector3 pos = new Vector3(Mathf.Sin(rad) * radius, Mathf.Cos(rad) * radius, 0);
            itemGo.transform.localPosition = pos;

            // Set content
            var itemImg = itemGo.transform.Find("Icon")?.GetComponent<Image>();
            if (itemImg != null && options[i].icon != null)
            {
                itemImg.sprite = options[i].icon;
                itemImg.gameObject.SetActive(true);
            }
            else if (itemImg != null)
            {
                itemImg.gameObject.SetActive(false);
            }

            var textName = itemGo.transform.Find("TextName")?.GetComponent<TMP_Text>();
            if (textName != null)
            {
                textName.text = options[i].name;
            }
        }

        menuPanel.SetActive(true);
        // Force unlock cursor so player can pick
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private void Update()
    {
        if (!IsOpen) return;

        Vector3 mousePos = Input.mousePosition - new Vector3(Screen.width / 2f, Screen.height / 2f, 0f);
        float distance = mousePos.magnitude;

        // Deadzone - mouse must be at least 40px away from center to select a sector
        if (distance > 40f && _currentOptions.Count > 0)
        {
            float angleStep = 360f / _currentOptions.Count;
            // Calculate angle (0 degrees is straight up, rotating clockwise)
            float angle = Mathf.Atan2(mousePos.x, mousePos.y) * Mathf.Rad2Deg;
            if (angle < 0) angle += 360f;

            // Find closest sector
            int newHoverIndex = Mathf.RoundToInt(angle / angleStep) % _currentOptions.Count;

            if (newHoverIndex != _hoveredIndex)
            {
                HighlightOption(newHoverIndex);
            }

            // Click to select
            if (Input.GetMouseButtonDown(0))
            {
                SelectOption(_hoveredIndex);
            }
        }
        else
        {
            if (_hoveredIndex != -1)
            {
                HighlightOption(-1);
            }
        }
    }

    private void HighlightOption(int index)
    {
        _hoveredIndex = index;

        for (int i = 0; i < _instantiatedItems.Count; i++)
        {
            var outline = _instantiatedItems[i].transform.Find("Outline")?.GetComponent<Image>();
            if (outline != null)
            {
                outline.color = (i == index) ? Color.green : Color.white;
            }
        }

        if (index >= 0 && index < _currentOptions.Count)
        {
            var opt = _currentOptions[index];
            if (centerTitleText != null)
            {
                centerTitleText.text = opt.name;
            }
            if (centerDescriptionText != null)
            {
                string costInfo = "";
                if (opt.goldCost > 0 || opt.lumberCost > 0)
                {
                    costInfo = $"\nCost: {opt.goldCost} Gold | {opt.lumberCost} Lumber";
                }
                centerDescriptionText.text = opt.description + costInfo;
            }
        }
        else
        {
            if (centerTitleText != null) centerTitleText.text = "";
            if (centerDescriptionText != null) centerDescriptionText.text = "Hover over an option";
        }
    }

    private void SelectOption(int index)
    {
        if (index >= 0 && index < _currentOptions.Count)
        {
            _currentOptions[index].callback?.Invoke();
            _onSelectionMade?.Invoke(index);
        }
        Close();
    }

    public void Close()
    {
        if (menuPanel != null)
        {
            menuPanel.SetActive(false);
        }

        // Restore cursor lock mode (e.g. if the character movement locks it)
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }
}
