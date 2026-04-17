using UnityEngine;
using UnityEngine.UI;

public class UpgradeShopUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private InventoryManager inventoryManager;
    [SerializeField] private UpgradeSystem upgradeSystem;

    [Header("UI")]
    [SerializeField] private Text goldText;
    [SerializeField] private Button damageButton;
    [SerializeField] private Button reloadButton;
    [SerializeField] private Button maxHealthButton;
    [SerializeField] private Button speedButton;
    [SerializeField] private Button jumpButton;
    [SerializeField] private Button flyButton;

    [Header("Button Labels (Optional)")]
    [SerializeField] private Text damageButtonText;
    [SerializeField] private Text reloadButtonText;
    [SerializeField] private Text maxHealthButtonText;
    [SerializeField] private Text speedButtonText;
    [SerializeField] private Text jumpButtonText;
    [SerializeField] private Text flyButtonText;

    private void Awake()
    {
        if (inventoryManager == null)
        {
            inventoryManager = FindAnyObjectByType<InventoryManager>();
        }

        if (upgradeSystem == null)
        {
            upgradeSystem = FindAnyObjectByType<UpgradeSystem>();
        }
    }

    private void OnEnable()
    {
        if (inventoryManager != null)
        {
            inventoryManager.OnGoldChanged += HandleGoldChanged;
        }

        if (upgradeSystem != null)
        {
            upgradeSystem.OnUpgradePurchased += HandleUpgradePurchased;
        }

        BindButtons();
        RefreshUI();
    }

    private void OnDisable()
    {
        if (inventoryManager != null)
        {
            inventoryManager.OnGoldChanged -= HandleGoldChanged;
        }

        if (upgradeSystem != null)
        {
            upgradeSystem.OnUpgradePurchased -= HandleUpgradePurchased;
        }

        UnbindButtons();
    }

    private void BindButtons()
    {
        if (upgradeSystem == null)
        {
            return;
        }

        if (damageButton != null) damageButton.onClick.AddListener(HandleBuyDamage);
        if (reloadButton != null) reloadButton.onClick.AddListener(HandleBuyReload);
        if (maxHealthButton != null) maxHealthButton.onClick.AddListener(HandleBuyMaxHealth);
        if (speedButton != null) speedButton.onClick.AddListener(HandleBuySpeed);
        if (jumpButton != null) jumpButton.onClick.AddListener(HandleBuyJump);
        if (flyButton != null) flyButton.onClick.AddListener(HandleBuyFly);
    }

    private void UnbindButtons()
    {
        if (damageButton != null) damageButton.onClick.RemoveAllListeners();
        if (reloadButton != null) reloadButton.onClick.RemoveAllListeners();
        if (maxHealthButton != null) maxHealthButton.onClick.RemoveAllListeners();
        if (speedButton != null) speedButton.onClick.RemoveAllListeners();
        if (jumpButton != null) jumpButton.onClick.RemoveAllListeners();
        if (flyButton != null) flyButton.onClick.RemoveAllListeners();
    }

    private void HandleBuyDamage() => upgradeSystem.TryBuyDamageUpgrade();
    private void HandleBuyReload() => upgradeSystem.TryBuyReloadUpgrade();
    private void HandleBuyMaxHealth() => upgradeSystem.TryBuyMaxHealthUpgrade();
    private void HandleBuySpeed() => upgradeSystem.TryBuyMoveSpeedUpgrade();
    private void HandleBuyJump() => upgradeSystem.TryBuyJumpUpgrade();
    private void HandleBuyFly() => upgradeSystem.TryBuyFlySkill();

    private void HandleGoldChanged(int _)
    {
        RefreshUI();
    }

    private void HandleUpgradePurchased(UpgradeType _, int __)
    {
        RefreshUI();
    }

    private void RefreshUI()
    {
        if (inventoryManager == null || upgradeSystem == null)
        {
            return;
        }

        if (goldText != null)
        {
            goldText.text = $"Gold: {inventoryManager.Gold}";
        }

        RefreshButton(damageButton, damageButtonText, UpgradeType.Damage, "Damage");
        RefreshButton(reloadButton, reloadButtonText, UpgradeType.ReloadSpeed, "Reload");
        RefreshButton(maxHealthButton, maxHealthButtonText, UpgradeType.MaxHealth, "Max HP");
        RefreshButton(speedButton, speedButtonText, UpgradeType.MoveSpeed, "Speed");
        RefreshButton(jumpButton, jumpButtonText, UpgradeType.JumpHeight, "Jump");
        RefreshButton(flyButton, flyButtonText, UpgradeType.FlySkill, "Fly");
    }

    private void RefreshButton(Button button, Text label, UpgradeType type, string title)
    {
        UpgradeConfig cfg = upgradeSystem.GetConfig(type);
        if (cfg == null)
        {
            return;
        }

        bool affordable = !cfg.IsMaxLevel && inventoryManager.CanAfford(cfg.CurrentCost);
        if (button != null)
        {
            button.interactable = !cfg.IsMaxLevel && affordable;
        }

        if (label != null)
        {
            if (cfg.IsMaxLevel)
            {
                label.text = $"{title}\nMAX";
            }
            else
            {
                label.text = $"{title}\nLv {cfg.Level}/{cfg.MaxLevel} - {cfg.CurrentCost}G";
            }
        }
    }
}
