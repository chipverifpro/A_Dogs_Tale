using UnityEngine;

/*
// Example: called when player lands a hit
public void ApplyHit(Enemy enemy, float baseDamage)
{
    var zones = enemy.GetComponent<HitZoneSystem>();
    float mult = zones ? zones.CurrentMultiplier : 1f;
    float finalDamage = baseDamage * mult;
    enemy.TakeDamage(finalDamage);
}
If you need the side for effects: zones.CurrentZone == HitZone.FlankLeft vs FlankRight.

// --------------------

You can move the character like this:
    cc.Move(directionVector * speed * Time.deltaTime);

*/

public class BattleAgent
{
    int HealthPoints;

    string AttackName;
    int AttackDamage;
    float AttackSpeed;

    string PowerAttackName;
    int PowerAttackDamage;
    float PowerAttackSpeed;

    string DodgeName;
    float DodgeSpeed;
}
