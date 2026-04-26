using System;
using System.Collections.Generic;
using DogGame.Modules;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.UI;

public sealed class InventoryDialogUI : MonoBehaviour
{
    private static readonly Vector3 PreviewAnchorPosition = new(60000f, 60000f, 60000f);

    [Header("Resources")]
    [SerializeField] private string arrowsSpriteResourcePath = "Sprites/ArrowsSpriteSheetA";
    [SerializeField] private string inventoryActionsSpriteResourcePath = "Sprites/InventoryActionsSheetA";

    [Header("Layout")]
    [SerializeField] private int uiSortOrder = 5300;
    [SerializeField] private Vector2 referenceResolution = new(1920f, 1080f);
    [SerializeField] private Vector2 dialogSize = new(760f, 760f);
    [SerializeField] private float actionButtonHeight = 82f;
    [SerializeField] private float previewSpinDegreesPerSecond = 24f;

    private readonly Dictionary<int, Sprite> arrowSprites = new();
    private readonly Dictionary<int, Sprite> actionSprites = new();
    private readonly List<Button> actionButtons = new();

    private Canvas overlayCanvas;
    private RectTransform dialogRect;
    private GameObject dialogRoot;
    private RawImage previewImage;
    private TextMeshProUGUI itemNameLabel;
    private Button leftArrowButton;
    private Button rightArrowButton;

    private RenderTexture previewTexture;
    private GameObject previewWorldRoot;
    private GameObject previewClone;
    private Camera previewCamera;
    private Light previewLight;
    private float framingRadius = 1f;

    private int selectedIndex;
    private WorldObject previewedItem;
    private ContainerModule displayedContainer;
    private bool isOpen;

    private enum InventoryAction
    {
        Use = 0,
        Eat = 1,
        Give = 2,
        Trade = 3,
        Drop = 4,
        PickUp = 5
    }

    private void Awake()
    {
        BuildUI();
        Hide();
    }

    private void Update()
    {
        if (WasInventoryTogglePressedThisFrame())
            Toggle();

        if (!isOpen)
            return;

        RefreshInventoryView();
        SpinPreview();
    }

    private void OnDestroy()
    {
        DestroyPreviewClone();
        DestroyPreviewWorld();
        ReleasePreviewTexture();
    }

    public void Toggle()
    {
        if (isOpen)
            Hide();
        else
            Show();
    }

    public void Show()
    {
        EnsureEventSystem();
        isOpen = true;
        dialogRoot.SetActive(true);
        RefreshInventoryView(forcePreviewRefresh: true);
    }

    public void Hide()
    {
        isOpen = false;
        selectedIndex = 0;
        previewedItem = null;
        displayedContainer = null;

        if (dialogRoot != null)
            dialogRoot.SetActive(false);

        DestroyPreviewClone();
    }

