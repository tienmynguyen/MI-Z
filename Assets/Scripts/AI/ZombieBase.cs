using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(NetworkObject))]
public class ZombieBase : NetworkBehaviour
{
    [Header("Stats")]
    [SerializeField] protected int maxHealth = 100;
    [SerializeField] protected float moveSpeed = 3.5f;
    [SerializeField] protected int damage = 10;

    [Header("Combat")]
    [SerializeField] protected float detectRange = 45f;
    [SerializeField] protected float attackRange = 2f;
    [SerializeField] protected float attackInterval = 1.2f;
    [SerializeField] protected float targetRefreshInterval = 0.4f;

    [Header("Drop")]
    [SerializeField, Range(0f, 1f)] protected float coinDropChance = 0.35f;
    [SerializeField] protected GameObject goldCoinPrefab;
    [SerializeField] protected float dropHeightOffset = 0.25f;

    [Header("Debug")]
    [SerializeField] protected bool allowOfflineAiTesting = true;
    [SerializeField] protected bool autoSnapToNavMeshOnStart = true;
    [SerializeField] protected float navMeshSnapDistance = 4f;

    [Header("Animation")]
    [SerializeField] protected Animator animator;
    [SerializeField] protected float animationCrossFade = 0.12f;
    [SerializeField] protected float deathDespawnDelay = 2f;
    [SerializeField] protected float forceSwitchDelayAfterClipEnd = 1f;
    [SerializeField] protected string spawnGroundState = "Spawn_Ground";
    [SerializeField] protected string spawnAirState = "Spawn_Air";
    [SerializeField] protected string[] idleStates = { "Idle_A", "Idle_B" };
    [SerializeField] protected string[] walkStates = { "Walking_A", "Walking_B", "Walking_C" };
    [SerializeField] protected string[] runStates = { "Running_A", "Running_B" };
    [SerializeField] protected string hitAState = "Hit_A";
    [SerializeField] protected string hitBState = "Hit_B";
    [SerializeField] protected string deathAState = "Death_A";
    [SerializeField] protected string deathBState = "Death_B";
    [SerializeField] protected string attackUseItemState = "Use_Item";
    [SerializeField] protected string attackThrowState = "Throw";
    [SerializeField] protected string fallbackState = "T-Pose";

    protected readonly NetworkVariable<int> CurrentHealth = new(
        writePerm: NetworkVariableWritePermission.Server);

    protected readonly NetworkVariable<bool> Dead = new(
        writePerm: NetworkVariableWritePermission.Server);

    protected NavMeshAgent Agent;
    protected PlayerNetwork CurrentTarget;
    protected WaveManager WaveManager;

    private readonly List<PlayerNetwork> playerCache = new();
    private float nextAttackTime;
    private float nextTargetSearchTime;
    private bool serverStateInitialized;
    private bool deathSequenceStarted;
    private string currentAnimState;
    private float nextIdleAnimSwitchTime;
    private float nextMoveAnimSwitchTime;
    private int nextAttackAnimIndex;

    public bool IsDead => Dead.Value;

    protected virtual void Awake()
    {
        Agent = GetComponent<NavMeshAgent>();
        Agent.speed = moveSpeed;
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
    }

