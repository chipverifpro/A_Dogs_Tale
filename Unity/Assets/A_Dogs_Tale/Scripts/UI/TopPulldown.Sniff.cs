using UnityEngine;
using TMPro;
using UnityEngine.UI;
using DogGame.Modules;

public partial class TopPulldown
{
    private static readonly Color SniffOverlayBackgroundColor = new(0.055f, 0.065f, 0.075f, 0.88f);
    private static readonly Color SniffBarBackgroundColor = new(0.13f, 0.15f, 0.17f, 0.95f);
    private static readonly Color GroundScentBarColor = new(0.42f, 0.75f, 0.28f, 1f);
    private static readonly Color AirScentBarColor = new(0.22f, 0.72f, 0.9f, 1f);

    public static void ToggleDogSight(string triggerSource = "DogSight")
    {
        TopPulldown pulldown = FindFirstObjectByType<TopPulldown>(FindObjectsInactive.Include);
        if (pulldown == null)
        {
            Debug.LogWarning("TopPulldown: cannot toggle Dog Sight because no TopPulldown instance was found.");
            return;
        }

        pulldown.ToggleSniffMode(triggerSource);
    }

    private void BuildSniffResultsOverlay(Transform parent, Transform searchRoot)
    {
        Transform existingOverlay = FindExistingUiElement(parent, searchRoot, "SniffResultsOverlay");
        GameObject overlayObject;
        if (existingOverlay == null)
        {
            overlayObject = new GameObject("SniffResultsOverlay", typeof(RectTransform), typeof(Image));
            overlayObject.transform.SetParent(parent, false);
        }
        else
        {
            overlayObject = existingOverlay.gameObject;
        }

        sniffResultsOverlayRect = GetOrAddComponent<RectTransform>(overlayObject);
        Image background = GetOrAddComponent<Image>(overlayObject);
        background.color = SniffOverlayBackgroundColor;
        background.raycastTarget = false;

        sniffResultsTitleLabel = BuildSniffOverlayLabel(
            overlayObject.transform,
            "Title",
            new Vector2(14f, -8f),
            new Vector2(-14f, -38f),
            22f,
            TextAlignmentOptions.MidlineLeft);

        BuildSniffBar(
            overlayObject.transform,
            "Ground",
            48f,
            GroundScentBarColor,
            out sniffGroundBarFill,
            out sniffGroundValueLabel);
        BuildSniffBar(
            overlayObject.transform,
            "Air",
            94f,
            AirScentBarColor,
            out sniffAirBarFill,
            out sniffAirValueLabel);

        ApplySniffResultsOverlayLayout();
    }

    private void BuildSniffBar(
        Transform parent,
        string labelText,
        float top,
        Color fillColor,
        out Image fillImage,
        out TextMeshProUGUI valueLabel)
    {
        TextMeshProUGUI nameLabel = BuildSniffOverlayLabel(
            parent,
            labelText + "Label",
            new Vector2(14f, -top),
            new Vector2(104f, -(top + 32f)),
            18f,
            TextAlignmentOptions.MidlineLeft);
        nameLabel.text = labelText;

        GameObject barObject = new(labelText + "Bar", typeof(RectTransform), typeof(Image));
        barObject.transform.SetParent(parent, false);
        RectTransform barRect = barObject.GetComponent<RectTransform>();
        SetSniffOverlayRect(barRect, new Vector2(104f, -top), new Vector2(-72f, -(top + 28f)));
        Image barBackground = barObject.GetComponent<Image>();
        barBackground.color = SniffBarBackgroundColor;
        barBackground.raycastTarget = false;

        GameObject fillObject = new("Fill", typeof(RectTransform), typeof(Image));
        fillObject.transform.SetParent(barObject.transform, false);
        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(2f, 2f);
        fillRect.offsetMax = new Vector2(-2f, -2f);
        fillImage = fillObject.GetComponent<Image>();
        fillImage.color = fillColor;
        fillImage.raycastTarget = false;
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImage.fillAmount = 0f;

        valueLabel = BuildSniffOverlayLabel(
            parent,
            labelText + "Value",
            new Vector2(-68f, -top),
            new Vector2(-12f, -(top + 32f)),
            17f,
            TextAlignmentOptions.MidlineRight);
        valueLabel.text = "0";
    }

    private TextMeshProUGUI BuildSniffOverlayLabel(
        Transform parent,
        string objectName,
        Vector2 offsetMin,
        Vector2 offsetMax,
        float fontSize,
        TextAlignmentOptions alignment)
    {
        Transform existing = parent.Find(objectName);
        GameObject labelObject = existing != null
            ? existing.gameObject
            : new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        if (existing == null)
            labelObject.transform.SetParent(parent, false);

        RectTransform rect = GetOrAddComponent<RectTransform>(labelObject);
        SetSniffOverlayRect(rect, offsetMin, offsetMax);
        TextMeshProUGUI label = GetOrAddComponent<TextMeshProUGUI>(labelObject);
        label.fontSize = fontSize;
        label.color = Color.white;
        label.alignment = alignment;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
        if (TMP_Settings.defaultFontAsset != null)
            label.font = TMP_Settings.defaultFontAsset;
        return label;
    }

