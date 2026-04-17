using UnityEngine;
using UnityEngine.UI;

public class DayNightCycleUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private WaveManager waveManager;
    [SerializeField] private Text phaseText;
    [SerializeField] private Text timerText;

    [Header("Fallback Display")]
    [SerializeField] private bool useDebugLogWhenNoText = true;

    private int currentNight;
    private bool isBossNight;
    private int aliveZombieCount;
    private bool isNightActive;

    private void Awake()
    {
        if (gameManager == null)
        {
            gameManager = FindAnyObjectByType<GameManager>();
        }

        if (waveManager == null)
        {
            waveManager = FindAnyObjectByType<WaveManager>();
        }
    }

    private void OnEnable()
    {
        if (gameManager != null)
        {
            gameManager.OnDayStarted += HandleDayStarted;
            gameManager.OnDayTimeUpdated += HandleDayTimeUpdated;
            gameManager.OnNightStarted += HandleNightStarted;
            gameManager.OnNightEnded += HandleNightEnded;
        }

        if (waveManager != null)
        {
            waveManager.OnAliveZombieCountChanged += HandleAliveZombieChanged;
        }

        RefreshUI();
    }

    private void OnDisable()
    {
        if (gameManager != null)
        {
            gameManager.OnDayStarted -= HandleDayStarted;
            gameManager.OnDayTimeUpdated -= HandleDayTimeUpdated;
            gameManager.OnNightStarted -= HandleNightStarted;
            gameManager.OnNightEnded -= HandleNightEnded;
        }

        if (waveManager != null)
        {
            waveManager.OnAliveZombieCountChanged -= HandleAliveZombieChanged;
        }
    }

    private void HandleDayStarted(int day, float duration)
    {
        isNightActive = false;
        currentNight = 0;
        isBossNight = false;
        aliveZombieCount = 0;

        SetPhaseText($"DAY {day}");
        SetTimerText($"Time Left: {FormatAsMinuteSecond(duration)}");
    }

    private void HandleDayTimeUpdated(float remainingSeconds)
    {
        if (isNightActive)
        {
            return;
        }

        SetTimerText($"Time Left: {FormatAsMinuteSecond(remainingSeconds)}");
    }

    private void HandleNightStarted(int night, bool bossWave)
    {
        isNightActive = true;
        currentNight = night;
        isBossNight = bossWave;

        string bossLabel = bossWave ? " (BOSS WAVE)" : string.Empty;
        SetPhaseText($"NIGHT {night}{bossLabel}");
        SetTimerText("Objective: Eliminate all zombies");
    }

    private void HandleNightEnded(int night)
    {
        if (!isNightActive || night != currentNight)
        {
            return;
        }

        SetTimerText("Night cleared!");
    }

    private void HandleAliveZombieChanged(int aliveCount)
    {
        aliveZombieCount = aliveCount;

        if (!isNightActive)
        {
            return;
        }

        string bossLabel = isBossNight ? " (Boss)" : string.Empty;
        SetTimerText($"Zombies Remaining: {aliveZombieCount}{bossLabel}");
    }

    private void RefreshUI()
    {
        if (gameManager == null)
        {
            SetPhaseText("GameManager not found");
            SetTimerText("Assign GameManager in inspector");
            return;
        }

        if (gameManager.CurrentPhase == GamePhase.Day)
        {
            SetPhaseText($"DAY {gameManager.CurrentDay}");
            SetTimerText($"Time Left: {FormatAsMinuteSecond(gameManager.RemainingDayTime)}");
            return;
        }

        bool bossNight = gameManager.IsBossNight;
        string bossLabelText = bossNight ? " (BOSS WAVE)" : string.Empty;
        SetPhaseText($"NIGHT {gameManager.CurrentNight}{bossLabelText}");

        if (waveManager != null)
        {
            SetTimerText($"Zombies Remaining: {waveManager.AliveZombieCount}");
        }
        else
        {
            SetTimerText("Objective: Eliminate all zombies");
        }
    }

    private string FormatAsMinuteSecond(float totalSeconds)
    {
        int seconds = Mathf.CeilToInt(Mathf.Max(0f, totalSeconds));
        int minutesPart = seconds / 60;
        int secondsPart = seconds % 60;
        return $"{minutesPart:00}:{secondsPart:00}";
    }

    private void SetPhaseText(string content)
    {
        if (phaseText != null)
        {
            phaseText.text = content;
            return;
        }

        if (useDebugLogWhenNoText)
        {
            Debug.Log($"[DayNightCycleUI] Phase: {content}");
        }
    }

    private void SetTimerText(string content)
    {
        if (timerText != null)
        {
            timerText.text = content;
            return;
        }

        if (useDebugLogWhenNoText)
        {
            Debug.Log($"[DayNightCycleUI] Timer: {content}");
        }
    }
}
