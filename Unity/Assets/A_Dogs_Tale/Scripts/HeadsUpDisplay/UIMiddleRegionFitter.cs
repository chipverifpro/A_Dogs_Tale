using UnityEngine;

namespace DogGame.UI
{
    /// <summary>
    /// Keeps a middle UI root centered between a top UI region and a bottom UI region.
    ///
    /// Intended hierarchy:
    ///
    /// UIRootCanvas
    /// ├── TopCenter
    /// │   └── PulldownPanel
    /// ├── BottomCenter
    /// │   └── BottomBanner
    /// └── Middle
    ///
    /// Attach this script to Middle.
    /// Assign Top Region to the actual pulldown panel or TopCenter root.
    /// Assign Bottom Region to the actual bottom banner panel or BottomCenter root.
    /// Assign Canvas Rect to the UIRootCanvas RectTransform.
    /// </summary>
    [ExecuteAlways]
    public sealed class UIMiddleRegionFitter : MonoBehaviour
    {
        [Header("Required References")]
        [SerializeField] private RectTransform canvasRect;
        [SerializeField] private RectTransform topRegion;
        [SerializeField] private RectTransform bottomRegion;

        [Header("Target")]
        [SerializeField] private RectTransform middleRegion;

        [Header("Padding")]
        [SerializeField] private float topPadding = 12f;
        [SerializeField] private float bottomPadding = 12f;

        [Header("Behavior")]
        [SerializeField] private bool resizeMiddleHeight = true;
        [SerializeField] private bool stretchMiddleWidth = true;
        [SerializeField] private float minimumMiddleHeight = 100f;

        private void Reset()
        {
            middleRegion = GetComponent<RectTransform>();

            Canvas parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas != null)
            {
                canvasRect = parentCanvas.GetComponent<RectTransform>();
            }
        }

        private void OnEnable()
        {
            ApplyLayout();
        }

        private void Update()
        {
            ApplyLayout();
        }

        private void OnRectTransformDimensionsChange()
        {
            ApplyLayout();
        }

        private void ApplyLayout()
        {
            if (canvasRect == null || topRegion == null || bottomRegion == null)
            {
                Debug.LogError("UIMiddleRegionFitter has unassigned references",this);
                return;
            }

            if (middleRegion == null)
            {
                middleRegion = GetComponent<RectTransform>();
                if (middleRegion == null)
                {
                    Debug.LogError("UIMiddleRegionFitter has unassigned reference middleRegion",this);
                    return;
                }
            }

            Vector3[] topCorners = new Vector3[4];
            Vector3[] bottomCorners = new Vector3[4];

            topRegion.GetWorldCorners(topCorners);
            bottomRegion.GetWorldCorners(bottomCorners);

            // Convert top/bottom region edges into canvas-local coordinates.
            float topRegionBottomY = WorldYToCanvasLocalY(topCorners[0].y);      // bottom-left corner
            float bottomRegionTopY = WorldYToCanvasLocalY(bottomCorners[1].y);  // top-left corner

            float availableTopY = topRegionBottomY - topPadding;
            float availableBottomY = bottomRegionTopY + bottomPadding;

            float availableHeight = availableTopY - availableBottomY;

            if (availableHeight < minimumMiddleHeight)
            {
                availableHeight = minimumMiddleHeight;
            }

            float centerY = (availableTopY + availableBottomY) * 0.5f;

            // Make Middle use the canvas center as its anchor reference.
            middleRegion.anchorMin = new Vector2(0.5f, 0.5f);
            middleRegion.anchorMax = new Vector2(0.5f, 0.5f);
            middleRegion.pivot = new Vector2(0.5f, 0.5f);

            Vector2 anchoredPosition = middleRegion.anchoredPosition;
            anchoredPosition.x = 0f;
            anchoredPosition.y = centerY;
            middleRegion.anchoredPosition = anchoredPosition;

            Vector2 sizeDelta = middleRegion.sizeDelta;

            if (stretchMiddleWidth)
            {
                sizeDelta.x = canvasRect.rect.width;
            }

            if (resizeMiddleHeight)
            {
                sizeDelta.y = availableHeight;
            }

            middleRegion.sizeDelta = sizeDelta;
        }

        private float WorldYToCanvasLocalY(float worldY)
        {
            Vector3 worldPoint = new Vector3(0f, worldY, 0f);
            Vector3 canvasLocalPoint = canvasRect.InverseTransformPoint(worldPoint);
            return canvasLocalPoint.y;
        }
    }
}