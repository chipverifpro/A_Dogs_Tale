using UnityEngine;
using UnityEngine.EventSystems;

sealed class TopPulldownTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
{
    private TopPulldown owner;
    private System.Func<string> tooltipTextProvider;

    public void Initialize(TopPulldown owner, System.Func<string> tooltipTextProvider)
    {
        this.owner = owner;
        this.tooltipTextProvider = tooltipTextProvider;
    }

    public string GetTooltipText()
    {
        return tooltipTextProvider != null ? tooltipTextProvider() : null;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (owner != null)
            owner.ShowTooltip(this, eventData.position);
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        if (owner != null)
            owner.MoveTooltip(this, eventData.position);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (owner != null)
            owner.HideTooltip(this);
    }

    private void OnDisable()
    {
        if (owner != null)
            owner.HideTooltip(this);
    }
}
