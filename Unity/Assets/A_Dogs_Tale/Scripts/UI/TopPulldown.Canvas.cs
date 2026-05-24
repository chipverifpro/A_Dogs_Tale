using System.Collections.Generic;
using DogGame;
using DogGame.Modules;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.UI;

public partial class TopPulldown
{
    private Transform FindExistingScentTargetCanvas()
    {
        Transform localCanvas = FindDescendantByName(transform, CanvasName);
        if (localCanvas != null)
            return localCanvas;

        GameObject sceneCanvas = GameObject.Find(CanvasName);
        if (sceneCanvas != null)
            return sceneCanvas.transform;

        RectTransform[] rectTransforms = FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < rectTransforms.Length; i++)
        {
            RectTransform rectTransform = rectTransforms[i];
            if (rectTransform != null && rectTransform.name == CanvasName)
                return rectTransform;
        }

        return null;
    }

    private Transform EnsureSectionContainer(Transform canvasTransform, string containerName)
    {
        Transform sectionTransform = canvasTransform.Find(containerName);
        if (sectionTransform == null)
        {
            sectionTransform = FindDescendantByName(canvasTransform, containerName);
            if (sectionTransform != null && sectionTransform.parent != canvasTransform)
                sectionTransform.SetParent(canvasTransform, false);
        }

        if (sectionTransform == null)
        {
            GameObject sectionObject = new GameObject(containerName, typeof(RectTransform));
            sectionObject.transform.SetParent(canvasTransform, false);
            sectionTransform = sectionObject.transform;
        }

        RectTransform sectionRect = sectionTransform as RectTransform;
        if (sectionRect == null)
            sectionRect = GetOrAddComponent<RectTransform>(sectionTransform.gameObject);

        sectionRect.anchorMin = Vector2.zero;
        sectionRect.anchorMax = Vector2.one;
        sectionRect.offsetMin = Vector2.zero;
        sectionRect.offsetMax = Vector2.zero;
        sectionRect.pivot = new Vector2(0.5f, 0.5f);

        return sectionTransform;
    }

    private void ReparentExistingUiElement(Transform preferredParent, Transform searchRoot, string elementName)
    {
        Transform existing = FindDescendantByName(searchRoot, elementName);
        if (existing != null && existing.parent != preferredParent)
            existing.SetParent(preferredParent, false);
    }

    private Transform FindExistingUiElement(Transform preferredParent, Transform searchRoot, string elementName)
    {
        Transform existing = preferredParent.Find(elementName);
        if (existing != null)
            return existing;

        existing = FindDescendantByName(searchRoot, elementName);
        if (existing != null && existing.parent != preferredParent)
            existing.SetParent(preferredParent, false);

        return existing;
    }

    private Transform FindDescendantByName(Transform root, string childName)
    {
        if (root == null)
            return null;

        if (root.name == childName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDescendantByName(root.GetChild(i), childName);
            if (found != null)
                return found;
        }

        return null;
    }
}
