using UnityEngine;
using UnityEngine.Events;

public class CombatActor : MonoBehaviour
{
    [Header("Attack")]
    public float baseDamage = 10f;
    public float attackCooldown = 0.35f;

    [Header("Block")]
    public float blockDuration = 0.35f;
    public float blockCooldown = 0.6f;
    public float blockChipFactor = 0.2f;    // fraction of damage taken while blocking (0.2 = 80% reduced)

    [Header("Parry")]
    public float parryWindow = 0.12f;       // seconds after block starts = “perfect block”
    public float parryStaminaRefund = 10f;  // optional: stamina refund on parry

    [Header("Resources (optional)")]
    public float staminaMax = 100f;
    public float stamina = 100f;
    public float blockStaminaCost = 8f;     // cost applied on normal block (not on parry)

    [Header("Events")]
    public UnityEvent onAttack;            // for VFX/SFX
    public UnityEvent onBlockStart;
    public UnityEvent onBlockEnd;
    public UnityEvent<float> onDealDamage; // passes finalDamage
    public UnityEvent onParry;
    public UnityEvent onSuccessfulBlock;    // non-parry block
    public UnityEvent onBlockBreak;         // if stamina depleted

    public bool IsBlocking { get; private set; }
    public bool IsAttacking { get; private set; }
    public float BlockStartTime { get; private set; } = -999f;

    float lastAttackTime = -999f;
    float lastBlockTime = -999f;


    public bool TryBlock()
    {
        Debug.Log("Try Block");
        if (Time.time < lastBlockTime + blockCooldown || IsBlocking) return false;
        IsBlocking = true;
        lastBlockTime = Time.time;
        onBlockStart?.Invoke();
        // Auto end after duration
        StopAllCoroutines();
        StartCoroutine(BlockWindow());
        return true;
    }

    System.Collections.IEnumerator BlockWindow()
    {
        yield return new WaitForSeconds(blockDuration);
        IsBlocking = false;
        onBlockEnd?.Invoke();
    }

    public bool TryAttack(IHitReceiver target, float zoneMultiplier = 1f)
    {
        Debug.Log("Try Attack");
        if (Time.time < lastAttackTime + attackCooldown || IsAttacking) return false;
        lastAttackTime = Time.time;
        IsAttacking = true;

        float finalDamage = baseDamage * Mathf.Max(0f, zoneMultiplier);
        onAttack?.Invoke();
        onDealDamage?.Invoke(finalDamage);
        target?.ReceiveHit(finalDamage);

        // very short “busy” to avoid double-fires in one frame
        StopAllCoroutines();
        StartCoroutine(AttackBusy(0.05f));
        return true;
    }

    System.Collections.IEnumerator AttackBusy(float t)
    {
        yield return new WaitForSeconds(t);
        IsAttacking = false;
    }


    // Optional helpers
    public void SpendStamina(float amt)
    {
        stamina = Mathf.Max(0f, stamina - Mathf.Max(0f, amt));
        if (stamina <= 0f) onBlockBreak?.Invoke();
    }

    public void RefundStamina(float amt)
    {
        stamina = Mathf.Min(staminaMax, stamina + Mathf.Max(0f, amt));
    }
}

public struct HitIntent
{
    public float damage;
    public bool canBeParried;
    public UnityEngine.Transform attacker;  // for VFX direction, camera shakes, etc.

    public HitIntent(float damage, bool canBeParried, UnityEngine.Transform attacker)
    {
        this.damage = damage;
        this.canBeParried = canBeParried;
        this.attacker = attacker;
    }
}

public interface IHitReceiver
{
    void ReceiveHit(float damage);
}

public interface IStunnable
{
    void Stun(float duration);
}

