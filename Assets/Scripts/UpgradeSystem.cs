using System;
using UnityEngine;

public enum UpgradeType
{
    Damage,
    ReloadSpeed,
    MaxHealth,
    MoveSpeed,
    JumpHeight,
    FlySkill
}

[Serializable]
public class UpgradeConfig
{
    [SerializeField] private UpgradeType type = UpgradeType.Damage;
    [SerializeField] private int baseCost = 50;
    [SerializeField] private int costIncreasePerLevel = 25;
    [SerializeField] private int maxLevel = 10;
    [SerializeField] private float valuePerLevel = 1f;

    public UpgradeType Type => type;
    public int Level { get; private set; }
    public int MaxLevel => maxLevel;
    public float ValuePerLevel => valuePerLevel;
    public bool IsMaxLevel => Level >= maxLevel;
    public int CurrentCost => baseCost + (Level * costIncreasePerLevel);
    public void SetType(UpgradeType newType) => type = newType;

    public void Configure(
        UpgradeType newType,
        int newBaseCost,
        int newCostIncreasePerLevel,
        int newMaxLevel,
        float newValuePerLevel)
    {
        type = newType;
        baseCost = newBaseCost;
        costIncreasePerLevel = newCostIncreasePerLevel;
        maxLevel = newMaxLevel;
        valuePerLevel = newValuePerLevel;
    }

    public void IncreaseLevel()
    {
        Level++;
    }
}

[RequireComponent(typeof(InventoryManager))]
[RequireComponent(typeof(PlayerNetwork))]
public class UpgradeSystem : MonoBehaviour
{
    [Header("Upgrade Configs")]
    [SerializeField] private UpgradeConfig damageUpgrade = new();
    [SerializeField] private UpgradeConfig reloadSpeedUpgrade = new();
    [SerializeField] private UpgradeConfig maxHealthUpgrade = new();
    [SerializeField] private UpgradeConfig speedUpgrade = new();
    [SerializeField] private UpgradeConfig jumpUpgrade = new();
    [SerializeField] private UpgradeConfig flySkillUpgrade = new();

    [Header("Fly")]
    [SerializeField] private float flyDurationSeconds = 6f;

    private InventoryManager inventory;
    private PlayerNetwork playerNetwork;

    public event Action<UpgradeType, int> OnUpgradePurchased;

    private void Reset()
    {
        damageUpgrade.Configure(UpgradeType.Damage, 80, 45, 10, 5f);
        reloadSpeedUpgrade.Configure(UpgradeType.ReloadSpeed, 70, 35, 10, 0.08f);
        maxHealthUpgrade.Configure(UpgradeType.MaxHealth, 90, 40, 8, 15f);
        speedUpgrade.Configure(UpgradeType.MoveSpeed, 80, 30, 8, 0.35f);
        jumpUpgrade.Configure(UpgradeType.JumpHeight, 60, 30, 8, 0.2f);
        flySkillUpgrade.Configure(UpgradeType.FlySkill, 120, 70, 5, 1f);
    }

    private void Awake()
    {
        inventory = GetComponent<InventoryManager>();
        playerNetwork = GetComponent<PlayerNetwork>();
    }

    private void OnValidate()
    {
        if (damageUpgrade != null) damageUpgrade.SetType(UpgradeType.Damage);
        if (reloadSpeedUpgrade != null) reloadSpeedUpgrade.SetType(UpgradeType.ReloadSpeed);
        if (maxHealthUpgrade != null) maxHealthUpgrade.SetType(UpgradeType.MaxHealth);
        if (speedUpgrade != null) speedUpgrade.SetType(UpgradeType.MoveSpeed);
        if (jumpUpgrade != null) jumpUpgrade.SetType(UpgradeType.JumpHeight);
        if (flySkillUpgrade != null) flySkillUpgrade.SetType(UpgradeType.FlySkill);
    }

    public bool TryBuyDamageUpgrade() => TryPurchase(damageUpgrade);
    public bool TryBuyReloadUpgrade() => TryPurchase(reloadSpeedUpgrade);
    public bool TryBuyMaxHealthUpgrade() => TryPurchase(maxHealthUpgrade);
    public bool TryBuyMoveSpeedUpgrade() => TryPurchase(speedUpgrade);
    public bool TryBuyJumpUpgrade() => TryPurchase(jumpUpgrade);
    public bool TryBuyFlySkill() => TryPurchase(flySkillUpgrade);

    public UpgradeConfig GetConfig(UpgradeType type)
    {
        return type switch
        {
            UpgradeType.Damage => damageUpgrade,
            UpgradeType.ReloadSpeed => reloadSpeedUpgrade,
            UpgradeType.MaxHealth => maxHealthUpgrade,
            UpgradeType.MoveSpeed => speedUpgrade,
            UpgradeType.JumpHeight => jumpUpgrade,
            UpgradeType.FlySkill => flySkillUpgrade,
            _ => null
        };
    }

    private bool TryPurchase(UpgradeConfig config)
    {
        if (config == null || config.IsMaxLevel)
        {
            return false;
        }

        int cost = config.CurrentCost;
        if (!inventory.TrySpendGold(cost))
        {
            return false;
        }

        config.IncreaseLevel();
        ApplyUpgrade(config);
        OnUpgradePurchased?.Invoke(config.Type, config.Level);
        return true;
    }

    private void ApplyUpgrade(UpgradeConfig config)
    {
        switch (config.Type)
        {
            case UpgradeType.Damage:
                playerNetwork.AddWeaponDamage(Mathf.RoundToInt(config.ValuePerLevel));
                break;

            case UpgradeType.ReloadSpeed:
                playerNetwork.AddReloadSpeed(config.ValuePerLevel);
                break;

            case UpgradeType.MaxHealth:
                playerNetwork.AddMaxHealth(Mathf.RoundToInt(config.ValuePerLevel), true);
                break;

            case UpgradeType.MoveSpeed:
                playerNetwork.AddMoveSpeed(config.ValuePerLevel);
                break;

            case UpgradeType.JumpHeight:
                playerNetwork.AddJumpHeight(config.ValuePerLevel);
                break;

            case UpgradeType.FlySkill:
                playerNetwork.ActivateFly(flyDurationSeconds);
                break;
        }
    }

}
