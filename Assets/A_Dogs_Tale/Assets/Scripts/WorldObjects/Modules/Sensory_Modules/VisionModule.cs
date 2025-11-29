using UnityEngine;

public class VisionModule : WorldModule
{
    [Header("Vision Module")]
    [Tooltip("Primary renderer for this object. If left empty, will try GetComponentInChildren<Renderer>().")]
    public Renderer mainRenderer;

    [Tooltip("Optional extra renderers (LOD children, sub-meshes, etc.).")]
    public Renderer[] extraRenderers;

    private Color[] _originalColors;
    private bool _initializedColors;

    protected override void Awake()
    {
        base.Awake();

        // Auto-assign main renderer if not wired in Inspector
        if (mainRenderer == null)
        {
            mainRenderer = GetComponentInChildren<Renderer>();
            if (mainRenderer == null)
            {
                Debug.LogWarning($"{name}: VisualModule could not find a Renderer.", this);
                return;
            }
        }

        CacheOriginalColors();
    }

    private void CacheOriginalColors()
    {
        if (mainRenderer == null) return;

        // We only snapshot the mainRenderer's material color for now;
        // you can extend to per-material or per-extraRenderer if you need.
        _originalColors = new Color[1];
        _originalColors[0] = GetCurrentColor();
        _initializedColors = true;
    }

    private Color GetCurrentColor()
    {
        if (mainRenderer == null) return Color.white;

        // Use material.color (instance) so we don't clobber sharedMaterial
        if (mainRenderer.material.HasProperty("_Color"))
            return mainRenderer.material.color;

        return Color.white;
    }

    /// <summary>
    /// Show/hide this object's renderers.
    /// </summary>
    public void SetVisible(bool visible)
    {
        if (mainRenderer != null)
            mainRenderer.enabled = visible;

        if (extraRenderers != null)
        {
            for (int i = 0; i < extraRenderers.Length; i++)
            {
                if (extraRenderers[i] != null)
                    extraRenderers[i].enabled = visible;
            }
        }
    }

    /// <summary>
    /// Apply a tint color (multiplies onto the base color).
    /// Good for highlighting, sniff mode effects, etc.
    /// </summary>
    public void SetTint(Color tint)
    {
        if (mainRenderer == null) return;

        var mat = mainRenderer.material; // instance
        if (mat.HasProperty("_Color"))
        {
            Color baseColor = _initializedColors ? _originalColors[0] : mat.color;
            mat.color = baseColor * tint;
        }

        // Optional: apply to extra renderers if present
        if (extraRenderers != null)
        {
            for (int i = 0; i < extraRenderers.Length; i++)
            {
                var r = extraRenderers[i];
                if (r == null) continue;

                var m = r.material;
                if (m.HasProperty("_Color"))
                {
                    Color baseColor = m.color;
                    m.color = baseColor * tint;
                }
            }
        }
    }

    /// <summary>
    /// Restore original color.
    /// </summary>
    public void ResetColor()
    {
        if (mainRenderer == null || !_initializedColors) return;

        var mat = mainRenderer.material;
        if (mat.HasProperty("_Color"))
            mat.color = _originalColors[0];
    }

    /// <summary>
    /// Convenience: set unity layer for all renderers on this object.
    /// (Separate from Rendering Layer Mask.)
    /// </summary>
    public void SetUnityLayer(int layer)
    {
        gameObject.layer = layer;

        if (mainRenderer != null)
            mainRenderer.gameObject.layer = layer;

        if (extraRenderers != null)
        {
            for (int i = 0; i < extraRenderers.Length; i++)
            {
                if (extraRenderers[i] != null)
                    extraRenderers[i].gameObject.layer = layer;
            }
        }
    }
}