using UnityEngine;
using System;

public enum HitZone { Front, FlankLeft, FlankRight, Rear }

public class HitZoneSystem : MonoBehaviour
{
    [Header("Refs")]
    public Transform player;

    [Header("Zone Angles (Degrees)")]
    [Range(10f, 170f)] public float frontHalfAngle = 60f; // ± angle from enemy forward
    [Range(10f, 170f)] public float rearHalfAngle  = 60f; // ± angle around enemy back

    [Header("Damage Multipliers")]
    public float frontMult     = 0.75f;
    public float flankMult     = 1.0f;
    public float rearMult      = 1.5f;

    [Header("Gizmos")]
    public bool drawGizmos = true;
    public float gizmoRadius = 3.25f;
    public Color frontCol = new Color(1f, 0.4f, 0.4f, 0.25f);
    public Color flankCol = new Color(1f, 0.85f, 0.4f, 0.25f);
    public Color rearCol  = new Color(0.4f, 1f, 0.4f, 0.25f);

    public HitZone CurrentZone { get; private set; }
    public float   CurrentMultiplier => GetMultiplier(CurrentZone);

    void Update()
    {
        if (!player) return;
        CurrentZone = EvaluateZone(player.position);
    }

    public HitZone EvaluateZone(Vector3 targetPos)
    {
        Vector3 fwd = transform.forward; fwd.y = 0f; fwd.Normalize();
        Vector3 toP = (targetPos - transform.position); toP.y = 0f;
        if (toP.sqrMagnitude < 0.0001f) return HitZone.Front;

        Vector3 dir = toP.normalized;

        // Signed angle: +left, -right (Unity's left-handed around up)
        float signed = Vector3.SignedAngle(fwd, dir, Vector3.up);
        float absA   = Mathf.Abs(signed);

        // Front cone
        if (absA <= frontHalfAngle) return HitZone.Front;

        // Rear cone
        if (absA >= (180f - rearHalfAngle)) return HitZone.Rear;

        // Flanks: sign tells side
        return signed > 0f ? HitZone.FlankLeft : HitZone.FlankRight;
    }

    public float GetMultiplier(HitZone z)
    {
        switch (z)
        {
            case HitZone.Front:      return frontMult;
            case HitZone.Rear:       return rearMult;
            case HitZone.FlankLeft:
            case HitZone.FlankRight: return flankMult;
        }
        return 1f;
    }

    void OnDrawGizmos()
    {
        if (!drawGizmos) return;

        // Draw sectors: front, left flank, right flank, rear
        Vector3 c = transform.position;
        DrawSector(c, transform.forward,  frontHalfAngle, frontCol);
        DrawSector(c, -transform.forward, rearHalfAngle,  rearCol);

        // Flanks are what's left between front+rear — show as wide bands
        DrawFlank(c, transform.forward, frontHalfAngle, rearHalfAngle, +1, flankCol);
        DrawFlank(c, transform.forward, frontHalfAngle, rearHalfAngle, -1, flankCol);
    }

    void DrawSector(Vector3 center, Vector3 dir, float halfAngle, Color col, int segs=48)
    {
        Gizmos.color = col;
        Vector3 right = Quaternion.AngleAxis(+halfAngle, Vector3.up) * dir;
        Vector3 left  = Quaternion.AngleAxis(-halfAngle, Vector3.up) * dir;

        // fan
        Vector3 prev = center + right.normalized * gizmoRadius;
        for (int i=1;i<=segs;i++)
        {
            float t = Mathf.Lerp(+halfAngle, -halfAngle, i/(float)segs);
            Vector3 next = center + (Quaternion.AngleAxis(t, Vector3.up) * dir).normalized * gizmoRadius;
            Gizmos.DrawLine(prev, next);
            Gizmos.DrawLine(center, next);
            prev = next;
        }
    }

    void DrawFlank(Vector3 center, Vector3 fwd, float frontHalf, float rearHalf, int side, Color col, int segs=64)
    {
        // side: +1 left, -1 right
        Gizmos.color = col;
        float start = side * frontHalf;
        float end   = side * (180f - rearHalf);

        Vector3 prev = center + (Quaternion.AngleAxis(start, Vector3.up) * fwd).normalized * gizmoRadius;
        for (int i=1;i<=segs;i++)
        {
            float t = Mathf.Lerp(start, end, i/(float)segs);
            Vector3 next = center + (Quaternion.AngleAxis(t, Vector3.up) * fwd).normalized * gizmoRadius;
            Gizmos.DrawLine(prev, next);
            Gizmos.DrawLine(center, next);
            prev = next;
        }
    }
}