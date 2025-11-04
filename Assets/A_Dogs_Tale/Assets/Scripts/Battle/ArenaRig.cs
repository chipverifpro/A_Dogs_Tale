using UnityEngine;

[ExecuteAlways]
public class ArenaRig : MonoBehaviour
{
    [Header("Arena Geometry")]
    public Transform center;                  // Usually your enemy root or a child at feet height
    [Min(0.1f)] public float orbitRadius = 4f;
    [Min(0f)]  public float minRadius   = 3.2f;
    [Min(0.1f)] public float maxRadius  = 5.8f;

    [Header("Gizmos")]
    public bool drawGizmos = true;
    public Color ringColor = new Color(1f, 1f, 1f, 0.35f);

    public Vector3 CenterPos => center ? center.position : transform.position;

    private void OnDrawGizmos()
    {
        if (!drawGizmos) return;
        var c = CenterPos;
        DrawCircle(c, orbitRadius, ringColor);
        DrawCircle(c, minRadius, new Color(0.7f, 0.7f, 0.7f, 0.25f));
        DrawCircle(c, maxRadius, new Color(0.7f, 0.7f, 0.7f, 0.25f));
    }

    private void DrawCircle(Vector3 pos, float r, Color col, int segs=96)
    {
        Gizmos.color = col;
        Vector3 prev = pos + new Vector3(r, 0f, 0f);
        for (int i=1;i<=segs;i++)
        {
            float t = (i/(float)segs) * Mathf.PI * 2f;
            Vector3 next = pos + new Vector3(Mathf.Cos(t)*r, 0f, Mathf.Sin(t)*r);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
}