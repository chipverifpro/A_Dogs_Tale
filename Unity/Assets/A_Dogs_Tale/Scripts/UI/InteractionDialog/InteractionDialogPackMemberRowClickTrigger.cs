using UnityEngine;
using UnityEngine.EventSystems;

sealed class InteractionDialogPackMemberRowClickTrigger : MonoBehaviour, IPointerClickHandler
{
    private InteractionDialogUI owner;
    private int rowIndex;

    public void Initialize(InteractionDialogUI owner, int rowIndex)
    {
        this.owner = owner;
        this.rowIndex = rowIndex;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
            return;

        owner?.OnPackMemberListRowClicked(rowIndex);
    }
}
