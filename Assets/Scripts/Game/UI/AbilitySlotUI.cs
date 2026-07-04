using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AbilitySlotUI : MonoBehaviour
{
    [Header("Slot Key Configuration")]
    [SerializeField] private AbilityInputKey inputKey;

    [Header("UI Component References")]
    [SerializeField] private Image iconImage;
    [SerializeField] private GameObject countdownPanel;
    [SerializeField] private TextMeshProUGUI cooldownText;
    [SerializeField] private Image cooldownFill;

    [Header("Building Phase Configurations")]
    [SerializeField] private Sprite buildingIcon;
    [SerializeField] private string buildingName = "Build Action";
    [SerializeField] private string buildingDescription = "Performs building action";

    public AbilityInputKey InputKey => inputKey;

    private void Start()
    {
        // Turn off countdown panel initially
        if (countdownPanel != null)
        {
            countdownPanel.SetActive(false);
        }
    }

    private void Update()
    {
        if (GameManager.Instance == null) return;

        GamePhase currentPhase = GameManager.Instance.CurrentPhase;

        if (currentPhase == GamePhase.Building)
        {
            // Switch to building icons for Q, E, R
            if (inputKey == AbilityInputKey.Passive)
            {
                // For Passive, show the character's passive if available, or hide the slot
                UpdateCombatAbilityVisuals();
            }
            else
            {
                // Q, E, R show building phase actions
                if (iconImage != null)
                {
                    if (buildingIcon != null)
                    {
                        iconImage.gameObject.SetActive(true);
                        iconImage.sprite = buildingIcon;
                    }
                    else
                    {
                        // Fallback to placeholder/empty if no sprite assigned
                        iconImage.gameObject.SetActive(false);
                    }
                }

                if (countdownPanel != null)
                {
                    countdownPanel.SetActive(false);
                }
            }
        }
        else if (currentPhase == GamePhase.Combat)
        {
            UpdateCombatAbilityVisuals();
        }
    }

    private void UpdateCombatAbilityVisuals()
    {
        if (CombatComponent.LocalInstance == null)
        {
            if (iconImage != null) iconImage.gameObject.SetActive(false);
            if (countdownPanel != null) countdownPanel.SetActive(false);
            return;
        }

        AbilityData ability = CombatComponent.LocalInstance.GetAbilityData(inputKey);
        if (ability != null)
        {
            if (iconImage != null)
            {
                iconImage.gameObject.SetActive(true);
                iconImage.sprite = ability.icon;
            }

            // Passive does not have a cooldown overlay
            if (inputKey != AbilityInputKey.Passive)
            {
                float remaining = CombatComponent.LocalInstance.GetRemainingCooldown(inputKey);
                float max = CombatComponent.LocalInstance.GetMaxCooldown(inputKey);

                if (remaining > 0f)
                {
                    if (countdownPanel != null) countdownPanel.SetActive(true);
                    if (cooldownText != null) cooldownText.text = $"{remaining:F1}s";
                    if (cooldownFill != null) cooldownFill.fillAmount = remaining / max;
                }
                else
                {
                    if (countdownPanel != null) countdownPanel.SetActive(false);
                }
            }
            else
            {
                if (countdownPanel != null) countdownPanel.SetActive(false);
            }
        }
        else
        {
            // Hide the slot if character doesn't have an ability configured for this key
            if (iconImage != null) iconImage.gameObject.SetActive(false);
            if (countdownPanel != null) countdownPanel.SetActive(false);
        }
    }
}
