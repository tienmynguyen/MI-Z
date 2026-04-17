using UnityEngine;
using UnityEngine.UI;

public class AutoGameHUD : MonoBehaviour
{
    [Header("Auto Create")]
    [SerializeField] private bool createCanvasIfMissing = true;
    [SerializeField] private string canvasName = "GameHUD_Canvas";

    [Header("Styling")]
    [SerializeField] private int fontSize = 24;
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private Vector2 bottomLeftStart = new(20f, 20f);
    [SerializeField] private float lineSpacing = 28f;
    [SerializeField] private Vector2 topCenterOffset = new(0f, -20f);

    private Canvas hudCanvas;
    private Text hpText;
    private Text coinText;
    private Text ammoText;
    private Text dayNightText;

    private PlayerNetwork localPlayer;
    private InventoryManager localInventory;
    private GameManager gameManager;
    private WaveManager waveManager;

    private void Start()
    {
        EnsureCanvas();
        EnsureTexts();
        ResolveReferences();
    }

    private void Update()
    {
        ResolveReferences();
        UpdateBottomLeftStats();
        UpdateTopCenterDayNight();
    }

    private void EnsureCanvas()
    {
        Canvas existing = FindAnyObjectByType<Canvas>();
        if (existing != null)
        {
            hudCanvas = existing;
            return;
        }

        if (!createCanvasIfMissing)
        {
            return;
        }

        GameObject canvasObject = new(canvasName);
        hudCanvas = canvasObject.AddComponent<Canvas>();
        hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObject.AddComponent<CanvasScaler>();
        canvasObject.AddComponent<GraphicRaycaster>();
    }

    private void EnsureTexts()
    {
        if (hudCanvas == null)
        {
            return;
        }

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        hpText = CreateOrFindText("HUD_HP", hudCanvas.transform, font);
        coinText = CreateOrFindText("HUD_Coin", hudCanvas.transform, font);
        ammoText = CreateOrFindText("HUD_Ammo", hudCanvas.transform, font);
        dayNightText = CreateOrFindText("HUD_DayNight", hudCanvas.transform, font);

        SetupBottomLeftLine(hpText.rectTransform, 0);
        SetupBottomLeftLine(coinText.rectTransform, 1);
        SetupBottomLeftLine(ammoText.rectTransform, 2);
        SetupTopCenter(dayNightText.rectTransform);
    }

    private void ResolveReferences()
    {
        if (localPlayer == null)
        {
            PlayerNetwork[] players = FindObjectsByType<PlayerNetwork>(FindObjectsSortMode.None);
            foreach (PlayerNetwork player in players)
            {
                if (player != null && (player.IsOwner || players.Length == 1))
                {
                    localPlayer = player;
                    break;
                }
            }
        }

        if (localInventory == null && localPlayer != null)
        {
            localInventory = localPlayer.GetComponent<InventoryManager>();
        }

        if (gameManager == null)
        {
            gameManager = FindAnyObjectByType<GameManager>();
        }

        if (waveManager == null)
        {
            waveManager = FindAnyObjectByType<WaveManager>();
        }
    }

    private void UpdateBottomLeftStats()
    {
        if (hpText != null)
        {
            int hpValue = localPlayer != null ? localPlayer.DisplayHealth : 0;
            hpText.text = $"HP: {hpValue}";
        }

        if (coinText != null)
        {
            int gold = localInventory != null ? localInventory.Gold : 0;
            coinText.text = $"Coin: {gold}";
        }

        if (ammoText != null)
        {
            if (localPlayer == null)
            {
                ammoText.text = "Ammo: 0/0";
            }
            else if (localPlayer.IsReloading)
            {
                ammoText.text = $"Ammo: {localPlayer.CurrentAmmoInMagazine}/{localPlayer.CurrentReserveAmmo} (Reloading)";
            }
            else
            {
                ammoText.text = $"Ammo: {localPlayer.CurrentAmmoInMagazine}/{localPlayer.CurrentReserveAmmo}";
            }
        }
    }

    private void UpdateTopCenterDayNight()
    {
        if (dayNightText == null)
        {
            return;
        }

        if (gameManager == null)
        {
            dayNightText.text = "Day/Night: --";
            return;
        }

        if (gameManager.CurrentPhase == GamePhase.Day)
        {
            dayNightText.text =
                $"DAY {gameManager.CurrentDay} | {FormatAsMinuteSecond(gameManager.RemainingDayTime)}";
            return;
        }

        string boss = gameManager.IsBossNight ? " BOSS" : string.Empty;
        int alive = waveManager != null ? waveManager.AliveZombieCount : 0;
        dayNightText.text = $"NIGHT {gameManager.CurrentNight}{boss} | Zombies: {alive}";
    }

    private Text CreateOrFindText(string objectName, Transform parent, Font font)
    {
        Transform existing = parent.Find(objectName);
        if (existing != null && existing.TryGetComponent(out Text text))
        {
            return text;
        }

        GameObject textObject = new(objectName);
        textObject.transform.SetParent(parent, false);
        Text created = textObject.AddComponent<Text>();
        created.font = font;
        created.fontSize = fontSize;
        created.color = textColor;
        created.horizontalOverflow = HorizontalWrapMode.Overflow;
        created.verticalOverflow = VerticalWrapMode.Overflow;
        created.alignment = TextAnchor.MiddleLeft;
        created.text = objectName;
        return created;
    }

    private void SetupBottomLeftLine(RectTransform rect, int lineIndex)
    {
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0f, 0f);
        rect.anchoredPosition = bottomLeftStart + (Vector2.up * (lineSpacing * lineIndex));
        rect.sizeDelta = new Vector2(520f, lineSpacing);
    }

    private void SetupTopCenter(RectTransform rect)
    {
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = topCenterOffset;
        rect.sizeDelta = new Vector2(800f, 40f);
        dayNightText.alignment = TextAnchor.MiddleCenter;
    }

    private string FormatAsMinuteSecond(float totalSeconds)
    {
        int seconds = Mathf.CeilToInt(Mathf.Max(0f, totalSeconds));
        int minutesPart = seconds / 60;
        int secondsPart = seconds % 60;
        return $"{minutesPart:00}:{secondsPart:00}";
    }
}
