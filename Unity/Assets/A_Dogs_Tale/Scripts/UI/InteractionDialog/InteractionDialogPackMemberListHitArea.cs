using UnityEngine;
using UnityEngine.EventSystems;

sealed class InteractionDialogPackMemberListHitArea :
    MonoBehaviour,
    IPointerClickHandler,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler,
    IScrollHandler
{
    private InteractionDialogUI owner;
    private bool suppressNextClick;

    public void Initialize(InteractionDialogUI owner)
    {
        this.owner = owner;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData == null ||
            eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        if (suppressNextClick)
        {
            suppressNextClick = false;
            return;
        }

        owner?.SelectPackMemberListRowAtScreenPosition(eventData.position, eventData.pressEventCamera);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData == null)
            return;

        owner?.BeginPackMemberListDrag(eventData.position, eventData.pressEventCamera);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (eventData == null)
            return;

        suppressNextClick = true;
        owner?.DragPackMemberList(eventData.position, eventData.pressEventCamera);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
    }

    public void OnScroll(PointerEventData eventData)
    {
        if (eventData == null)
            return;

        owner?.ScrollPackMemberList(eventData.scrollDelta);
    }
}