    private bool WasInventoryTogglePressedThisFrame()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.iKey.wasPressedThisFrame)
            return false;

        GameObject selectedObject = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (selectedObject != null &&
            (selectedObject.GetComponent<TMP_InputField>() != null || selectedObject.GetComponent<InputField>() != null))
        {
            return false;
        }

        return true;
    }

    private void BuildUI()
    {
        LoadSprites();
        EnsureEventSystem();

        GameObject canvasObject = new("InventoryDialogCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        overlayCanvas = canvasObject.GetComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.overrideSorting = true;
        overlayCanvas.sortingOrder = uiSortOrder;

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.anchorMin = Vector2.zero;
        canvasRect.anchorMax = Vector2.one;
        canvasRect.offsetMin = Vector2.zero;
        canvasRect.offsetMax = Vector2.zero;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.matchWidthOrHeight = 1f;

        dialogRoot = CreateUIObject("InventoryDialog", canvasObject.transform);
        dialogRect = dialogRoot.GetComponent<RectTransform>();
        dialogRect.anchorMin = new Vector2(0.5f, 0.5f);
        dialogRect.anchorMax = new Vector2(0.5f, 0.5f);
        dialogRect.pivot = new Vector2(0.5f, 0.5f);
        dialogRect.anchoredPosition = Vector2.zero;
        dialogRect.sizeDelta = dialogSize;

        Image dialogImage = dialogRoot.AddComponent<Image>();
        dialogImage.color = new Color(0.08f, 0.075f, 0.055f, 0.94f);

        BuildHeader(dialogRoot.transform);
        BuildPreviewArea(dialogRoot.transform);
        BuildActionButtons(dialogRoot.transform);
        EnsurePreviewWorld();
    }

    private void BuildHeader(Transform parent)
    {
        GameObject titleObject = CreateUIObject("Title", parent);
        RectTransform titleRect = titleObject.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.offsetMin = new Vector2(32f, -72f);
        titleRect.offsetMax = new Vector2(-96f, -18f);

        TextMeshProUGUI titleLabel = titleObject.AddComponent<TextMeshProUGUI>();
        titleLabel.text = "Inventory";
        titleLabel.fontSize = 34f;
        titleLabel.color = new Color(0.98f, 0.93f, 0.78f, 1f);
        titleLabel.alignment = TextAlignmentOptions.MidlineLeft;

        Button closeButton = CreateSpriteButton(
            "CloseButton",
            parent,
            arrowSprites.TryGetValue(5, out Sprite closeSprite) ? closeSprite : null,
            "X",
            OnCloseClicked);

        RectTransform closeRect = closeButton.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(1f, 1f);
        closeRect.anchorMax = new Vector2(1f, 1f);
        closeRect.pivot = new Vector2(1f, 1f);
        closeRect.anchoredPosition = new Vector2(-20f, -18f);
        closeRect.sizeDelta = new Vector2(54f, 54f);
    }

    private void BuildPreviewArea(Transform parent)
    {
        GameObject previewPanel = CreateUIObject("PreviewPanel", parent);
        RectTransform previewPanelRect = previewPanel.GetComponent<RectTransform>();
        previewPanelRect.anchorMin = new Vector2(0f, 0.42f);
        previewPanelRect.anchorMax = new Vector2(1f, 0.9f);
        previewPanelRect.offsetMin = new Vector2(44f, 18f);
        previewPanelRect.offsetMax = new Vector2(-44f, -12f);

        Image previewBackground = previewPanel.AddComponent<Image>();
        previewBackground.color = new Color(0.97f, 0.91f, 0.74f, 0.12f);

        GameObject rawImageObject = CreateUIObject("ItemPreview", previewPanel.transform);
        RectTransform rawImageRect = rawImageObject.GetComponent<RectTransform>();
        rawImageRect.anchorMin = new Vector2(0.18f, 0.17f);
        rawImageRect.anchorMax = new Vector2(0.82f, 0.92f);
        rawImageRect.offsetMin = Vector2.zero;
        rawImageRect.offsetMax = Vector2.zero;

        previewImage = rawImageObject.AddComponent<RawImage>();
        previewImage.raycastTarget = false;
        previewImage.color = Color.white;

        GameObject labelObject = CreateUIObject("ItemName", previewPanel.transform);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(1f, 0f);
        labelRect.pivot = new Vector2(0.5f, 0f);
        labelRect.offsetMin = new Vector2(24f, 16f);
        labelRect.offsetMax = new Vector2(-24f, 58f);

        itemNameLabel = labelObject.AddComponent<TextMeshProUGUI>();
        itemNameLabel.fontSize = 28f;
        itemNameLabel.color = new Color(0.98f, 0.93f, 0.78f, 1f);
        itemNameLabel.alignment = TextAlignmentOptions.Center;

        leftArrowButton = CreateSpriteButton(
            "PreviousItemButton",
            previewPanel.transform,
            arrowSprites.TryGetValue(0, out Sprite leftSprite) ? leftSprite : null,
            "<",
            OnPreviousItemClicked);

        RectTransform leftRect = leftArrowButton.GetComponent<RectTransform>();
        leftRect.anchorMin = new Vector2(0f, 0.5f);
        leftRect.anchorMax = new Vector2(0f, 0.5f);
        leftRect.pivot = new Vector2(0f, 0.5f);
        leftRect.anchoredPosition = new Vector2(22f, 18f);
        leftRect.sizeDelta = new Vector2(82f, 82f);

        rightArrowButton = CreateSpriteButton(
            "NextItemButton",
            previewPanel.transform,
            arrowSprites.TryGetValue(1, out Sprite rightSprite) ? rightSprite : null,
            ">",
            OnNextItemClicked);

        RectTransform rightRect = rightArrowButton.GetComponent<RectTransform>();
        rightRect.anchorMin = new Vector2(1f, 0.5f);
        rightRect.anchorMax = new Vector2(1f, 0.5f);
        rightRect.pivot = new Vector2(1f, 0.5f);
        rightRect.anchoredPosition = new Vector2(-22f, 18f);
        rightRect.sizeDelta = new Vector2(82f, 82f);
    }

    private void BuildActionButtons(Transform parent)
    {
        GameObject actionPanel = CreateUIObject("ActionPanel", parent);
        RectTransform actionPanelRect = actionPanel.GetComponent<RectTransform>();
        actionPanelRect.anchorMin = new Vector2(0f, 0f);
        actionPanelRect.anchorMax = new Vector2(1f, 0.36f);
        actionPanelRect.offsetMin = new Vector2(44f, 34f);
        actionPanelRect.offsetMax = new Vector2(-44f, -20f);

        Image actionBackground = actionPanel.AddComponent<Image>();
        actionBackground.color = new Color(0.02f, 0.018f, 0.014f, 0.18f);

        HorizontalLayoutGroup layout = actionPanel.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.spacing = 14f;
        layout.padding = new RectOffset(16, 16, 16, 16);

        CreateActionButton(actionPanel.transform, InventoryAction.Use, OnUseClicked);
        CreateActionButton(actionPanel.transform, InventoryAction.Eat, OnEatClicked);
        CreateActionButton(actionPanel.transform, InventoryAction.Give, OnGiveClicked);
        CreateActionButton(actionPanel.transform, InventoryAction.Trade, OnTradeClicked);
        CreateActionButton(actionPanel.transform, InventoryAction.Drop, OnDropClicked);
        CreateActionButton(actionPanel.transform, InventoryAction.PickUp, OnPickUpClicked);
    }

    private void CreateActionButton(Transform parent, InventoryAction action, UnityEngine.Events.UnityAction clickHandler)
    {
        int index = (int)action;
        Sprite sprite = actionSprites.TryGetValue(index, out Sprite foundSprite) ? foundSprite : null;
        Button button = CreateSpriteButton($"{action}Button", parent, sprite, GetActionFallbackText(action), clickHandler);

        float width = actionButtonHeight;
        if (sprite != null && sprite.rect.height > 0f)
            width = actionButtonHeight * (sprite.rect.width / sprite.rect.height);

        RectTransform rect = button.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(width, actionButtonHeight);

        LayoutElement layoutElement = button.gameObject.AddComponent<LayoutElement>();
        layoutElement.preferredWidth = width;
        layoutElement.preferredHeight = actionButtonHeight;
        layoutElement.minWidth = width;
        layoutElement.minHeight = actionButtonHeight;

        actionButtons.Add(button);
    }

    private Button CreateSpriteButton(
        string objectName,
        Transform parent,
        Sprite sprite,
        string fallbackText,
        UnityEngine.Events.UnityAction clickHandler)
    {
        GameObject buttonObject = CreateUIObject(objectName, parent);
        Image image = buttonObject.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.color = sprite != null
            ? Color.white
            : new Color(0.88f, 0.78f, 0.5f, 0.86f);

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(clickHandler);

        if (sprite == null)
            AddFallbackButtonText(buttonObject.transform, fallbackText);

        return button;
    }

    private void AddFallbackButtonText(Transform parent, string text)
    {
        GameObject labelObject = CreateUIObject("FallbackLabel", parent);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = 20f;
        label.color = Color.black;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
    }

    private void RefreshInventoryView(bool forcePreviewRefresh = false)
    {
        ContainerModule container = GetCurrentContainer();
        if (container != displayedContainer)
        {
            displayedContainer = container;
            selectedIndex = 0;
            forcePreviewRefresh = true;
        }

        int itemCount = container != null ? container.HeldItemCount : 0;
        if (itemCount <= 0)
        {
            SetNoHeldItemState();
            return;
        }

        selectedIndex = Mathf.Clamp(selectedIndex, 0, itemCount - 1);
        WorldObject item = container.HeldItems[selectedIndex];

        itemNameLabel.text = item != null ? item.DisplayName : "No item held";
        bool hasMultipleItems = itemCount > 1;
        leftArrowButton.gameObject.SetActive(hasMultipleItems);
        rightArrowButton.gameObject.SetActive(hasMultipleItems);

        if (forcePreviewRefresh || item != previewedItem)
            BuildPreviewClone(item);
    }

    private void SetNoHeldItemState()
    {
        itemNameLabel.text = "No item held";
        leftArrowButton.gameObject.SetActive(false);
        rightArrowButton.gameObject.SetActive(false);

        if (previewedItem != null || previewClone != null)
        {
            previewedItem = null;
            DestroyPreviewClone();
        }
    }

    private ContainerModule GetCurrentContainer()
    {
        Dir dir = Dir.Instance;
        WorldObject controlledObject = dir != null && dir.playerPack != null ? dir.playerPack.packLeader : null;
        if (controlledObject == null)
            return null;

        if (controlledObject.containerModule == null)
            controlledObject.CreateModulesIfNeeded(ModuleFlags.containerModule);

        return controlledObject.containerModule;
    }

    private void OnPreviousItemClicked()
    {
        int itemCount = displayedContainer != null ? displayedContainer.HeldItemCount : 0;
        if (itemCount <= 1)
            return;

        selectedIndex = (selectedIndex - 1 + itemCount) % itemCount;
        RefreshInventoryView(forcePreviewRefresh: true);
    }

    private void OnNextItemClicked()
    {
        int itemCount = displayedContainer != null ? displayedContainer.HeldItemCount : 0;
        if (itemCount <= 1)
            return;

        selectedIndex = (selectedIndex + 1) % itemCount;
        RefreshInventoryView(forcePreviewRefresh: true);
    }

    private void OnCloseClicked()
    {
        Hide();
    }

    private void OnUseClicked()
    {
    }

    private void OnEatClicked()
    {
    }

    private void OnGiveClicked()
    {
    }

    private void OnTradeClicked()
    {
    }

    private void OnDropClicked()
    {
    }

    private void OnPickUpClicked()
    {
    }

    private void LoadSprites()
    {
        LoadSpriteSheet(arrowsSpriteResourcePath, arrowSprites);
        LoadSpriteSheet(inventoryActionsSpriteResourcePath, actionSprites);
    }

    private void LoadSpriteSheet(string resourcePath, Dictionary<int, Sprite> lookup)
    {
        lookup.Clear();

        Sprite[] sprites = Resources.LoadAll<Sprite>(NormalizeResourcePath(resourcePath));
        for (int i = 0; i < sprites.Length; i++)
        {
            Sprite sprite = sprites[i];
            int index = GetSpriteSheetIndex(sprite.name);
            if (index >= 0)
                lookup[index] = sprite;
        }
    }

    private void EnsurePreviewWorld()
    {
        if (previewWorldRoot != null)
            return;

        previewWorldRoot = new GameObject("InventoryDialogPreviewWorld");
        previewWorldRoot.hideFlags = HideFlags.HideAndDontSave;
        previewWorldRoot.transform.position = PreviewAnchorPosition;

        GameObject cameraObject = new("PreviewCamera");
        cameraObject.hideFlags = HideFlags.HideAndDontSave;
        cameraObject.transform.SetParent(previewWorldRoot.transform, false);
        previewCamera = cameraObject.AddComponent<Camera>();
        previewCamera.enabled = false;
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        previewCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        previewCamera.orthographic = true;
        previewCamera.nearClipPlane = 0.01f;
        previewCamera.farClipPlane = 100f;

        GameObject lightObject = new("PreviewLight");
        lightObject.hideFlags = HideFlags.HideAndDontSave;
        lightObject.transform.SetParent(previewWorldRoot.transform, false);
        previewLight = lightObject.AddComponent<Light>();
        previewLight.type = LightType.Directional;
        previewLight.intensity = 1.25f;
        previewLight.color = Color.white;
        previewLight.shadows = LightShadows.None;
        previewLight.transform.rotation = Quaternion.Euler(35f, 135f, 0f);

        EnsurePreviewTexture();
    }

    private void EnsurePreviewTexture()
    {
        if (previewTexture != null)
            return;

        previewTexture = new RenderTexture(768, 768, 16, RenderTextureFormat.ARGB32);
        previewTexture.name = "InventoryDialogPreviewRT";
        previewTexture.Create();

        previewImage.texture = previewTexture;
        previewCamera.targetTexture = previewTexture;
    }

    private void BuildPreviewClone(WorldObject item)
    {
        DestroyPreviewClone();
        previewedItem = item;

        if (item == null)
            return;

        EnsurePreviewWorld();
        previewClone = CreateVisualClone(item.gameObject);
        previewClone.name = $"{item.name}_InventoryPreview";
        previewClone.hideFlags = HideFlags.HideAndDontSave;
        previewClone.transform.SetParent(previewWorldRoot.transform, false);
        previewClone.transform.position = PreviewAnchorPosition;

        CenterPreviewClone(previewClone);
        RenderPreview();
    }

    private void CenterPreviewClone(GameObject clone)
    {
        Renderer[] renderers = clone.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            framingRadius = 1f;
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        clone.transform.position += PreviewAnchorPosition - bounds.center;

        Bounds centeredBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            centeredBounds.Encapsulate(renderers[i].bounds);

        framingRadius = Mathf.Max(centeredBounds.extents.y, Mathf.Max(centeredBounds.extents.x, centeredBounds.extents.z));
        if (framingRadius < 0.1f)
            framingRadius = 0.5f;
    }

    private void SpinPreview()
    {
        if (previewClone == null)
            return;

        previewClone.transform.RotateAround(
            PreviewAnchorPosition,
            Vector3.up,
            previewSpinDegreesPerSecond * Time.unscaledDeltaTime);

        RenderPreview();
    }

    private void RenderPreview()
    {
        if (previewCamera == null)
            return;

        float distance = Mathf.Max(2f, framingRadius * 4f);
        previewCamera.transform.position = PreviewAnchorPosition + new Vector3(0f, framingRadius * 0.25f, -distance);
        previewCamera.transform.LookAt(PreviewAnchorPosition + new Vector3(0f, framingRadius * 0.15f, 0f));
        previewCamera.orthographicSize = framingRadius * 1.45f;
        previewCamera.Render();
    }

    private static GameObject CreateVisualClone(GameObject sourceRoot)
    {
        Dictionary<Transform, Transform> transformMap = new();

        GameObject cloneRoot = new(sourceRoot.name);
        CopyTransform(sourceRoot.transform, cloneRoot.transform);
        transformMap[sourceRoot.transform] = cloneRoot.transform;

        Transform[] sourceTransforms = sourceRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 1; i < sourceTransforms.Length; i++)
        {
            Transform source = sourceTransforms[i];
            GameObject child = new(source.name);
            Transform childTransform = child.transform;
            childTransform.SetParent(transformMap[source.parent], false);
            CopyTransform(source, childTransform);
            transformMap[source] = childTransform;
        }

        for (int i = 0; i < sourceTransforms.Length; i++)
        {
            Transform source = sourceTransforms[i];
            Transform destination = transformMap[source];

            MeshFilter sourceMeshFilter = source.GetComponent<MeshFilter>();
            MeshRenderer sourceMeshRenderer = source.GetComponent<MeshRenderer>();
            if (sourceMeshFilter != null && sourceMeshRenderer != null)
                CopyMeshRenderer(sourceMeshFilter, sourceMeshRenderer, destination.gameObject);

            SkinnedMeshRenderer sourceSkinnedRenderer = source.GetComponent<SkinnedMeshRenderer>();
            if (sourceSkinnedRenderer != null)
                CopySkinnedMeshRenderer(sourceSkinnedRenderer, destination.gameObject, transformMap);
        }

        Renderer[] renderers = cloneRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].enabled = true;
            renderers[i].shadowCastingMode = ShadowCastingMode.Off;
            renderers[i].receiveShadows = false;
        }

        return cloneRoot;
    }

    private static void CopyTransform(Transform source, Transform destination)
    {
        destination.localPosition = source.localPosition;
        destination.localRotation = source.localRotation;
        destination.localScale = source.localScale;
    }

    private static void CopyMeshRenderer(MeshFilter sourceFilter, MeshRenderer sourceRenderer, GameObject destination)
    {
        MeshFilter destinationFilter = destination.AddComponent<MeshFilter>();
        destinationFilter.sharedMesh = sourceFilter.sharedMesh;

        MeshRenderer destinationRenderer = destination.AddComponent<MeshRenderer>();
        destinationRenderer.sharedMaterials = sourceRenderer.sharedMaterials;
        destinationRenderer.enabled = true;
    }

    private static void CopySkinnedMeshRenderer(
        SkinnedMeshRenderer sourceRenderer,
        GameObject destination,
        Dictionary<Transform, Transform> transformMap)
    {
        SkinnedMeshRenderer destinationRenderer = destination.AddComponent<SkinnedMeshRenderer>();
        destinationRenderer.sharedMesh = sourceRenderer.sharedMesh;
        destinationRenderer.sharedMaterials = sourceRenderer.sharedMaterials;
        destinationRenderer.enabled = true;
        destinationRenderer.localBounds = sourceRenderer.localBounds;
        destinationRenderer.updateWhenOffscreen = sourceRenderer.updateWhenOffscreen;
        destinationRenderer.rootBone = sourceRenderer.rootBone != null && transformMap.TryGetValue(sourceRenderer.rootBone, out Transform mappedRootBone)
            ? mappedRootBone
            : null;

        Transform[] sourceBones = sourceRenderer.bones;
        Transform[] destinationBones = new Transform[sourceBones.Length];
        for (int i = 0; i < sourceBones.Length; i++)
        {
            Transform bone = sourceBones[i];
            if (bone != null && transformMap.TryGetValue(bone, out Transform mappedBone))
                destinationBones[i] = mappedBone;
        }

        destinationRenderer.bones = destinationBones;
    }

    private void DestroyPreviewClone()
    {
        if (previewClone == null)
            return;

        if (Application.isPlaying)
            Destroy(previewClone);
        else
            DestroyImmediate(previewClone);

        previewClone = null;
    }

    private void DestroyPreviewWorld()
    {
        if (previewWorldRoot == null)
            return;

        if (Application.isPlaying)
            Destroy(previewWorldRoot);
        else
            DestroyImmediate(previewWorldRoot);

        previewWorldRoot = null;
        previewCamera = null;
        previewLight = null;
    }

    private void ReleasePreviewTexture()
    {
        if (previewCamera != null)
            previewCamera.targetTexture = null;

        if (previewImage != null)
            previewImage.texture = null;

        if (previewTexture != null)
        {
            previewTexture.Release();
            if (Application.isPlaying)
                Destroy(previewTexture);
            else
                DestroyImmediate(previewTexture);
        }

        previewTexture = null;
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
            return;

        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }

    private static GameObject CreateUIObject(string objectName, Transform parent)
    {
        GameObject uiObject = new(objectName, typeof(RectTransform));
        uiObject.transform.SetParent(parent, false);
        return uiObject;
    }

    private static string GetActionFallbackText(InventoryAction action)
    {
        return action switch
        {
            InventoryAction.Use => "USE",
            InventoryAction.Eat => "EAT",
            InventoryAction.Give => "GIVE",
            InventoryAction.Trade => "TRADE",
            InventoryAction.Drop => "DROP",
            InventoryAction.PickUp => "PICK UP",
            _ => action.ToString()
        };
    }

    private static string NormalizeResourcePath(string resourcePath)
    {
        if (string.IsNullOrWhiteSpace(resourcePath))
            return string.Empty;

        resourcePath = resourcePath.Replace("\\", "/");
        int extensionIndex = resourcePath.LastIndexOf(".", StringComparison.Ordinal);
        if (extensionIndex >= 0)
            resourcePath = resourcePath[..extensionIndex];

        const string resourcesToken = "/Resources/";
        int resourcesIndex = resourcePath.IndexOf(resourcesToken, StringComparison.OrdinalIgnoreCase);
        if (resourcesIndex >= 0)
            resourcePath = resourcePath[(resourcesIndex + resourcesToken.Length)..];

        return resourcePath.Trim('/');
    }

    private static int GetSpriteSheetIndex(string spriteName)
    {
        if (string.IsNullOrEmpty(spriteName))
            return -1;

        int underscoreIndex = spriteName.LastIndexOf('_');
        if (underscoreIndex < 0 || underscoreIndex >= spriteName.Length - 1)
            return -1;

        return int.TryParse(spriteName[(underscoreIndex + 1)..], out int index) ? index : -1;
    }
}

public static class InventoryDialogBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInventoryDialogExists()
    {
        if (UnityEngine.Object.FindFirstObjectByType<InventoryDialogUI>() != null)
            return;

        GameObject inventoryDialogObject = new("InventoryDialogUI");
        inventoryDialogObject.AddComponent<InventoryDialogUI>();
    }
}
