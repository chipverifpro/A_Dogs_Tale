using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class CurvedArrowVisual : MonoBehaviour
{
    [Header("Refs")]
    public Transform head;                 // Quad for arrowhead (auto-created if null)

    [Header("Look & Feel")]
    public float yOffset = 0.03f;          // lift from ground to avoid z-fighting
    public int segments = 24;              // curve resolution (12–48 good)
    public float startWidth = 0.06f;       // shaft base width
    public float endWidth   = 0.012f;      // shaft tip width
    public float headLength = 0.35f;       // head size (world units)
    public float headWidth  = 0.25f;

    [Header("Curve")]
    public float minLengthToShow = 0.3f;   // ignore tiny drags
    public float maxLength = 10f;          // clamp arrow length
    public float baseCurveHeight = 0.35f;  // base vertical bow
    public float curvePerMeter   = 0.18f;  // extra bow per meter of length
    public float maxCurveHeight  = 2.0f;   // cap bow height

    [Header("Color")]
    public Gradient normalColor;           // line gradient when NOT aimed
    public Gradient aimedColor;            // line gradient when aimed

    LineRenderer lr;
    bool visible;

    void Awake()
    {
        lr = GetComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = 0;

        // Tapered width
        var widthCurve = new AnimationCurve();
        widthCurve.AddKey(0f, startWidth);
        widthCurve.AddKey(1f, endWidth);
        lr.widthCurve = widthCurve;

        if (!head)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "ArrowHead";
            go.transform.SetParent(transform, false);
            head = go.transform;
            var mr = go.GetComponent<MeshRenderer>();
            if (mr) mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            DestroyImmediate(go.GetComponent<Collider>());
        }

        Hide();
    }

    public void Hide()
    {
        if (!visible) return;
        visible = false;
        lr.enabled = false;
        lr.positionCount = 0;
        if (head) head.gameObject.SetActive(false);
    }

    /// Show/update the curved arrow.
    /// from/to are world positions on the ground plane (y already set to ground).
    public void Show(Vector3 from, Vector3 to, bool aimed)
    {
        // Lift a bit above ground
        var a = new Vector3(from.x, from.y + yOffset, from.z);
        var b = new Vector3(to.x,   to.y   + yOffset, to.z);

        // Clamp length
        Vector3 ab = b - a;
        float len = ab.magnitude;
        if (len < minLengthToShow)
        {
            Hide();
            return;
        }
        if (len > maxLength)
        {
            b = a + ab.normalized * maxLength;
            ab = b - a;
            len = ab.magnitude;
        }

        // Choose gradient
        lr.colorGradient = aimed ? aimedColor : normalColor;

        // Quadratic Bézier control point (midpoint raised by curve height)
        float h = Mathf.Clamp(baseCurveHeight + curvePerMeter * len, baseCurveHeight, maxCurveHeight);
        Vector3 mid = (a + b) * 0.5f + Vector3.up * h;

        // Build points
        if (segments < 2) segments = 2;
        lr.positionCount = segments + 1;
        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            // Quadratic Bezier: LERP(LERP(a,mid,t), LERP(mid,b,t), t)
            Vector3 p0 = Vector3.Lerp(a, mid, t);
            Vector3 p1 = Vector3.Lerp(mid, b, t);
            Vector3 p  = Vector3.Lerp(p0, p1, t);
            lr.SetPosition(i, p);
        }

        // Enable visuals
        if (!visible)
        {
            visible = true;
            lr.enabled = true;
            if (head) head.gameObject.SetActive(true);
        }

        // Arrowhead at tip, aligned with curve tangent
        Vector3 tip = lr.GetPosition(segments);
        Vector3 tipPrev = lr.GetPosition(segments - 1);
        Vector3 tangent = (tip - tipPrev);
        if (tangent.sqrMagnitude < 1e-6f) tangent = ab; // fallback

        if (head)
        {
            head.position = tip;
            // Make Quad face up and point along tangent
            head.rotation = Quaternion.LookRotation(tangent.normalized, Vector3.up) * Quaternion.Euler(90f, 0f, 0f);
            head.localScale = new Vector3(headWidth, headLength, 1f);
        }
    }
}