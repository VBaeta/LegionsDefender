using UnityEngine;
using Photon.Pun;

public class ResourceManager : MonoBehaviourPun
{
    public static ResourceManager LocalInstance { get; private set; }

    [Header("Resources")]
    [SerializeField] private int gold = 1000;
    [SerializeField] private int lumber = 100;

    [Header("Lumberjacks")]
    [SerializeField] private int lumberjackCount = 1;
    [SerializeField] private int lumberjackEfficiency = 1;

    public int Gold => gold;
    public int Lumber => lumber;
    public int LumberjackCount => lumberjackCount;
    public float LumberjackEfficiency => lumberjackEfficiency;

    private void Awake()
    {
        if (photonView.IsMine)
        {
            LocalInstance = this;
        }
    }

    private void Start()
    {
        if (!photonView.IsMine) return;

        // Pull starting values from GameManager if available
        if (GameManager.Instance != null)
        {
            gold = GameManager.Instance.startingGold;
            lumber = GameManager.Instance.startingLumber;
        }
    }

    public void AddGold(int amount)
    {
        gold += amount;
        Debug.Log($"[ResourceManager] Added {amount} Gold. Total: {gold}");
    }

    public bool SpendGold(int amount)
    {
        if (gold >= amount)
        {
            gold -= amount;
            Debug.Log($"[ResourceManager] Spent {amount} Gold. Remaining: {gold}");
            return true;
        }
        return false;
    }

    public void AddLumber(int amount)
    {
        lumber += amount;
        Debug.Log($"[ResourceManager] Added {amount} Lumber. Total: {lumber}");
    }

    public bool SpendLumber(int amount)
    {
        if (lumber >= amount)
        {
            lumber -= amount;
            Debug.Log($"[ResourceManager] Spent {amount} Lumber. Remaining: {lumber}");
            return true;
        }
        return false;
    }

    public void AddLumberjack()
    {
        lumberjackCount++;
        Debug.Log($"[ResourceManager] Lumberjack Count upgraded: {lumberjackCount}");
    }

    public void UpgradeEfficiency(int amount)
    {
        lumberjackEfficiency += amount;
        Debug.Log($"[ResourceManager] Lumberjack Efficiency upgraded: {lumberjackEfficiency * 100}%");
    }
}
