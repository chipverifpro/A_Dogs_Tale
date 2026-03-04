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


//IDEAS:
// Limit rotate speed, show rotation travel by rounded arrows
// Switch from straight target arrow to curved when power-up
// Combine functions into fewer classes.  Simplify.
// Show limits that move-in/out can be, and player's position within the range
// Effects for hit / miss
// Add keyboard controls
// Turn player in direction of travel, exposing side to enemy
// Show block
