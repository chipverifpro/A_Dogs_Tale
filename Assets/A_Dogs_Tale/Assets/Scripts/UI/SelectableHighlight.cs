using UnityEngine;

[DisallowMultipleComponent]
public class SelectableHighlight : MonoBehaviour
{
    [SerializeField] private Color defaultHighlightColor = Color.yellow;
    [SerializeField] private float emissionBoost = 2.0f;

    private Renderer[] renderers;
    private MaterialPropertyBlock propertyBlock;
    private bool isHighlighted;

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();
        propertyBlock = new MaterialPropertyBlock();
    }

    public void SetHighlighted(bool highlighted, Color? colorOverride = null)
    {
        if (renderers == null || renderers.Length == 0)
            return;

        if (isHighlighted == highlighted)
            return;

        isHighlighted = highlighted;
        Color hColor = colorOverride ?? defaultHighlightColor;

        foreach (var r in renderers)
        {
            if (r == null) continue;
            var mat = r.sharedMaterial;
            if (mat == null) continue;

            r.GetPropertyBlock(propertyBlock);

            if (highlighted)
            {
                // Try several common color properties, depending on shader:
                // Standard / built-in: _Color, _EmissionColor
                // URP Lit: _BaseColor, _EmissionColor
                if (mat.HasProperty("_BaseColor"))
                    propertyBlock.SetColor("_BaseColor", hColor);
                if (mat.HasProperty("_Color"))
                    propertyBlock.SetColor("_Color", hColor);

                // Emission is often the most visible "highlight"
                if (mat.HasProperty("_EmissionColor"))
                {
                    Color em = hColor * emissionBoost;
                    propertyBlock.SetColor("_EmissionColor", em);
                    // We don't need to toggle keywords when using a property block
                }
            }
            else
            {
                // Remove overrides -> revert to material defaults
                propertyBlock.Clear();
            }

            r.SetPropertyBlock(propertyBlock);
        }
    }
}