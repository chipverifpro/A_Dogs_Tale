using UnityEngine;

public class EnemyFacePlayer : MonoBehaviour
{
    public Transform player;
    public float turnSpeedDegPerSec = 360f;

    void Update()
    {
        if (!player) return;
        Vector3 to = (player.position - transform.position);
        to.y = 0f;
        if (to.sqrMagnitude < 0.0001f) return;

        Quaternion target = Quaternion.LookRotation(to, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeedDegPerSec * Time.deltaTime);
    }
}