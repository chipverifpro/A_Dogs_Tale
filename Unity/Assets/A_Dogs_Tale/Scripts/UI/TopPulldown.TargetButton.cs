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
    private void EnsureSniffAction()
    {
        if (sniffAction != null)
            return;

        sniffAction = new InputAction(
            name: "Sniff",
            type: InputActionType.Button,
            binding: "<Keyboard>/f"
        );
    }

    private void OnSniffToggle(InputAction.CallbackContext ctx)
    {
        ToggleSniffMode("InputAction");
    }

    private void ToggleSniffMode(string triggerSource)
    {
        if (lastSniffToggleFrame == Time.frameCount)
            return;

        lastSniffToggleFrame = Time.frameCount;
        isSniffModeActive = !isSniffModeActive;

        EnsureSniffVisuals();

        if (sniffVisuals != null)
        {
            sniffVisuals.SetSniffMode(isSniffModeActive);
        }
        else if (logSniffToggleDiagnostics)
        {
            Debug.LogWarning("TopPulldown: sniffVisuals is not assigned and no SniffModeVisuals component was found.", this);
        }

        if (!EnsureDir() || dir.scentRegistry == null)
        {
            Debug.LogError("TopPulldown: scentRegistry is null!");
            LogSniffDiagnostic(triggerSource, "missing Dir or scentRegistry");
            return;
        }

        if (isSniffModeActive)
        {
            dir.scentRegistry.ActivateScentOverlay(dir.scentRegistry.SelectedTargetScent);
        }
        else
        {
            dir.scentRegistry.DeactivateScentOverlay();
            CloseDropdown();
        }

        LogSniffDiagnostic(triggerSource, isSniffModeActive ? "activated" : "deactivated");
    }

    private void LogSniffDiagnostic(string triggerSource, string result)
    {
        if (dir == null)
        {
            lastSniffDiagnostic = $"Sniff {triggerSource}: {result}; Dir=null";
        }
        else
        {
            ScentAirGround scentSystem = dir.scents != null ? dir.scents : dir.scentAirGround;
            Camera scentCamera = dir.scentCam;
            ScentSource selected = dir.scentRegistry != null ? dir.scentRegistry.SelectedTargetScent : null;

            string selectedDescription = DescribeScentSource(selected);
            string visualDescription = sniffVisuals != null ? $"sniffVisuals={sniffVisuals.name}" : "sniffVisuals=null";
            string cameraDescription = scentCamera != null
                ? $"{scentCamera.name}.enabled={scentCamera.enabled}"
                : "FogCamera=null";
            string scentSystemDescription = scentSystem != null
                ? $"currentAgentId={scentSystem.currentAgentId}, active={scentSystem.IsScentCameraActive}"
                : "ScentAirGround=null";

            lastSniffDiagnostic =
                $"Sniff {triggerSource}: {result}; mode={isSniffModeActive}; selected={selectedDescription}; {visualDescription}; {cameraDescription}; {scentSystemDescription}";
        }

        if (logSniffToggleDiagnostics)
            Debug.Log($"[TopPulldown] {lastSniffDiagnostic}", this);
    }

    private static string DescribeScentSource(ScentSource scentSource)
    {
        if (scentSource == null)
            return "null";

        string scentName = string.IsNullOrWhiteSpace(scentSource.scentName) ? scentSource.category.ToString() : scentSource.scentName;
        return $"{scentName}(agentId={scentSource.agentId})";
    }

    private void EnsureSniffVisuals()
    {
        if (sniffVisuals != null)
            return;

        sniffVisuals = GetComponent<SniffModeVisuals>();
        if (sniffVisuals != null)
            return;

        sniffVisuals = FindFirstObjectByType<SniffModeVisuals>(FindObjectsInactive.Include);
    }

    // Called by other systems (unchanged)
    public void OnSniff(Cell currentCell)
    {
        if (!EnsureDir() || dir.scentRegistry == null || dir.scents == null)
            return;

        var detections = dir.scentRegistry.CollectScentsAtCell(currentCell, dir.scents);
        // bind to UI
    }

    public void OnScentClicked(ScentDetection detection)
    {
        if (!EnsureDir() || dir.scentRegistry == null)
            return;

        ScentSource selectedSource = dir.scentRegistry.SetSelectedTargetScent(detection.scentSource);
        dir.scentRegistry.ActivateScentOverlay(selectedSource);
        RefreshNoseButtonSelectionState();
    }

    private void BuildNoseButton(Transform parent, Transform searchRoot)
    {
        Transform existingButton = FindExistingUiElement(parent, searchRoot, "TargetButton");
        if (existingButton == null)
            existingButton = FindExistingUiElement(parent, searchRoot, "ScentTargetButton");

        GameObject buttonObject;
        bool createdButton = existingButton == null;
        if (createdButton)
        {
            buttonObject = new GameObject(
                "TargetButton",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(parent, false);
        }
        else
        {
            buttonObject = existingButton.gameObject;
        }

        buttonObject.name = "TargetButton";

        noseButtonRect = buttonObject.GetComponent<RectTransform>();
        if (createdButton)
        {
            noseButtonRect.anchorMin = new Vector2(1f, 1f);
            noseButtonRect.anchorMax = new Vector2(1f, 1f);
            noseButtonRect.pivot = new Vector2(1f, 1f);
            noseButtonRect.anchoredPosition = new Vector2(-noseButtonMargin, -noseButtonMargin);
            noseButtonRect.sizeDelta = new Vector2(noseButtonSize, noseButtonSize);
        }
        ConfigureTopControlRect(noseButtonRect, 0);

        noseButtonImage = GetOrAddComponent<Image>(buttonObject);
        noseButtonImage.color = noseButtonColor;
        noseButtonImage.raycastTarget = true;

        Button button = GetOrAddComponent<Button>(buttonObject);
        button.targetGraphic = noseButtonImage;
        button.onClick.RemoveListener(ToggleDropdown);
        button.onClick.RemoveListener(AudioPlayer.PlayUiButtonClick);
        button.onClick.AddListener(AudioPlayer.PlayUiButtonClick);
        button.onClick.AddListener(ToggleDropdown);

        Transform existingPreview = buttonObject.transform.Find("AgentPreview");
        GameObject previewObject;
        if (existingPreview == null)
        {
            previewObject = new GameObject("AgentPreview", typeof(RectTransform), typeof(RawImage));
            previewObject.transform.SetParent(buttonObject.transform, false);
        }
        else
        {
            previewObject = existingPreview.gameObject;
        }

        RectTransform previewRect = previewObject.GetComponent<RectTransform>();
        ConfigureTopControlIconRect(previewRect, 0.82f);

        targetPreviewImage = GetOrAddComponent<RawImage>(previewObject);
        targetPreviewImage.color = Color.white;
        targetPreviewImage.raycastTarget = false;
        previewObject.transform.SetAsFirstSibling();

        Transform existingIcon = buttonObject.transform.Find("Icon");
        GameObject iconObject;
        bool createdIcon = existingIcon == null;
        if (createdIcon)
        {
            iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(buttonObject.transform, false);
        }
        else
        {
            iconObject = existingIcon.gameObject;
        }

        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        if (createdIcon)
        {
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = Vector2.one * (noseButtonSize * 0.68f);
            iconRect.anchoredPosition = Vector2.zero;
        }
        ConfigureTopControlIconRect(iconRect, 0.68f);

        noseIconImage = GetOrAddComponent<Image>(iconObject);
        noseIconImage.sprite = GetTargetCrosshairSprite();
        noseIconImage.preserveAspect = true;
        noseIconImage.color = Color.white;
        noseIconImage.raycastTarget = false;
        targetCrosshairImage = noseIconImage;
        iconObject.transform.SetAsLastSibling();

        RefreshTargetButtonPreview(force: true);
        ConfigureTooltip(buttonObject, GetTargetButtonTooltipText);
    }

    private void BuildDropdown(Transform parent, Transform searchRoot)
    {
        Transform existingDropdown = FindExistingUiElement(parent, searchRoot, "ScentTargetDropdown");
        if (existingDropdown != null)
        {
            BindExistingDropdown(existingDropdown.gameObject);
            return;
        }

        GameObject dropdownObject = new GameObject(
            "ScentTargetDropdown",
            typeof(RectTransform),
            typeof(Image));
        dropdownObject.transform.SetParent(parent, false);

        dropdownRect = dropdownObject.GetComponent<RectTransform>();
        ConfigureTopPanelRect(dropdownRect, 0);
        dropdownRect.sizeDelta = new Vector2(dropdownWidth, dropdownMaxHeight);

        Image dropdownImage = dropdownObject.GetComponent<Image>();
        dropdownImage.color = dropdownBackgroundColor;

        GameObject titleObject = CreateTMPLabel(
            parent: dropdownObject.transform,
            name: "Title",
            text: "Select target",
            fontSize: 26f,
            alignment: TextAlignmentOptions.Left);

        RectTransform titleRect = titleObject.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.offsetMin = new Vector2(14f, -42f);
        titleRect.offsetMax = new Vector2(-14f, -10f);

        GameObject scrollObject = new GameObject(
            "ScrollView",
            typeof(RectTransform),
            typeof(Image),
            typeof(ScrollRect));
        scrollObject.transform.SetParent(dropdownObject.transform, false);

        RectTransform scrollRectTransform = scrollObject.GetComponent<RectTransform>();
        scrollRectTransform.anchorMin = new Vector2(0f, 0f);
        scrollRectTransform.anchorMax = new Vector2(1f, 1f);
        scrollRectTransform.offsetMin = new Vector2(12f, 12f);
        scrollRectTransform.offsetMax = new Vector2(-16f, -48f);

        Image scrollBackground = scrollObject.GetComponent<Image>();
        scrollBackground.color = new Color(1f, 1f, 1f, 0.08f);

        dropdownScrollRect = scrollObject.GetComponent<ScrollRect>();
        dropdownScrollRect.horizontal = false;
        dropdownScrollRect.movementType = ScrollRect.MovementType.Clamped;
        dropdownScrollRect.scrollSensitivity = 28f;

        GameObject viewportObject = new GameObject(
            "Viewport",
            typeof(RectTransform),
            typeof(Image),
            typeof(Mask));
        viewportObject.transform.SetParent(scrollObject.transform, false);

        RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = new Vector2(-14f, 0f);

        Image viewportImage = viewportObject.GetComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.02f);
        viewportObject.GetComponent<Mask>().showMaskGraphic = false;

        GameObject contentObject = new GameObject(
            "Content",
            typeof(RectTransform),
            typeof(VerticalLayoutGroup),
            typeof(ContentSizeFitter));
        contentObject.transform.SetParent(viewportObject.transform, false);

        dropdownContentRect = contentObject.GetComponent<RectTransform>();
        dropdownContentRect.anchorMin = new Vector2(0f, 1f);
        dropdownContentRect.anchorMax = new Vector2(1f, 1f);
        dropdownContentRect.pivot = new Vector2(0.5f, 1f);
        dropdownContentRect.offsetMin = Vector2.zero;
        dropdownContentRect.offsetMax = Vector2.zero;

        VerticalLayoutGroup layout = contentObject.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = contentObject.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Scrollbar scrollbar = CreateScrollbar(scrollObject.transform);
        dropdownScrollRect.viewport = viewportRect;
        dropdownScrollRect.content = dropdownContentRect;
        dropdownScrollRect.verticalScrollbar = scrollbar;
        dropdownScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        dropdownScrollRect.verticalScrollbarSpacing = 4f;

        dropdownObject.SetActive(false);
    }

    private void BindExistingDropdown(GameObject dropdownObject)
    {
        dropdownRect = dropdownObject.GetComponent<RectTransform>();
        if (dropdownRect == null)
            return;

        ConfigureTopPanelRect(dropdownRect, 0);

        Image dropdownImage = dropdownObject.GetComponent<Image>();
        if (dropdownImage != null)
            dropdownImage.color = dropdownBackgroundColor;

        Transform scrollTransform = dropdownObject.transform.Find("ScrollView");
        dropdownScrollRect = scrollTransform != null ? scrollTransform.GetComponent<ScrollRect>() : null;
        Transform contentTransform = dropdownObject.transform.Find("ScrollView/Viewport/Content");
        dropdownContentRect = contentTransform != null ? contentTransform.GetComponent<RectTransform>() : null;

        dropdownObject.SetActive(false);
    }

    private void ToggleDropdown()
    {
        if (dropdownRect == null)
            return;

        bool shouldOpen = !dropdownRect.gameObject.activeSelf;
        if (shouldOpen)
        {
            CloseModePanel();
            OpenDropdown();
        }
        else
            CloseDropdown();
    }

    private void OpenDropdown()
    {
        if (dropdownRect == null)
            return;

        CloseSpeedPanel();
        CloseModePanel();
        CloseEmoteDropdown();
        RefreshDropdownContents();
        dropdownRect.gameObject.SetActive(true);
        Canvas.ForceUpdateCanvases();
        if (dropdownScrollRect != null)
            dropdownScrollRect.verticalNormalizedPosition = 1f;
    }

    private void CloseDropdown()
    {
        if (dropdownRect != null)
            dropdownRect.gameObject.SetActive(false);

        HideTooltip();
    }

    private void CloseOpenPanelsIfClickedOutside(Vector2 screenPoint)
    {
        bool scentDropdownOpen = dropdownRect != null && dropdownRect.gameObject.activeSelf;
        bool modePanelOpen = modePanelRect != null && modePanelRect.gameObject.activeSelf;
        bool speedPanelOpen = speedPanelRect != null && speedPanelRect.gameObject.activeSelf;
        bool emoteDropdownOpen = emoteDropdownRect != null && emoteDropdownRect.gameObject.activeSelf;
        if (!scentDropdownOpen && !modePanelOpen && !speedPanelOpen && !emoteDropdownOpen)
            return;

        bool clickedScentDropdown = scentDropdownOpen &&
                                    RectTransformUtility.RectangleContainsScreenPoint(dropdownRect, screenPoint, null);
        bool clickedNoseButton = noseButtonRect != null &&
                                 RectTransformUtility.RectangleContainsScreenPoint(noseButtonRect, screenPoint, null);
        bool clickedModePanel = modePanelOpen &&
                                RectTransformUtility.RectangleContainsScreenPoint(modePanelRect, screenPoint, null);
        bool clickedModeButton = modeButtonRect != null &&
                                 RectTransformUtility.RectangleContainsScreenPoint(modeButtonRect, screenPoint, null);
        bool clickedSpeedPanel = speedPanelOpen &&
                                 RectTransformUtility.RectangleContainsScreenPoint(speedPanelRect, screenPoint, null);
        bool clickedSpeedButton = speedButtonRect != null &&
                                  RectTransformUtility.RectangleContainsScreenPoint(speedButtonRect, screenPoint, null);
        bool clickedEmoteDropdown = emoteDropdownOpen &&
                                    RectTransformUtility.RectangleContainsScreenPoint(emoteDropdownRect, screenPoint, null);
        bool clickedEmoteButton = emoteButtonRect != null &&
                                  RectTransformUtility.RectangleContainsScreenPoint(emoteButtonRect, screenPoint, null);

        if (scentDropdownOpen && !clickedScentDropdown && !clickedNoseButton)
            CloseDropdown();

        if (modePanelOpen && !clickedModePanel && !clickedModeButton)
            CloseModePanel();

        if (speedPanelOpen && !clickedSpeedPanel && !clickedSpeedButton)
            CloseSpeedPanel();

        if (emoteDropdownOpen && !clickedEmoteDropdown && !clickedEmoteButton)
            CloseEmoteDropdown();
    }

    private void RefreshDropdownContents()
    {
        if (dropdownContentRect != null)
        {
            for (int childIndex = dropdownContentRect.childCount - 1; childIndex >= 0; childIndex--)
                Destroy(dropdownContentRect.GetChild(childIndex).gameObject);
        }
        else
        {
            for (int i = 0; i < dropdownRows.Count; i++)
            {
                if (dropdownRows[i] != null)
                    Destroy(dropdownRows[i]);
            }
        }
        dropdownRows.Clear();

        if (!EnsureDir() || dir.scentRegistry == null || dropdownContentRect == null)
            return;

        List<ScentSource> scentSources = dir.scentRegistry.GetAvailableScentSources();
        ScentSource selectedTarget = dir.scentRegistry.SelectedTargetScent;

        if (scentSources.Count == 0)
        {
            dropdownRows.Add(CreateInfoRow("No scents available yet."));
            ResizeDropdown(1);
            return;
        }

        for (int i = 0; i < scentSources.Count; i++)
        {
            ScentSource scentSource = scentSources[i];
            dropdownRows.Add(CreateScentRow(scentSource, scentSource == selectedTarget));
        }

        ResizeDropdown(scentSources.Count);
    }

    private void ResizeDropdown(int rowCount)
    {
        if (dropdownRect == null)
            return;

        float headerHeight = 56f;
        float rowHeight = 54f;
        float chrome = 22f;
        float desiredHeight = headerHeight + chrome + rowHeight * Mathf.Max(1, rowCount);
        dropdownRect.sizeDelta = new Vector2(dropdownWidth, Mathf.Min(dropdownMaxHeight, desiredHeight));
    }

    private GameObject CreateInfoRow(string message)
    {
        return CreateInfoRowForParent(dropdownContentRect, message);
    }

    private GameObject CreateScentRow(ScentSource scentSource, bool isSelected)
    {
        GameObject rowObject = new GameObject(
            "ScentRow",
            typeof(RectTransform),
            typeof(Image),
            typeof(Button),
            typeof(LayoutElement));
        rowObject.transform.SetParent(dropdownContentRect, false);

        LayoutElement layout = rowObject.GetComponent<LayoutElement>();
        layout.preferredHeight = 52f;

        Image background = rowObject.GetComponent<Image>();
        background.color = isSelected ? dropdownSelectedColor : dropdownRowColor;

        Button button = rowObject.GetComponent<Button>();
        button.targetGraphic = background;
        button.onClick.AddListener(AudioPlayer.PlayUiButtonClick);
        button.onClick.AddListener(() => HandleScentSelected(scentSource));

        GameObject swatchObject = new GameObject(
            "Swatch",
            typeof(RectTransform),
            typeof(Image));
        swatchObject.transform.SetParent(rowObject.transform, false);

        RectTransform swatchRect = swatchObject.GetComponent<RectTransform>();
        swatchRect.anchorMin = new Vector2(0f, 0.5f);
        swatchRect.anchorMax = new Vector2(0f, 0.5f);
        swatchRect.pivot = new Vector2(0f, 0.5f);
        swatchRect.anchoredPosition = new Vector2(12f, 0f);
        swatchRect.sizeDelta = new Vector2(18f, 18f);

        Image swatchImage = swatchObject.GetComponent<Image>();
        swatchImage.color = GetScentColor(scentSource);

        GameObject labelObject = CreateTMPLabel(
            rowObject.transform,
            "Label",
            BuildScentRowText(scentSource),
            20f,
            TextAlignmentOptions.Left);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(40f, 8f);
        labelRect.offsetMax = new Vector2(-14f, -8f);

        return rowObject;
    }

    private string BuildScentRowText(ScentSource scentSource)
    {
        if (scentSource == null)
            return "Unknown scent";

        string displayName = !string.IsNullOrWhiteSpace(scentSource.scentName)
            ? scentSource.scentName.Trim()
            : scentSource.category.ToString();

        return $"{displayName} ({scentSource.category})";
    }

    private Color GetScentColor(ScentSource scentSource)
    {
        if (scentSource == null)
            return new Color(0.85f, 0.85f, 0.85f, 1f);

        if (scentSource.sourceGroundColor.a > 0f)
            return scentSource.sourceGroundColor;

        if (scentSource.sourceAirColor.a > 0f)
            return scentSource.sourceAirColor;

        if (scentSource.categoryColor.a > 0f)
            return scentSource.categoryColor;

        return new Color(0.85f, 0.85f, 0.85f, 1f);
    }

    private void HandleScentSelected(ScentSource scentSource)
    {
        if (!EnsureDir() || dir.scentRegistry == null)
            return;

        ScentSource selectedSource = dir.scentRegistry.SetSelectedTargetScent(scentSource);
        if (selectedSource == null)
            return;

        if (isSniffModeActive)
            dir.scentRegistry.ActivateScentOverlay(selectedSource);

        BottomBanner.Show(
            BannerSense.Smell,
            BannerLevel.Low,
            $"Target scent set: {BuildScentRowText(selectedSource)}");

        RefreshNoseButtonSelectionState();
        RefreshTargetButtonPreview(force: true);
        RefreshActiveTooltipText();
        CloseDropdown();
    }

    private void RefreshTargetButtonPreview(bool force = false)
    {
        if (targetPreviewImage == null)
            return;

        WorldObject previewObject = GetCurrentTargetPreviewWorldObject();
        if (!force && previewObject == targetPreviewedAgent && targetPreviewClone != null)
            return;

        BuildTargetPreviewClone(previewObject);
    }

    private WorldObject GetCurrentTargetPreviewWorldObject()
    {
        ScentSource selectedSource = GetSelectedTargetScent();
        if (selectedSource == null)
            return GetCurrentControlledWorldObject();

        return ResolveScentSourceWorldObject(selectedSource);
    }

    private ScentSource GetSelectedTargetScent()
    {
        return EnsureDir() && dir.scentRegistry != null
            ? dir.scentRegistry.SelectedTargetScent
            : null;
    }

    private WorldObject ResolveScentSourceWorldObject(ScentSource scentSource)
    {
        if (scentSource == null)
            return null;

        if (scentSource.agent != null)
            return scentSource.agent;

        if (scentSource.agentId < 0)
            return null;

        if (EnsureDir() && dir.worldObjectRegistry != null && dir.worldObjectRegistry.TryGet(scentSource.agentId, out WorldObject dirObject))
            return dirObject;

        WorldObjectRegistry registry = WorldObjectRegistry.Instance;
        return registry != null && registry.TryGet(scentSource.agentId, out WorldObject registryObject)
            ? registryObject
            : null;
    }

    private void EnsureTargetPreviewWorld()
    {
        if (targetPreviewWorldRoot != null)
            return;

        targetPreviewWorldRoot = new GameObject("TopPulldownTargetPreviewWorld");
        targetPreviewWorldRoot.hideFlags = HideFlags.HideAndDontSave;
        targetPreviewWorldRoot.transform.position = TargetPreviewAnchorPosition;

        GameObject cameraObject = new("TargetPreviewCamera");
        cameraObject.hideFlags = HideFlags.HideAndDontSave;
        cameraObject.transform.SetParent(targetPreviewWorldRoot.transform, false);
        targetPreviewCamera = cameraObject.AddComponent<Camera>();
        targetPreviewCamera.enabled = false;
        targetPreviewCamera.clearFlags = CameraClearFlags.SolidColor;
        targetPreviewCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        targetPreviewCamera.orthographic = true;
        targetPreviewCamera.nearClipPlane = 0.01f;
        targetPreviewCamera.farClipPlane = 100f;

        GameObject lightObject = new("TargetPreviewLight");
        lightObject.hideFlags = HideFlags.HideAndDontSave;
        lightObject.transform.SetParent(targetPreviewWorldRoot.transform, false);
        targetPreviewLight = lightObject.AddComponent<Light>();
        targetPreviewLight.type = LightType.Directional;
        targetPreviewLight.intensity = 1.25f;
        targetPreviewLight.color = Color.white;
        targetPreviewLight.shadows = LightShadows.None;
        targetPreviewLight.transform.rotation = Quaternion.Euler(35f, 135f, 0f);

        EnsureTargetPreviewTexture();
    }

    private void EnsureTargetPreviewTexture()
    {
        if (targetPreviewTexture != null)
            return;

        targetPreviewTexture = new RenderTexture(384, 384, 16, RenderTextureFormat.ARGB32);
        targetPreviewTexture.name = "TopPulldownTargetPreviewRT";
        targetPreviewTexture.Create();

        if (targetPreviewImage != null)
            targetPreviewImage.texture = targetPreviewTexture;
        if (targetPreviewCamera != null)
            targetPreviewCamera.targetTexture = targetPreviewTexture;
    }

    private void BuildTargetPreviewClone(WorldObject agent)
    {
        DestroyTargetPreviewClone();
        targetPreviewedAgent = agent;

        if (agent == null)
        {
            ClearTargetPreviewTexture();
            return;
        }

        EnsureTargetPreviewWorld();
        targetPreviewClone = CreateTargetVisualClone(agent.gameObject);
        targetPreviewClone.name = $"{agent.name}_TargetButtonPreview";
        targetPreviewClone.hideFlags = HideFlags.HideAndDontSave;
        targetPreviewClone.transform.SetParent(targetPreviewWorldRoot.transform, false);
        targetPreviewClone.transform.position = TargetPreviewAnchorPosition;

        CenterTargetPreviewClone(targetPreviewClone);
        RenderTargetPreview();
    }

    private void CenterTargetPreviewClone(GameObject clone)
    {
        Renderer[] renderers = clone.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            targetPreviewFramingRadius = 1f;
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        clone.transform.position += TargetPreviewAnchorPosition - bounds.center;

        Bounds centeredBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            centeredBounds.Encapsulate(renderers[i].bounds);

        targetPreviewFramingRadius = Mathf.Max(centeredBounds.extents.y, Mathf.Max(centeredBounds.extents.x, centeredBounds.extents.z));
        if (targetPreviewFramingRadius < 0.1f)
            targetPreviewFramingRadius = 0.5f;
    }

    private void SpinTargetButtonPreview()
    {
        if (targetPreviewClone == null)
            return;

        targetPreviewClone.transform.RotateAround(
            TargetPreviewAnchorPosition,
            Vector3.up,
            targetPreviewSpinDegreesPerSecond * Time.unscaledDeltaTime);

        RenderTargetPreview();
    }

    private void RenderTargetPreview()
    {
        if (targetPreviewCamera == null)
            return;

        float distance = Mathf.Max(2f, targetPreviewFramingRadius * 4f);
        float cameraHeight = Mathf.Tan(targetPreviewViewAngleDegrees * Mathf.Deg2Rad) * distance;
        targetPreviewCamera.transform.position = TargetPreviewAnchorPosition + new Vector3(0f, cameraHeight, -distance);
        targetPreviewCamera.transform.LookAt(TargetPreviewAnchorPosition + new Vector3(0f, targetPreviewFramingRadius * 0.1f, 0f));
        float figureScale = Mathf.Max(0.01f, targetPreviewFigureScale);
        targetPreviewCamera.orthographicSize = (targetPreviewFramingRadius * 1.45f) / figureScale;
        targetPreviewCamera.Render();
    }

    private void ClearTargetPreviewTexture()
    {
        if (targetPreviewTexture == null)
            return;

        RenderTexture previousActiveTexture = RenderTexture.active;
        RenderTexture.active = targetPreviewTexture;
        GL.Clear(true, true, Color.clear);
        RenderTexture.active = previousActiveTexture;
    }

    private static GameObject CreateTargetVisualClone(GameObject sourceRoot)
    {
        Dictionary<Transform, Transform> transformMap = new();

        GameObject cloneRoot = new(sourceRoot.name);
        CopyTargetTransform(sourceRoot.transform, cloneRoot.transform);
        transformMap[sourceRoot.transform] = cloneRoot.transform;

        Transform[] sourceTransforms = sourceRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 1; i < sourceTransforms.Length; i++)
        {
            Transform source = sourceTransforms[i];
            GameObject child = new(source.name);
            Transform childTransform = child.transform;
            childTransform.SetParent(transformMap[source.parent], false);
            CopyTargetTransform(source, childTransform);
            transformMap[source] = childTransform;
        }

        for (int i = 0; i < sourceTransforms.Length; i++)
        {
            Transform source = sourceTransforms[i];
            Transform destination = transformMap[source];

            MeshFilter sourceMeshFilter = source.GetComponent<MeshFilter>();
            MeshRenderer sourceMeshRenderer = source.GetComponent<MeshRenderer>();
            if (sourceMeshFilter != null && sourceMeshRenderer != null)
                CopyTargetMeshRenderer(sourceMeshFilter, sourceMeshRenderer, destination.gameObject);

            SkinnedMeshRenderer sourceSkinnedRenderer = source.GetComponent<SkinnedMeshRenderer>();
            if (sourceSkinnedRenderer != null)
                CopyTargetSkinnedMeshRenderer(sourceSkinnedRenderer, destination.gameObject, transformMap);
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

    private static void CopyTargetTransform(Transform source, Transform destination)
    {
        destination.localPosition = source.localPosition;
        destination.localRotation = source.localRotation;
        destination.localScale = source.localScale;
    }

    private static void CopyTargetMeshRenderer(MeshFilter sourceFilter, MeshRenderer sourceRenderer, GameObject destination)
    {
        MeshFilter destinationFilter = destination.AddComponent<MeshFilter>();
        destinationFilter.sharedMesh = sourceFilter.sharedMesh;

        MeshRenderer destinationRenderer = destination.AddComponent<MeshRenderer>();
        destinationRenderer.sharedMaterials = sourceRenderer.sharedMaterials;
        destinationRenderer.enabled = true;
    }

    private static void CopyTargetSkinnedMeshRenderer(
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

    private void DestroyTargetPreviewClone()
    {
        if (targetPreviewClone == null)
            return;

        if (Application.isPlaying)
            Destroy(targetPreviewClone);
        else
            DestroyImmediate(targetPreviewClone);

        targetPreviewClone = null;
        targetPreviewedAgent = null;
    }

    private void DestroyTargetPreviewWorld()
    {
        if (targetPreviewWorldRoot == null)
            return;

        if (Application.isPlaying)
            Destroy(targetPreviewWorldRoot);
        else
            DestroyImmediate(targetPreviewWorldRoot);

        targetPreviewWorldRoot = null;
        targetPreviewCamera = null;
        targetPreviewLight = null;
    }

    private void ReleaseTargetPreviewTexture()
    {
        if (targetPreviewCamera != null)
            targetPreviewCamera.targetTexture = null;

        if (targetPreviewImage != null)
            targetPreviewImage.texture = null;

        if (targetPreviewTexture != null)
        {
            targetPreviewTexture.Release();
            if (Application.isPlaying)
                Destroy(targetPreviewTexture);
            else
                DestroyImmediate(targetPreviewTexture);
        }

        targetPreviewTexture = null;
    }

    private void RefreshNoseButtonSelectionState()
    {
        if (noseButtonImage == null)
            return;

        ScentSource selectedSource = EnsureDir() && dir.scentRegistry != null
            ? dir.scentRegistry.SelectedTargetScent
            : null;

        if (selectedSource == null)
        {
            noseButtonImage.color = noseButtonColor;
            return;
        }

        Color accent = GetScentColor(selectedSource);
        accent.a = 0.94f;
        noseButtonImage.color = accent;
    }

    private Sprite GetScentIconSprite()
    {
        return SpriteServer.SpriteLookup("Sense_Smell_None")
            ?? SpriteServer.SpriteLookup("Sense_Smell_Low")
            ?? SpriteServer.SpriteLookup("Sense_Alert_None");
    }

    private Sprite GetTargetCrosshairSprite()
    {
        return SpriteServer.SpriteSheetLookup(targetIconSpriteResourcePath, 0)
            ?? SpriteServer.SpriteLookup("TargetIcon_D_0");
    }

    private string GetTargetButtonTooltipText()
    {
        ScentSource selectedSource = GetSelectedTargetScent();
        if (selectedSource == null)
            return "Target";

        WorldObject targetObject = ResolveScentSourceWorldObject(selectedSource);
        string targetName = targetObject != null && !string.IsNullOrWhiteSpace(targetObject.DisplayName)
            ? targetObject.DisplayName.Trim()
            : GetScentDisplayName(selectedSource);

        return string.IsNullOrWhiteSpace(targetName)
            ? "Target"
            : $"Target: {targetName}";
    }

    private static string GetScentDisplayName(ScentSource scentSource)
    {
        if (scentSource == null)
            return string.Empty;

        return !string.IsNullOrWhiteSpace(scentSource.scentName)
            ? scentSource.scentName.Trim()
            : scentSource.category.ToString();
    }
}
