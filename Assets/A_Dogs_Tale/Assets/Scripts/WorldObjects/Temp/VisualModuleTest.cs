using UnityEngine;

public class VisualModuleTest : MonoBehaviour
{
    public WorldObject target;
    public Color highlightColor = Color.yellow;

    private bool _highlighted;

    void Update()
    {
        if (target == null) return;

        if (Input.GetKeyDown(KeyCode.H))
        {
            _highlighted = !_highlighted;
            if (_highlighted)
                target.Visual.SetTint(highlightColor);
            else
                target.Visual.ResetColor();
        }

        if (Input.GetKeyDown(KeyCode.V))
        {
            bool visible = !target.Visual.mainRenderer.enabled;
            target.Visual.SetVisible(visible);
        }
    }
}