    private static void SetSniffOverlayRect(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(offsetMin.x, offsetMax.y);
        rect.offsetMax = new Vector2(offsetMax.x, offsetMin.y);
    }

    private void ApplySniffResultsOverlayLayout()
    {
        if (sniffResultsOverlayRect == null)
            return;
        if (!useCornerControls)
        {
            sniffResultsOverlayRect.gameObject.SetActive(false);
            return;
        }

        float topInset = GetTopSafeAreaInset();
        float width = Mathf.Max(340f, topControlButtonSize * 2.25f);
        float height = 136f;
        sniffResultsOverlayRect.gameObject.SetActive(true);
        sniffResultsOverlayRect.anchorMin = new Vector2(1f, 1f);
        sniffResultsOverlayRect.anchorMax = new Vector2(1f, 1f);
        sniffResultsOverlayRect.pivot = new Vector2(1f, 1f);
        sniffResultsOverlayRect.localScale = Vector3.one;
        sniffResultsOverlayRect.anchoredPosition = new Vector2(
            -(cornerControlMargin + topControlButtonSize + modeButtonSpacing),
            -(cornerControlMargin + topInset));
        sniffResultsOverlayRect.sizeDelta = new Vector2(width, height);
    }

    private void EnsureSniffOverlaySubscription()
    {
        ScentAirGround scentSystem = EnsureDir()
            ? (dir.scents != null ? dir.scents : dir.scentAirGround)
            : null;
        if (subscribedSniffOverlayScentSystem == scentSystem)
            return;

        RemoveSniffOverlaySubscription();
        subscribedSniffOverlayScentSystem = scentSystem;
        if (subscribedSniffOverlayScentSystem != null)
            subscribedSniffOverlayScentSystem.PhysicsCycleCompleted += OnSniffPhysicsCycleCompleted;
    }

    private void RemoveSniffOverlaySubscription()
    {
        if (subscribedSniffOverlayScentSystem != null)
            subscribedSniffOverlayScentSystem.PhysicsCycleCompleted -= OnSniffPhysicsCycleCompleted;
        subscribedSniffOverlayScentSystem = null;
    }

    private void OnSniffPhysicsCycleCompleted(ScentAirGround scentSystem)
    {
        RefreshSniffResultsOverlay();
    }

    private void RefreshSniffOverlayForContextChanges()
    {
        EnsureSniffOverlaySubscription();

        WorldObject agent = dir != null && dir.playerPack != null ? dir.playerPack.packLeader : null;
        Cell cell = agent != null && agent.locationModule != null ? agent.locationModule.cell : null;
        string scentKey = dir != null && dir.scentRegistry != null
            ? dir.scentRegistry.SelectedTargetScentKey
            : string.Empty;

        if (agent != sniffOverlayAgent || cell != sniffOverlayCell || scentKey != sniffOverlayScentKey)
            RefreshSniffResultsOverlay();
    }

    private void RefreshSniffResultsOverlay()
    {
        if (sniffResultsOverlayRect == null)
            return;

        WorldObject agent = EnsureDir() && dir.playerPack != null ? dir.playerPack.packLeader : null;
        Cell cell = agent != null && agent.locationModule != null ? agent.locationModule.cell : null;
        ScentSource selected = dir != null && dir.scentRegistry != null
            ? dir.scentRegistry.SelectedTargetScent
            : null;
        string scentKey = dir != null && dir.scentRegistry != null
            ? dir.scentRegistry.SelectedTargetScentKey
            : string.Empty;

        sniffOverlayAgent = agent;
        sniffOverlayCell = cell;
        sniffOverlayScentKey = scentKey;

        float ground = 0f;
        float air = 0f;
        if (agent != null && cell != null && selected != null && agent.scentPerceptionModule != null)
        {
            int height = agent.locationModule.height;
            agent.scentPerceptionModule.TryGetScentStrengthAtCell(
                scentKey, cell.pos, height, ScentMedium.Ground, out ground);
            agent.scentPerceptionModule.TryGetScentStrengthAtCell(
                scentKey, cell.pos, height, ScentMedium.Air, out air);
        }

        float fullScale = subscribedSniffOverlayScentSystem != null
            ? Mathf.Max(0.000001f, subscribedSniffOverlayScentSystem.maxVisualIntensity)
            : 1f;
        if (sniffGroundBarFill != null)
            sniffGroundBarFill.fillAmount = Mathf.Clamp01(ground / fullScale);
        if (sniffAirBarFill != null)
            sniffAirBarFill.fillAmount = Mathf.Clamp01(air / fullScale);
        if (sniffGroundValueLabel != null)
            sniffGroundValueLabel.text = ground.ToString("0.###");
        if (sniffAirValueLabel != null)
            sniffAirValueLabel.text = air.ToString("0.###");
        if (sniffResultsTitleLabel != null)
        {
            string scentName = selected == null
                ? "No scent selected"
                : (string.IsNullOrWhiteSpace(selected.scentName) ? scentKey : selected.scentName);
            sniffResultsTitleLabel.text = "Sniff: " + scentName;
        }
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
        RefreshTargetButtonSelectionState();
    }
}
