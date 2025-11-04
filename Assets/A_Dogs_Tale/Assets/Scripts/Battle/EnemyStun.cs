using UnityEngine;

public class EnemyStun : MonoBehaviour, IStunnable
{
    public bool IsStunned { get; private set; }
    float stunEnd;

    public void Stun(float duration)
    {
        if (duration <= 0f) return;
        Debug.Log("Stun");
        IsStunned = true;
        stunEnd = Time.time + duration;
        // TODO: play stun VFX/anim, disable attack AI, lower guard, etc.
    }

    void Update()
    {
        if (IsStunned && Time.time >= stunEnd)
        {
            IsStunned = false;
            // TODO: exit stun VFX/anim, re-enable AI
        }
    }
}