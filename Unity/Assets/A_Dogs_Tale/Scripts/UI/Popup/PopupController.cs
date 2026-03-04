/*using UnityEngine;
using UnityEngine.UIElements;

public class PopupController : MonoBehaviour
{
    [Header("Optional: assign in inspector (otherwise loaded via Resources)")]
    public VisualTreeAsset pageGame;
    public VisualTreeAsset pagePack;
    public VisualTreeAsset pageInventory;
    public VisualTreeAsset pageSettings;

    UIDocument doc;
    VisualElement root, popup, frame, tabStrip, content;
    Button tabGame, tabPack, tabInventory, tabSettings;
    string curTab = "Game";
    bool isOpen;
    bool initialized;

    void Awake()
    {
        doc = GetComponent<UIDocument>();
        if (doc == null)
        {
            Debug.LogError("PopupController: UIDocument not found on the same GameObject.");
            return;
        }

        root = doc.rootVisualElement;
        // Delay full init until the visual tree has been created
        root.RegisterCallback<GeometryChangedEvent>(OnFirstGeometryChanged);
    }

    void OnFirstGeometryChanged(GeometryChangedEvent evt)
    {
        Debug.LogWarning($"OnFirstGeometryChanged: initialized = {initialized}");
        if (initialized) return;

        // Try to query the elements
        popup = root.Q<VisualElement>("PopupRoot");
        frame = root.Q<VisualElement>("Frame");
        tabStrip = root.Q<VisualElement>("TabStrip");
        content = root.Q<VisualElement>("TabContent");

        tabGame = root.Q<Button>("Tab-Game");
        tabPack = root.Q<Button>("Tab-Pack");
        tabInventory = root.Q<Button>("Tab-Inventory");
        tabSettings = root.Q<Button>("Tab-Settings");

        // Debug: what do we actually see right now?
        var allButtons = root.Query<Button>().ToList();
        Debug.LogWarning("PopupController: Found " + allButtons.Count + " buttons in root.");

        // If there are no buttons yet, the visual tree isn't ready.
        // Just wait for the next GeometryChangedEvent.
        if (allButtons.Count == 0 || popup == null)
        {
            return;
        }

        // From this point on we consider the tree ready and initialize once.
        initialized = true;
        root.UnregisterCallback<GeometryChangedEvent>(OnFirstGeometryChanged);

        if (tabGame == null || tabPack == null || tabInventory == null || tabSettings == null)
        {
            Debug.LogError("PopupController: One or more tab buttons are missing from the UXML.");
            return;
        }

        tabGame.clicked += () => SwitchTab("Game");
        tabPack.clicked += () => SwitchTab("Pack");
        tabInventory.clicked += () => SwitchTab("Inventory");
        tabSettings.clicked += () => SwitchTab("Settings");

        Debug.Log($"PopupController: success: {allButtons.Count} buttons assigned.");
        ApplyResponsiveLayout();
        Close(); // start hidden
    }

    void Update()
    {
        if (!initialized) return;

        if (Input.GetKeyDown(KeyCode.Escape)) Toggle();
        if (!isOpen) return;

        if (Input.GetKeyDown(KeyCode.Alpha1)) SwitchTab("Game");
        if (Input.GetKeyDown(KeyCode.Alpha2)) SwitchTab("Pack");
        if (Input.GetKeyDown(KeyCode.Alpha3)) SwitchTab("Inventory");
        if (Input.GetKeyDown(KeyCode.Alpha4)) SwitchTab("Settings");
    }

    void ApplyResponsiveLayout()
    {
        if (!initialized || root == null || frame == null || tabStrip == null) return;

        var sz = root.worldBound.size;
        bool portrait = sz.x < sz.y; // portrait: tabs on top; landscape: tabs on left

        SetClass(tabStrip, "horizontal", portrait);
        SetClass(tabStrip, "vertical", !portrait);
        SetClass(frame, "horizontal", portrait);
        SetClass(frame, "vertical", !portrait);
    }

    void SetClass(VisualElement ve, string cls, bool on)
    {
        if (ve == null) return;
        if (on) ve.AddToClassList(cls); else ve.RemoveFromClassList(cls);
    }

    public void Toggle()
    {
        if (!initialized) return;

        if (isOpen) Close();
        else Open();
    }

    public void Open()
    {
        if (!initialized) return;
        isOpen = true;
        popup?.RemoveFromClassList("hidden");
        SwitchTab(curTab);
    }

    public void Close()
    {
        if (!initialized) return;
        isOpen = false;
        popup?.AddToClassList("hidden");
    }

    void SwitchTab(string tab)
    {
        if (!initialized || content == null) return;

        curTab = tab;

        foreach (var t in new[] { tabGame, tabPack, tabInventory, tabSettings })
            t?.RemoveFromClassList("selected");

        (tab switch
        {
            "Game" => tabGame,
            "Pack" => tabPack,
            "Inventory" => tabInventory,
            "Settings" => tabSettings,
            _ => tabGame
        })?.AddToClassList("selected");

        content.Clear();

        var vta = (tab switch
        {
            "Game" => pageGame ?? Resources.Load<VisualTreeAsset>("UI/Popup/Pages/Page_Game"),
            "Pack" => pagePack ?? Resources.Load<VisualTreeAsset>("UI/Popup/Pages/Page_Pack"),
            "Inventory" => pageInventory ?? Resources.Load<VisualTreeAsset>("UI/Popup/Pages/Page_Inventory"),
            "Settings" => pageSettings ?? Resources.Load<VisualTreeAsset>("UI/Popup/Pages/Page_Settings"),
            _ => pageGame
        });

        if (vta != null)
        {
            var page = vta.CloneTree();
            content.Add(page);

            // Optional: auto-bind per-page controllers if PageBinder is present
            var binder = GetComponent<PageBinder>();
            if (binder != null)
                binder.TryBind(tab, page);
        }
        else
        {
            Debug.LogWarning($"PopupController: No VisualTreeAsset found for tab '{tab}'.");
        }
    }
}
*/


