using UnityEngine;

public class PlayerDefenseResolver : MonoBehaviour, IHitReceiver
{
    public CombatActor actor;   // assign your player’s CombatActor
    public float parryStunDuration = 0.75f;

    // Hook to your player health system if you have one
    public float hpMax = 100f;
    public float hp = 100f;

    void Reset()
    {
        actor = GetComponent<CombatActor>();
    }

    /// Call this from the enemy's hitbox when a strike lands.
    public void ResolveIncomingHit(HitIntent hit, IStunnable attackerStunnable = null)
    {
        float now = Time.time;
        Debug.Log("Resolve Incoming Hit");
        if (actor && actor.IsBlocking)
        {
            // Check perfect window
            bool inParry = hit.canBeParried && (now - actor.BlockStartTime) <= actor.parryWindow;

            if (inParry)
            {
                actor.onParry?.Invoke();
                actor.RefundStamina(actor.parryStaminaRefund);
                // Optional: briefly freeze player/enemy, play parry SFX/VFX
                attackerStunnable?.Stun(parryStunDuration);
                return; // no damage on parry
            }
            else
            {
                // Normal block: chip damage + stamina cost
                float chip = hit.damage * Mathf.Clamp01(actor.blockChipFactor);
                ApplyDamage(chip);
                actor.onSuccessfulBlock?.Invoke();
                actor.SpendStamina(actor.blockStaminaCost);
                return;
            }
        }

        // Not blocking: full damage
        ApplyDamage(hit.damage);
    }

    public void ReceiveHit(float damage)
    {
        Debug.Log("Receive Hit");
        // Legacy IHitReceiver support if something calls straight damage
        ApplyDamage(damage);
    }

    void ApplyDamage(float dmg)
    {
        Debug.Log("Apply Damage");
        hp = Mathf.Max(0f, hp - Mathf.Max(0f, dmg));
        if (hp <= 0f)
        {
            Debug.Log("Player down!");
            // TODO: death/KO flow
        }
    }
}