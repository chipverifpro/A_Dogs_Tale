using UnityEngine;
using UnityEngine.EventSystems;

sealed class InteractionDialogInputBlocker :
    MonoBehaviour,
    IPointerDownHandler,
    IPointerClickHandler,
    IBeginDragHandler,
    IDragHandler,
    IScrollHandler
{
    public void OnPointerDown(PointerEventData eventData)
    {
    }

    public void OnPointerClick(PointerEventData eventData)
    {
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
    }

    public void OnDrag(PointerEventData eventData)
    {
    }

    public void OnScroll(PointerEventData eventData)
    {
    }
}
