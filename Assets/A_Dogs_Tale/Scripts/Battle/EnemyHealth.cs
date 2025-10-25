using UnityEngine;

public class EnemyHealth : MonoBehaviour, IHitReceiver
{
    public float maxHP = 100f;
    public float currentHP = 100f;

    public void ReceiveHit(float damage)
    {
        currentHP = Mathf.Max(0f, currentHP - damage);
        Debug.Log($"Enemy took {damage:0.##}, HP now {currentHP:0.##}");
        if (currentHP <= 0f) Die();
    }

    void Die()
    {
        Debug.Log("Enemy defeated!");
        // TODO: play death anim, drop loot, notify state machine, etc.
    }
}