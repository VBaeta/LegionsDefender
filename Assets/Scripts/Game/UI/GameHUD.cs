using UnityEngine;
using TMPro;
using Photon.Pun;

public class GameHUD : MonoBehaviour
{
    [Header("Resource Texts")]
    [SerializeField] private TextMeshProUGUI goldText;
    [SerializeField] private TextMeshProUGUI lumberText;

    [Header("Lumberjack Texts")]
    [SerializeField] private TextMeshProUGUI lumberjacksCountText;
    [SerializeField] private TextMeshProUGUI lumberjacksQualityText;

    [Header("Castle Texts")]
    [SerializeField] private TextMeshProUGUI castleHealthText;

    [Header("Wave Texts")]
    [SerializeField] private TextMeshProUGUI currentWaveText;
    [SerializeField] private TextMeshProUGUI remainingEnemiesText;
    [SerializeField] private TextMeshProUGUI phaseTimerText;

    [Header("Player Stats Texts")]
    [SerializeField] private TextMeshProUGUI playerHealthText;
    [SerializeField] private TextMeshProUGUI playerManaText;

    private void Update()
    {
        UpdateResourceHUD();
        UpdateLumberjackHUD();
        UpdateCastleHUD();
        UpdateWaveHUD();
        UpdatePlayerStatsHUD();
    }

    private void UpdateResourceHUD()
    {
        if (ResourceManager.LocalInstance != null)
        {
            if (goldText != null)
                goldText.text = ResourceManager.LocalInstance.Gold.ToString();
            if (lumberText != null)
                lumberText.text = ResourceManager.LocalInstance.Lumber.ToString();
        }
        else
        {
            if (goldText != null) goldText.text = "";
            if (lumberText != null) lumberText.text = "";
        }
    }

    private void UpdateLumberjackHUD()
    {
        if (ResourceManager.LocalInstance != null)
        {
            if (lumberjacksCountText != null)
                lumberjacksCountText.text = ResourceManager.LocalInstance.LumberjackCount.ToString();
            if (lumberjacksQualityText != null)
                lumberjacksQualityText.text = ResourceManager.LocalInstance.LumberjackEfficiency.ToString();
        }
        else
        {
            if (lumberjacksCountText != null) lumberjacksCountText.text = "";
            if (lumberjacksQualityText != null) lumberjacksQualityText.text = "";
        }
    }

    private void UpdateCastleHUD()
    {
        if (GameManager.Instance != null)
        {
            float current = GameManager.Instance.CastleCurrentHealth;
            float max = GameManager.Instance.CastleMaxHealth;
            float pct = GameManager.Instance.CastleHealthPercentage * 100f;

            if (castleHealthText != null)
                castleHealthText.text = $"Castle HP: {current} / {max}";
        }
        else
        {
            if (castleHealthText != null) castleHealthText.text = "Castle HP: --";
        }
    }

    private void UpdateWaveHUD()
    {
        if (WaveManager.Instance != null)
        {
            int current = WaveManager.Instance.CurrentWaveIndex + 1; // 1-indexed for display
            int total = WaveManager.Instance.TotalWavesCount;
            int enemies = WaveManager.Instance.ActiveEnemiesCount;

            if (currentWaveText != null)
                currentWaveText.text = $"{current} / {total}";
            if (remainingEnemiesText != null)
                remainingEnemiesText.text = $"{enemies}";
        }
        else
        {
            if (currentWaveText != null) currentWaveText.text = "";
            if (remainingEnemiesText != null) remainingEnemiesText.text = "";
        }

        if (GameManager.Instance != null)
        {
            GamePhase phase = GameManager.Instance.CurrentPhase;
            float timer = GameManager.Instance.PhaseTimer;

            if (phaseTimerText != null)
            {
                if (phase == GamePhase.Building)
                {
                    phaseTimerText.text = $"{timer}";
                }
                else
                {
                    phaseTimerText.text = "FIGHT PHASE!";
                }
            }
        }
        else
        {
            if (phaseTimerText != null) phaseTimerText.text = "";
        }
    }

    private void UpdatePlayerStatsHUD()
    {
        if (CombatComponent.LocalInstance != null)
        {
            float hp = CombatComponent.LocalInstance.currentHealth;
            float maxHp = CombatComponent.LocalInstance.maxHealth;
            float mp = CombatComponent.LocalInstance.currentMana;
            float maxMp = CombatComponent.LocalInstance.maxMana;

            if (playerHealthText != null)
                playerHealthText.text = $"<color=#FF5555>{hp:F0}</color> / {maxHp:F0}";
            if (playerManaText != null)
                playerManaText.text = $"<color=#5555FF>{mp:F0}</color> / {maxMp:F0}";
        }
        else
        {
            if (playerHealthText != null) playerHealthText.text = "";
            if (playerManaText != null) playerManaText.text = "";
        }
    }
}
