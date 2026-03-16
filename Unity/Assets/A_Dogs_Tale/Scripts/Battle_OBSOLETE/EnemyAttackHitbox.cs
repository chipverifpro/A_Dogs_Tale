using UnityEngine;

[RequireComponent(typeof(Collider))]
public class EnemyAttackHitbox : MonoBehaviour
{
    public float damage = 12f;
    public bool canBeParried = true;
    public float activeTime = 0.18f;        // how long this hitbox is “hot”
    public MonoBehaviour stunnableOwner;    // enemy component that implements IStunnable (optional)

    Collider col;
    float endTime;
    bool active;

    void Awake()
    {
        col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    // Call when the attack animation reaches the strike frame (via Animation Event)
    public void ActivateWindow()
    {
        active = true;
        endTime = Time.time + activeTime;
    }

    void Update()
    {
        if (active && Time.time >= endTime) active = false;
    }

    void OnTriggerEnter(Collider other)
    {
        Debug.Log("On Trigger Enter");
        if (!active) return;

        var resolver = other.GetComponent<PlayerDefenseResolver>();
        if (resolver)
        {
            IStunnable st = stunnableOwner as IStunnable;
            var intent = new HitIntent(damage, canBeParried, transform);
            resolver.ResolveIncomingHit(intent, st);
        }
    }
}