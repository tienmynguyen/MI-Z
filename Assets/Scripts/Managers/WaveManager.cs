using System;
using UnityEngine;

public class WaveManager : MonoBehaviour
{
    [Header("Wave Settings")]
    [SerializeField] private int baseZombieCount = 8;
    [SerializeField] private int zombieIncreasePerWave = 3;
    [SerializeField] private int bossWaveBonusZombieCount = 10;

    public int CurrentWaveNumber { get; private set; }
    public bool IsBossWave { get; private set; }
    public int AliveZombieCount { get; private set; }
    public bool IsWaveInProgress { get; private set; }
    public bool IsCurrentWaveCleared => !IsWaveInProgress || AliveZombieCount <= 0;

    public event Action<int, bool, int> OnWaveStarted;
    public event Action<int> OnAliveZombieCountChanged;
    public event Action<int, bool> OnWaveCleared;

    public void StartNewWave(int waveNumber)
    {
        CurrentWaveNumber = waveNumber;
        IsBossWave = waveNumber % 5 == 0;
        IsWaveInProgress = true;

        AliveZombieCount = CalculateZombieCount(waveNumber, IsBossWave);
        OnWaveStarted?.Invoke(CurrentWaveNumber, IsBossWave, AliveZombieCount);
        OnAliveZombieCountChanged?.Invoke(AliveZombieCount);

        SpawnWave(AliveZombieCount, IsBossWave);
    }

    public void SetAliveZombieCount(int count)
    {
        if (!IsWaveInProgress)
        {
            return;
        }

        AliveZombieCount = Mathf.Max(0, count);
        OnAliveZombieCountChanged?.Invoke(AliveZombieCount);

        if (AliveZombieCount == 0)
        {
            CompleteWave();
        }
    }

    public void RegisterZombieKilled()
    {
        if (!IsWaveInProgress || AliveZombieCount <= 0)
        {
            return;
        }

        AliveZombieCount--;
        OnAliveZombieCountChanged?.Invoke(AliveZombieCount);

        if (AliveZombieCount <= 0)
        {
            CompleteWave();
        }
    }

    private int CalculateZombieCount(int waveNumber, bool isBossWave)
    {
        int normalWaveCount = baseZombieCount + ((waveNumber - 1) * zombieIncreasePerWave);
        if (isBossWave)
        {
            normalWaveCount += bossWaveBonusZombieCount;
        }

        return Mathf.Max(1, normalWaveCount);
    }

    private void CompleteWave()
    {
        AliveZombieCount = 0;
        IsWaveInProgress = false;
        OnAliveZombieCountChanged?.Invoke(AliveZombieCount);
        OnWaveCleared?.Invoke(CurrentWaveNumber, IsBossWave);
    }

    private void SpawnWave(int zombieCount, bool isBossWave)
    {
        // Hook your spawn system here (spawn points, navmesh, pooling...).
        // This log helps verify cycle logic before integrating real spawning.
        Debug.Log(
            $"Wave {CurrentWaveNumber} started | Boss: {isBossWave} | Zombies: {zombieCount}");
    }
}
