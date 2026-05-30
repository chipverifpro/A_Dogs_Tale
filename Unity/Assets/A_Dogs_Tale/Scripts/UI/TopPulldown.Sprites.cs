using UnityEngine;
using UnityEngine.UI;

public partial class TopPulldown
{
    private Sprite GetPulldownFrameSprite()
    {
        bool useTwoRows = UseTwoRowTopControls();
        if (useTwoRows)
        {
            if (pulldownFrameTwoRowSprite == null)
                pulldownFrameTwoRowSprite = LoadPulldownFrameSprite(GetPulldownFrameTwoRowResourcePath());

            if (pulldownFrameTwoRowSprite != null)
                return pulldownFrameTwoRowSprite;
        }

        if (pulldownFrameSprite == null)
            pulldownFrameSprite = LoadPulldownFrameSprite(pulldownFrameResourcePath);

        return pulldownFrameSprite;
    }

    private string GetPulldownFrameTwoRowResourcePath()
    {
        if (string.IsNullOrWhiteSpace(pulldownFrameTwoRowResourcePath) ||
            pulldownFrameTwoRowResourcePath == LegacyTwoRowPulldownFrameResourcePath)
        {
            pulldownFrameTwoRowResourcePath = DefaultTwoRowPulldownFrameResourcePath;
        }

        return pulldownFrameTwoRowResourcePath;
    }

    private Sprite LoadPulldownFrameSprite(string resourcePath)
    {
        if (string.IsNullOrWhiteSpace(resourcePath))
            return null;

        Sprite sprite = SpriteServer.SpriteResourceLookup(resourcePath);
        if (sprite != null)
            return sprite;

        Debug.LogWarning($"TopPulldown: could not load pulldown frame sprite at Resources/{resourcePath}.", this);
        return null;
    }

    private Sprite GetBehaviorFrameSprite()
    {
        if (behaviorFrameSprite == null)
            behaviorFrameSprite = LoadPanelFrameSprite(behaviorFrameResourcePath);

        return behaviorFrameSprite;
    }

    private Sprite GetGaitFrameSprite()
    {
        if (gaitFrameSprite == null)
            gaitFrameSprite = LoadPanelFrameSprite(gaitFrameResourcePath, useTopHalf: true);

        return gaitFrameSprite;
    }

    private Sprite GetEmoteFrameSprite()
    {
        if (emoteFrameSprite == null)
            emoteFrameSprite = LoadPanelFrameSprite(emoteFrameResourcePath);

        return emoteFrameSprite;
    }

    private Sprite LoadPanelFrameSprite(string resourcePath, bool useTopHalf = false)
    {
        if (string.IsNullOrWhiteSpace(resourcePath))
            return null;

        Sprite sprite = SpriteServer.SpriteResourceLookup(resourcePath, useTopHalf, PanelSpritePixelsPerUnit);
        if (sprite != null)
            return sprite;

        Debug.LogWarning($"TopPulldown: could not load panel frame sprite at Resources/{resourcePath}.", this);
        return null;
    }

    private static bool ApplyPanelFrame(Image image, Sprite frameSprite)
    {
        if (image == null || frameSprite == null)
            return false;

        image.sprite = frameSprite;
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        image.color = Color.white;
        return true;
    }

    private Sprite GetPulldownTabSprite()
    {
        if (string.IsNullOrWhiteSpace(pulldownTabResourcePath))
            return null;

        Sprite sprite = SpriteServer.SpriteResourceLookup(pulldownTabResourcePath);
        if (sprite == null)
            Debug.LogWarning($"TopPulldown: could not load pulldown tab sprite at Resources/{pulldownTabResourcePath}.", this);

        return sprite;
    }
}
