using System;
using System.Collections;
using UnityEngine;

public enum GamePhase
{
    Day,
    Night
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Cycle Settings")]
    [SerializeField] private float dayDurationSeconds = 120f;

    [Header("References")]
    [SerializeField] private WaveManager waveManager;

    public GamePhase CurrentPhase { get; private set; } = GamePhase.Day;
    public int CurrentDay { get; private set; } = 1;
    public int CurrentNight { get; private set; }
    public float RemainingDayTime { get; private set; }
    public bool IsBossNight => CurrentNight > 0 && CurrentNight % 5 == 0;

    public event Action<int, float> OnDayStarted;
    public event Action<float> OnDayTimeUpdated;
    public event Action<int, bool> OnNightStarted;
    public event Action<int> OnNightEnded;

    private Coroutine gameLoopCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        if (waveManager == null)
        {
            waveManager = FindObjectOfType<WaveManager>();
        }

        if (waveManager == null)
        {
            Debug.LogError("GameManager requires a WaveManager in the scene.");
            enabled = false;
            return;
        }

        gameLoopCoroutine = StartCoroutine(GameLoop());
    }

    private IEnumerator GameLoop()
    {
        while (true)
        {
            yield return RunDayPhase();
            yield return RunNightPhase();
            CurrentDay++;
        }
    }

    private IEnumerator RunDayPhase()
    {
        CurrentPhase = GamePhase.Day;
        RemainingDayTime = dayDurationSeconds;
        OnDayStarted?.Invoke(CurrentDay, dayDurationSeconds);

        while (RemainingDayTime > 0f)
        {
            RemainingDayTime -= Time.deltaTime;
            OnDayTimeUpdated?.Invoke(Mathf.Max(RemainingDayTime, 0f));
            yield return null;
        }

        RemainingDayTime = 0f;
        OnDayTimeUpdated?.Invoke(RemainingDayTime);
    }

    private IEnumerator RunNightPhase()
    {
        CurrentPhase = GamePhase.Night;
        CurrentNight = CurrentDay;

        bool isBossWave = IsBossNight;
        OnNightStarted?.Invoke(CurrentNight, isBossWave);

        waveManager.StartNewWave(CurrentNight);

        while (!waveManager.IsCurrentWaveCleared)
        {
            yield return null;
        }

        OnNightEnded?.Invoke(CurrentNight);
    }

    public void RestartCycle()
    {
        if (gameLoopCoroutine != null)
        {
            StopCoroutine(gameLoopCoroutine);
        }

        CurrentDay = 1;
        CurrentNight = 0;
        CurrentPhase = GamePhase.Day;
        RemainingDayTime = 0f;
        gameLoopCoroutine = StartCoroutine(GameLoop());
    }
}
