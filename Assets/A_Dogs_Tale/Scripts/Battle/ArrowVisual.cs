using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class ArrowVisual : MonoBehaviour
{
    [Header("Refs")]
    public Camera cam;                    // Main Camera (optional, used only if you extend to screen-space)
    public Transform head;                // A small Quad child as arrowhead (see notes)

    [Header("Look & Feel")]
    public float yOffset = 0.02f;         // lift off ground to avoid z-fighting
    public float shaftWidth = 0.04f;      // world units
    public float headLength = 0.35f;      // world units
    public float headWidth  = 0.25f;      // world units
    public float minLengthToShow = 0.2f;  // don’t show for super tiny drags
    public Gradient normalColor;          // color along the line
    public Gradient aimedColor;           // color when aimed toward enemy

    LineRenderer lr;
    bool visible;

    void Awake()
    {
        lr = GetComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = 2;
        lr.textureMode = LineTextureMode.Stretch;
        lr.widthMultiplier = shaftWidth;

        if (head == null)
        {
            // Create a simple head if missing
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
        if (head) head.gameObject.SetActive(false);
    }

    /// Show/update the arrow.
    /// from/to: world positions on ground plane
    /// aimed: true when within your attack aim cone (for color swap)
    public void Show(Vector3 from, Vector3 to, bool aimed)
    {
        Vector3 a = new Vector3(from.x, from.y + yOffset, from.z);
        Vector3 b = new Vector3(to.x,   to.y   + yOffset, to.z);

        Vector3 dir = (b - a);
        float len = dir.magnitude;
        if (len < minLengthToShow)
        {
            Hide();
            return;
        }

        if (!visible)
        {
            visible = true;
            lr.enabled = true;
            if (head) head.gameObject.SetActive(true);
        }

        lr.colorGradient = aimed ? aimedColor : normalColor;

        // Shaft endpoints: stop a bit before the head so the head sits on the tip cleanly
        Vector3 dirN = dir / len;
        Vector3 shaftEnd = b - dirN * headLength * 0.8f;

        lr.SetPosition(0, a);
        lr.SetPosition(1, shaftEnd);
        lr.widthMultiplier = shaftWidth;

        // Head transform
        if (head)
        {
            head.position = b;
            head.rotation = Quaternion.LookRotation(dirN, Vector3.up) * Quaternion.Euler(90, 0, 0); // make Quad face up
            head.localScale = new Vector3(headWidth, headLength, 1f);
        }
    }
}