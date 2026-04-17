using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

public enum BossSkillType
{
    LeapAttack,
    PoisonBurst
}

public class BossZombie : ZombieBase
{
    [Header("Boss")]
    [SerializeField] private BossSkillType skillType = BossSkillType.LeapAttack;
    [SerializeField] private float skillCooldown = 8f;

    [Header("Leap Skill")]
    [SerializeField] private float leapDistance = 8f;
    [SerializeField] private float leapDuration = 0.55f;
    [SerializeField] private int leapDamage = 30;
    [SerializeField] private string leapStartState = "Jump_Start";
    [SerializeField] private string leapLoopState = "Jump_Full_Long";
    [SerializeField] private string leapLandState = "Jump_Land";
    [SerializeField] private string[] leapLoopFallbackStates = { "Jump_Full_Long", "Jump_Idle" };

    [Header("Poison Skill")]
    [SerializeField] private float poisonRadius = 6f;
    [SerializeField] private int poisonDamage = 20;
    [SerializeField] private string poisonCastState = "Throw";

    private float nextSkillTime;
    private bool isCastingSkill;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (IsServer)
        {
            nextSkillTime = Time.time + skillCooldown;
        }
    }

    protected override void UpdateMovementAndAttack(PlayerNetwork target)
    {
        if (isCastingSkill)
        {
            return;
        }

        if (Time.time >= nextSkillTime && target != null)
        {
            nextSkillTime = Time.time + skillCooldown;
            StartCoroutine(ExecuteSkill(target));
            return;
        }

        base.UpdateMovementAndAttack(target);
    }

    private IEnumerator ExecuteSkill(PlayerNetwork target)
    {
        isCastingSkill = true;
        TrySetAgentStopped(true);
        FaceTarget(target.transform.position);

        if (skillType == BossSkillType.LeapAttack)
        {
            yield return LeapAttack(target);
        }
        else
        {
            TryPlayFirstAvailableAnimation(poisonCastState, attackThrowState, attackUseItemState);
            PoisonBurst(target);
            yield return new WaitForSeconds(0.35f);
        }

        if (!Dead.Value)
        {
            TrySetAgentStopped(false);
        }

        isCastingSkill = false;
    }

    private IEnumerator LeapAttack(PlayerNetwork target)
    {
        if (target == null)
        {
            yield break;
        }

        Vector3 startPos = transform.position;
        Vector3 toTarget = target.transform.position - startPos;
        toTarget.y = 0f;

        Vector3 endPos = startPos + Vector3.ClampMagnitude(toTarget, leapDistance);
        if (NavMesh.SamplePosition(endPos, out NavMeshHit navHit, 2f, NavMesh.AllAreas))
        {
            endPos = navHit.position;
        }

        TryPlayFirstAvailableAnimation(leapStartState, "Jump_Start", "Jump_Idle");
        yield return new WaitForSeconds(0.1f);
        TryPlayFirstAvailableAnimation(leapLoopFallbackStates);

        Agent.enabled = false;
        float elapsed = 0f;
        while (elapsed < leapDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / leapDuration);
            float jumpArc = 2.2f * Mathf.Sin(Mathf.PI * t);
            Vector3 pos = Vector3.Lerp(startPos, endPos, t);
            pos.y += jumpArc;
            transform.position = pos;
            yield return null;
        }

        transform.position = endPos;
        Agent.enabled = true;
        if (Agent.isOnNavMesh)
        {
            Agent.Warp(endPos);
        }
        TryPlayFirstAvailableAnimation(leapLandState, "Jump_Land", "Jump_Idle");

        if (target != null && !target.IsDead)
        {
            float dist = Vector3.Distance(transform.position, target.transform.position);
            if (dist <= attackRange + 1f)
            {
                target.TakeDamageFromServer(leapDamage);
            }
        }
    }

    private void PoisonBurst(PlayerNetwork target)
    {
        if (target == null)
        {
            return;
        }

        PlayerNetwork[] players = FindObjectsByType<PlayerNetwork>(FindObjectsSortMode.None);
        foreach (PlayerNetwork player in players)
        {
            if (player == null || player.IsDead)
            {
                continue;
            }

            float dist = Vector3.Distance(transform.position, player.transform.position);
            if (dist <= poisonRadius)
            {
                player.TakeDamageFromServer(poisonDamage);
            }
        }
    }

    private void TryPlayFirstAvailableAnimation(params string[] candidates)
    {
        if (animator == null || candidates == null || candidates.Length == 0)
        {
            return;
        }

        foreach (string stateName in candidates)
        {
            if (string.IsNullOrWhiteSpace(stateName))
            {
                continue;
            }

            if (animator.HasState(0, Animator.StringToHash(stateName)))
            {
                TryPlayAnimation(stateName);
                return;
            }
        }
    }
}
