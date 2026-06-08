using UnityEngine;
using UnityEngine.EventSystems;

sealed class InteractionDialogTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
{
    private InteractionDialogUI owner;

    public string TooltipText { get; private set; }

    public void Initialize(InteractionDialogUI owner, string tooltipText)
    {
        this.owner = owner;
        TooltipText = tooltipText;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        owner?.ShowTooltip(this, eventData.position);
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        owner?.MoveTooltip(this, eventData.position);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        owner?.HideTooltip(this);
    }

    private void OnDisable()
    {
        owner?.HideTooltip(this);
    }
}