using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public class PopupController : MonoBehaviour
{
    [Header("Optional: assign in inspector (otherwise loaded via Resources)")]
    public VisualTreeAsset pageGame;
    public VisualTreeAsset pagePack;
    public VisualTreeAsset pageInventory;
    public VisualTreeAsset pageSettings;

    UIDocument doc;
    VisualElement root, popup, frame, tabStrip, content;
    Button tabGame, tabPack, tabInventory, tabSettings;
    string curTab = "Game";
    bool isOpen = false;
    bool initialized;

    void Awake()
    {
        doc = GetComponent<UIDocument>();
        if (doc == null)
        {
            Debug.LogError("PopupController: UIDocument not found on the same GameObject.");
            return;
        }

        root = doc.rootVisualElement;
    }

    void Start()
    {
        // Do initialization in a coroutine so we can wait until the visual tree is built.
        StartCoroutine(InitRoutine());
    }

    IEnumerator InitRoutine()
    {
        // Try for a few frames until we see our buttons
        for (int i = 0; i < 10; i++)
        {
            if (root == null)
                root = doc.rootVisualElement;

            if (root != null)
            {
                popup    = root.Q<VisualElement>("PopupRoot");
                frame    = root.Q<VisualElement>("Frame");
                tabStrip = root.Q<VisualElement>("TabStrip");
                content  = root.Q<VisualElement>("TabContent");

                tabGame      = root.Q<Button>("Tab-Game");
                tabPack      = root.Q<Button>("Tab-Pack");
                tabInventory = root.Q<Button>("Tab-Inventory");
                tabSettings  = root.Q<Button>("Tab-Settings");

                var allButtons = root.Query<Button>().ToList();
                if (allButtons.Count == 0) Debug.LogWarning("PopupController: Init attempt " + i + " found " + allButtons.Count + " buttons.");

                if (popup != null && allButtons.Count > 0 &&
                    tabGame != null && tabPack != null && tabInventory != null && tabSettings != null)
                {
                    // We’re ready
                    break;
                }
            }

            // wait a frame and try again
            yield return null;
        }

        if (tabGame == null || tabPack == null || tabInventory == null || tabSettings == null)
        {
            Debug.LogError("PopupController: One or more tab buttons are missing from the UXML after InitRoutine.");
            yield break;
        }

        // Wire up tab clicks
        tabGame.clicked      += () => SwitchTab("Game");
        tabPack.clicked      += () => SwitchTab("Pack");
        tabInventory.clicked += () => SwitchTab("Inventory");
        tabSettings.clicked  += () => SwitchTab("Settings");

        ApplyResponsiveLayout();
        Close(); // start hidden

        initialized = true;
        Debug.Log("PopupController: Initialized OK.");
    }

    void Update()
    {
        if (!initialized) return;

        if (Input.GetKeyDown(KeyCode.Escape)) Toggle();
        if (!isOpen) return;

        if (Input.GetKeyDown(KeyCode.Alpha1)) SwitchTab("Game");
        if (Input.GetKeyDown(KeyCode.Alpha2)) SwitchTab("Pack");
        if (Input.GetKeyDown(KeyCode.Alpha3)) SwitchTab("Inventory");
        if (Input.GetKeyDown(KeyCode.Alpha4)) SwitchTab("Settings");
    }

    void ApplyResponsiveLayout()
    {
        if (!initialized || root == null || frame == null || tabStrip == null) return;

        var sz = root.worldBound.size;
        bool portrait = sz.x < sz.y; // portrait: tabs on top; landscape: tabs on left

        SetClass(tabStrip, "horizontal", portrait);
        SetClass(tabStrip, "vertical",   !portrait);
        SetClass(frame,    "horizontal", portrait);
        SetClass(frame,    "vertical",   !portrait);
    }

    void SetClass(VisualElement ve, string cls, bool on)
    {
        if (ve == null) return;
        if (on) ve.AddToClassList(cls); else ve.RemoveFromClassList(cls);
    }

    public void Toggle()
    {
        if (!initialized || popup == null) return;

        if (isOpen) Close();
        else Open();
    }
    public void Open()
    {
        if (!initialized || popup == null) return;

        isOpen = true;

        // First make sure it's participating in layout
        popup.style.display = DisplayStyle.Flex;

        // Then add the visible class so the transition animates in
        popup.AddToClassList("visible");

        SwitchTab(curTab);
    }

    public void Close()
    {
        if (!initialized || popup == null) return;

        isOpen = false;

        // Remove visible class so it animates out (fade/scale)
        popup.RemoveFromClassList("visible");

        // Optionally, hide after the animation finishes
        StartCoroutine(HideAfterDelay(0.15f)); // match USS duration
    }  

    private System.Collections.IEnumerator HideAfterDelay(float seconds)
    {
        yield return new WaitForSeconds(seconds);

        if (!isOpen && popup != null)
        {
            popup.style.display = DisplayStyle.None;
        }
    }

    void SwitchTab(string tab)
    {
        if (!initialized || content == null) return;

        curTab = tab;

        foreach (var t in new[] { tabGame, tabPack, tabInventory, tabSettings })
            t?.RemoveFromClassList("selected");

        (tab switch
        {
            "Game"      => tabGame,
            "Pack"      => tabPack,
            "Inventory" => tabInventory,
            "Settings"  => tabSettings,
            _           => tabGame
        })?.AddToClassList("selected");

        content.Clear();

        var vta = (tab switch
        {
            "Game"      => pageGame      ?? Resources.Load<VisualTreeAsset>("UI/Popup/Pages/Page_Game"),
            "Pack"      => pagePack      ?? Resources.Load<VisualTreeAsset>("UI/Popup/Pages/Page_Pack"),
            "Inventory" => pageInventory ?? Resources.Load<VisualTreeAsset>("UI/Popup/Pages/Page_Inventory"),
            "Settings"  => pageSettings  ?? Resources.Load<VisualTreeAsset>("UI/Popup/Pages/Page_Settings"),
            _ => pageGame
        });

        if (vta != null)
        {
            var page = vta.CloneTree();
            content.Add(page);

            var binder = GetComponent<PageBinder>();
            if (binder != null)
                binder.TryBind(tab, page);
        }
        else
        {
            Debug.LogWarning($"PopupController: No VisualTreeAsset found for tab '{tab}'.");
        }
    }
}