    protected virtual void Start()
    {
        if (autoSnapToNavMeshOnStart)
        {
            TrySnapToNearestNavMesh();
        }

        PlaySpawnAnimation();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            InitializeServerState();
        }
    }

    protected virtual void Update()
    {
        if (!ShouldRunAiLogic())
        {
            return;
        }

        if (!serverStateInitialized)
        {
            InitializeServerState();
        }

        if (Dead.Value)
        {
            return;
        }

        if (Time.time >= nextTargetSearchTime)
        {
            CurrentTarget = FindNearestAlivePlayer();
            nextTargetSearchTime = Time.time + targetRefreshInterval;
        }

        if (CurrentTarget == null)
        {
            TrySetAgentStopped(true);
            UpdateIdleAnimation();
            return;
        }

        UpdateMovementAndAttack(CurrentTarget);
    }

    protected virtual void UpdateMovementAndAttack(PlayerNetwork target)
    {
        float distance = Vector3.Distance(transform.position, target.transform.position);
        if (distance > detectRange)
        {
            TrySetAgentStopped(true);
            UpdateIdleAnimation();
            return;
        }

        if (distance <= attackRange)
        {
            TrySetAgentStopped(true);
            FaceTarget(target.transform.position);
            UpdateIdleAnimation();

            if (Time.time >= nextAttackTime)
            {
                nextAttackTime = Time.time + attackInterval;
                TryAttackTarget(target);
            }

            return;
        }

        if (!CanControlAgent())
        {
            return;
        }

        TrySetAgentStopped(false);
        Agent.SetDestination(target.transform.position);
        UpdateMoveAnimation(distance <= attackRange * 2.2f ? false : true);
    }

    protected virtual void TryAttackTarget(PlayerNetwork target)
    {
        if (target == null || target.IsDead)
        {
            return;
        }

        string attackState = nextAttackAnimIndex++ % 2 == 0 ? attackUseItemState : attackThrowState;
        TryPlayAnimation(attackState);
        target.TakeDamageFromServer(damage);
    }

    [ServerRpc(RequireOwnership = false)]
    public void ReceiveDamageServerRpc(int incomingDamage)
    {
        TakeDamageFromServer(incomingDamage);
    }

    public void TakeDamageFromServer(int incomingDamage)
    {
        if (!IsServer)
        {
            return;
        }

        if (incomingDamage <= 0 || Dead.Value)
        {
            return;
        }

        CurrentHealth.Value = Mathf.Max(0, CurrentHealth.Value - incomingDamage);
        if (CurrentHealth.Value <= 0)
        {
            HandleDeath();
            return;
        }

        TryPlayRandomAnimation(hitAState, hitBState);
    }

    protected virtual void HandleDeath()
    {
        if (Dead.Value || deathSequenceStarted)
        {
            return;
        }

        deathSequenceStarted = true;
        Dead.Value = true;
        TrySetAgentStopped(true);
        if (Agent != null)
        {
            Agent.enabled = false;
        }
        TryPlayRandomAnimation(deathAState, deathBState);

        TryDropGoldCoin();
        WaveManager?.RegisterZombieKilled();

        StartCoroutine(DespawnAfterDeathDelay());
    }

    private IEnumerator DespawnAfterDeathDelay()
    {
        if (deathDespawnDelay > 0f)
        {
            yield return new WaitForSeconds(deathDespawnDelay);
        }

        NetworkObject networkObject = GetComponent<NetworkObject>();
        if (networkObject != null && networkObject.IsSpawned)
        {
            networkObject.Despawn(true);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    protected virtual void TryDropGoldCoin()
    {
        if (goldCoinPrefab == null || Random.value > coinDropChance)
        {
            return;
        }

        Vector3 spawnPos = transform.position + Vector3.up * dropHeightOffset;
        GameObject coin = Instantiate(goldCoinPrefab, spawnPos, Quaternion.identity);

        NetworkObject coinNetworkObject = coin.GetComponent<NetworkObject>();
        if (coinNetworkObject != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            coinNetworkObject.Spawn();
        }
    }

    protected PlayerNetwork FindNearestAlivePlayer()
    {
        playerCache.Clear();
        PlayerNetwork[] players = FindObjectsByType<PlayerNetwork>(FindObjectsSortMode.None);
        if (players == null || players.Length == 0)
        {
            return null;
        }

        float bestDistanceSqr = float.MaxValue;
        PlayerNetwork bestTarget = null;
        Vector3 myPosition = transform.position;

        foreach (PlayerNetwork player in players)
        {
            if (player == null || player.IsDead || !player.gameObject.activeInHierarchy)
            {
                continue;
            }

            float sqrDistance = (player.transform.position - myPosition).sqrMagnitude;
            if (sqrDistance < bestDistanceSqr)
            {
                bestDistanceSqr = sqrDistance;
                bestTarget = player;
            }
        }

        return bestTarget;
    }

    protected virtual bool ShouldRunAiLogic()
    {
        if (IsServer)
        {
            return true;
        }

        if (!allowOfflineAiTesting)
        {
            return false;
        }

        return NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening;
    }

    protected virtual void InitializeServerState()
    {
        if (serverStateInitialized)
        {
            return;
        }

        CurrentHealth.Value = maxHealth;
        Dead.Value = false;
        nextAttackTime = Time.time + attackInterval;
        nextTargetSearchTime = Time.time;
        WaveManager = FindAnyObjectByType<WaveManager>();
        serverStateInitialized = true;
    }

    protected virtual void TrySnapToNearestNavMesh()
    {
        if (Agent == null || Agent.isOnNavMesh)
        {
            return;
        }

        if (!NavMesh.SamplePosition(transform.position, out NavMeshHit hit, navMeshSnapDistance, NavMesh.AllAreas))
        {
            return;
        }

        Agent.Warp(hit.position);
    }

    protected bool CanControlAgent()
    {
        return Agent != null && Agent.enabled && Agent.isOnNavMesh;
    }

    protected bool TrySetAgentStopped(bool stopped)
    {
        if (!CanControlAgent())
        {
            return false;
        }

        Agent.isStopped = stopped;
        return true;
    }

    protected void FaceTarget(Vector3 worldPosition)
    {
        Vector3 direction = worldPosition - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f)
        {
            return;
        }

        Quaternion lookRotation = Quaternion.LookRotation(direction.normalized);
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, 10f * Time.deltaTime);
    }

    protected void TryPlayAnimation(string stateName, bool forceRestart = false)
    {
        if (animator == null || string.IsNullOrEmpty(stateName))
        {
            return;
        }

        if (!forceRestart && currentAnimState == stateName)
        {
            return;
        }

        int stateHash = Animator.StringToHash(stateName);
        if (animator.HasState(0, stateHash))
        {
            animator.CrossFade(stateHash, animationCrossFade, 0);
            currentAnimState = stateName;
            return;
        }

        TryPlayFallbackAnimation();
    }

    protected void TryPlayRandomAnimation(params string[] states)
    {
        if (states == null || states.Length == 0)
        {
            return;
        }

        int randomIndex = Random.Range(0, states.Length);
        string stateName = states[randomIndex];
        bool forceRestart = stateName == currentAnimState;
        TryPlayAnimation(stateName, forceRestart);
    }

    protected void PlaySpawnAnimation()
    {
        bool onGround = CanControlAgent();
        TryPlayAnimation(onGround ? spawnGroundState : spawnAirState);
    }

    protected void UpdateIdleAnimation()
    {
        bool forceSwitch = IsCurrentAnimationNearEndNonLoop();
        if (!forceSwitch && Time.time < nextIdleAnimSwitchTime && !string.IsNullOrEmpty(currentAnimState))
        {
            return;
        }

        if (idleStates == null || idleStates.Length == 0)
        {
            return;
        }

        string stateName = PickRandomStateAvoidCurrent(idleStates);
        TryPlayAnimation(stateName, forceSwitch);
        nextIdleAnimSwitchTime = Time.time + Random.Range(0.35f, 0.8f);
    }

    protected void UpdateMoveAnimation(bool isRunning)
    {
        string[] sourceStates = isRunning ? runStates : walkStates;
        if (sourceStates == null || sourceStates.Length == 0)
        {
            return;
        }

        bool forceSwitch = IsCurrentAnimationNearEndNonLoop();
        if (!forceSwitch && Time.time < nextMoveAnimSwitchTime && !string.IsNullOrEmpty(currentAnimState))
        {
            return;
        }

        string stateName = PickRandomStateAvoidCurrent(sourceStates);
        TryPlayAnimation(stateName, forceSwitch);
        nextMoveAnimSwitchTime = Time.time + Random.Range(0.2f, 0.45f);
    }

    private void TryPlayFallbackAnimation()
    {
        if (animator == null || string.IsNullOrEmpty(fallbackState))
        {
            return;
        }

        int fallbackHash = Animator.StringToHash(fallbackState);
        if (!animator.HasState(0, fallbackHash) || currentAnimState == fallbackState)
        {
            return;
        }

        animator.CrossFade(fallbackHash, animationCrossFade, 0);
        currentAnimState = fallbackState;
    }

    private bool IsCurrentAnimationNearEndNonLoop()
    {
        if (animator == null)
        {
            return false;
        }

        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        if (stateInfo.loop)
        {
            return false;
        }

        float clipLength = Mathf.Max(0.01f, stateInfo.length);
        float normalizedThreshold = 1f + (forceSwitchDelayAfterClipEnd / clipLength);
        return stateInfo.normalizedTime >= normalizedThreshold;
    }

    private string PickRandomStateAvoidCurrent(string[] states)
    {
        if (states == null || states.Length == 0)
        {
            return string.Empty;
        }

        if (states.Length == 1)
        {
            return states[0];
        }

        int tries = 3;
        while (tries-- > 0)
        {
            string candidate = states[Random.Range(0, states.Length)];
            if (candidate != currentAnimState)
            {
                return candidate;
            }
        }

        return states[Random.Range(0, states.Length)];
    }
}
