using UnityEngine;
using UnityEngine.UIElements;

[DefaultExecutionOrder(-5000)]
public class PopupBootstrap : MonoBehaviour
{
    public string panelSettingsPath = "UI/PanelSettings/RuntimePanelSettings"; // Resources path
    public string popupUxmlPath = "UI/Popup/PopupPanel";                        // Resources path

    void Awake()
    {
        var uid = gameObject.GetComponent<UIDocument>();
        if (!uid) uid = gameObject.AddComponent<UIDocument>();

        var panel = Resources.Load<PanelSettings>(panelSettingsPath);
        if (!panel) Debug.LogWarning("PanelSettings not found in Resources at '" + panelSettingsPath + "'. Create one via Create > UI Toolkit > Panel Settings.");
        uid.panelSettings = panel;

        var tree = Resources.Load<VisualTreeAsset>(popupUxmlPath);
        if (!tree) Debug.LogError("PopupPanel UXML not found at Resources/" + popupUxmlPath + ". Move PopupPanel.uxml under a Resources folder or update the path.");
        uid.visualTreeAsset = tree;

        if (!gameObject.GetComponent<PopupController>()) gameObject.AddComponent<PopupController>();
        if (!gameObject.GetComponent<PageBinder>()) gameObject.AddComponent<PageBinder>();
    }
}