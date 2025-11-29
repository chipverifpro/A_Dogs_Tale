using UnityEngine;
using UnityEngine.EventSystems; // to ignore UI clicks

public partial class Player : MonoBehaviour
{

    //[Header("Leader & Routing")]
    //public Transform leader;                    // your leader's Transform
    //public Player player;              // optional: your routing component with SetDestination(Vector3)

    [Header("Grid / Map")]
    public Vector3 origin = Vector3.zero;       // world-space origin of cell (0,0)
    public float cellSize = 1f;                 // world units per cell

    [Header("Raycast")]
    public LayerMask FP_BlockingMask;           // FirstPerson view: set to your targetable layers, including ceilings
    public LayerMask Overhead_BlockingMask;     // Overhead views: set to your targetable layers, exclude invisible ceilings
    private LayerMask navigationMask;           // we don't use this for navigation.
    public float rayMaxDistance = 200f;

    //[Header("Height Sampling (optional)")]
    private bool sampleTiltedFloorY = true; // don't change


    [Header("Input Control Settings")]
    public Transform playerTransform;        // the object to rotate
    public float turnSpeed = 1f;    // sensitivity multiplier
    public bool smooth = true;
    public float smoothTime = 4f;

    //private float currentYaw;
    private float targetYaw;
    private bool dragging = false;
    public bool leaderTravelling = false;
    private Vector2 lastPos;
    


    void AwakeMouseInput()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (!playerTransform)
            playerTransform = transform;  // Fallback to self
    
        dragging = false;
        startTimeMouseDown = 0f;
        // if (leader && !player) player = leader.GetComponent<Player>(); // not necessary
    }

    private float startTimeMouseDown;
    private float durationMouseDown;
    private EventSystem initialDownEvent;

    void Input_Update()
    {
        // 1) Input: A/D rotate, W/S move forward/back
        float turn = Input.GetAxisRaw("Horizontal"); // A/D
        float thrust = Input.GetAxisRaw("Vertical"); // W/S

        if ((Mathf.Abs(turn) > .001) || (Mathf.Abs(thrust) > .001))   // reject tiny movements
        {
            pack.PackLeader.next_formationCrumb.valid = false;
            Move_Update(turn, thrust);  // Only needed if we want to move
            leaderTravelling = false; // if keyboard input, stop travelling to click target
        }

        // Change the agent controlled by the player using the number keys
        if (Input.GetKeyDown(KeyCode.Alpha1)) ChangePlayerAgentById(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) ChangePlayerAgentById(1);
        if (Input.GetKeyDown(KeyCode.Alpha3)) ChangePlayerAgentById(2);
        if (Input.GetKeyDown(KeyCode.Alpha4)) ChangePlayerAgentById(3);
        if (Input.GetKeyDown(KeyCode.Alpha5)) ChangePlayerAgentById(4);

        UpdateMouseInput();  // Click/tap to move

        Update_Selectable();
    }

    void UpdateMouseInput()
    {
        // If shift is held, this click is for selection, not movement
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            return;

        bool turning = false;
        // mouse/touch start
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            //dragging = false;
            return;
        }

        if (Input.GetMouseButtonDown(0) && !dragging)
        {
            startTimeMouseDown = Time.time;
            dragging = true;
            lastPos = Input.mousePosition;

            // seed yaw targets
            targetYaw = agent.yawDeg;
            //currentYaw = agent.yawDeg;

            initialDownEvent = EventSystem.current;
        }

        // mouse up
        if (Input.GetMouseButtonUp(0))
        {
            dragging = false;
            durationMouseDown = Time.time - startTimeMouseDown;
            if (durationMouseDown < 0.2f)
            {
                //UpdateMouseClick(initialDownEvent, lastPos); // short press = click
                UpdateMouseClick(lastPos); // short press = click
            }
            return; // ok to return here
        }

        // while dragging
        if (dragging)
        {
            Vector2 currentPos = Input.mousePosition;

            // pixel deadzone instead of time gate
            float deltaX = currentPos.x - lastPos.x;
            const float PIXEL_DEADZONE = 1.5f;
            if (Mathf.Abs(deltaX) > PIXEL_DEADZONE)
            {
                turning = true;
                // NO Time.deltaTime here — deltaX is already frame-based
                targetYaw += deltaX * turnSpeed; // tune turnSpeed (e.g., 0.15–0.5)
            }

            lastPos = currentPos;
        }

        // apply rotation every frame (no == float check)
        float newYaw = smooth
            ? Mathf.LerpAngle(agent.yawDeg, targetYaw, Time.deltaTime * smoothTime)
            : targetYaw;

        if (Mathf.Abs(Mathf.DeltaAngle(agent.yawDeg, newYaw)) < 0.01f)
        {
            turning = false;
            leaderTravelling = false;
            return; // skip tiny changes
        }

        if (turning)
        {
            agent.yawDeg = newYaw;
            TransformPosition(agent);
        }
    }

    void UpdateMouseClick(Vector3 screenPosition)
    {
        // Ignore if pointer is over UI
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        if (Camera.main == null)
        {
            Debug.LogWarning("No Camera.main set; cannot raycast click.");
            return;
        }

        // Raycast to ground
        Ray ray = Camera.main.ScreenPointToRay(screenPosition);
        LayerMask groundMask = (dir.cameraModeSwitcher.cameraMode == CameraModes.FP) ? FP_BlockingMask : Overhead_BlockingMask;
        if (!Physics.Raycast(ray, out var hit, rayMaxDistance, groundMask))
        {
            // Optional: debug to see where you clicked
            Debug.DrawRay(ray.origin, ray.direction * 10f, Color.red, 0.25f);
            // Debug.Log("Click raycast missed ground.");
            return;
        }

        // Convert world → cell
        //Vector3 p = hit.point;
        // Bias hit point toward camera so clicking walls always targets near cell
        Vector3 p = hit.point - ray.direction.normalized * 0.05f;  // bias amount adjustable
        int cx = Mathf.FloorToInt((p.x - origin.x) / cellSize);
        int cz = Mathf.FloorToInt((p.z - origin.z) / cellSize);

        // Bounds check
        if (cx < 0 || cz < 0 || cx >= gen.cfg.mapWidth || cz >= gen.cfg.mapHeight)
            return;

        // Cell center (X,Z)
        float centerX = origin.x + (cx + 0.5f) * cellSize;
        float centerZ = origin.z + (cz + 0.5f) * cellSize;

        // Height Y
        float y = agent.height * gen.cfg.unitHeight;
        if (sampleTiltedFloorY && gen.cellGrid != null)
            y = SampleTiltedFloorY(new Vector2(centerX, centerZ), gen.cellGrid);

        Vector3 dest = new Vector3(centerX, y, centerZ);

        // Route the leader
        var crumb = new Crumb
        {
            pos2 = new(centerX, centerZ),
            height = y,
            valid = true
        };
        //Vector2 crumbpos2 = crumb.pos2;
        Vector2 dir_to_crumb = (agent.pos2 - crumb.pos2).normalized;
        crumb.yawDeg = Mathf.Atan2(dir_to_crumb.x, dir_to_crumb.y) * Mathf.Rad2Deg - yawCorrection; // face target
        agent.yawDeg = crumb.yawDeg;
        agent.next_formationCrumb = crumb;
        leaderTravelling = true;

        Debug.DrawLine(Camera.main.transform.position, hit.point, Color.cyan, 0.25f);
        Debug.DrawRay(dest, Vector3.up * 1.5f, Color.yellow, 0.5f);
        Debug.Log($"Clicked cell: ({cx},{cz}) world: {dest}");
    }


    // Mouse or touch primary press
    bool GetPrimaryDown()
    {
        if (Input.GetMouseButtonDown(0)) return true;
        if (Input.touchCount > 0 && Input.touches[0].phase == TouchPhase.Began) return true;
        return false;
    }

    // --- Tilted floor height sampling (uses your Cell.tiltFloor & height) ---
    float SampleTiltedFloorY(Vector2 worldXZ, Cell[,] grid)
    {
        int cx = Mathf.FloorToInt((worldXZ.x - origin.x) / cellSize);
        int cz = Mathf.FloorToInt((worldXZ.y - origin.z) / cellSize);
        int W = grid.GetLength(0), H = grid.GetLength(1);
        if (cx < 0 || cz < 0 || cx >= W || cz >= H) return agent ? agent.pos2.y : 0f;

        var cell = grid[cx, cz];

        // Plane normal from tilt
        Vector3 n = (cell.tiltFloor * Vector3.up).normalized;

        // Cell center point on plane at base height
        float centerX = origin.x + (cx + 0.5f) * cellSize;
        float centerZ = origin.z + (cz + 0.5f) * cellSize;
        Vector3 P0 = new Vector3(centerX, cell.height, centerZ);

        // Solve n·(X - P0)=0 for y, where X=(x,y,z)
        float ny = Mathf.Abs(n.y) < 1e-5f ? Mathf.Sign(n.y) * 1e-5f : n.y;
        float x = worldXZ.x, z = worldXZ.y;
        float y = P0.y - (n.x * (x - P0.x) + n.z * (z - P0.z)) / ny;
        return y;
    }


    [Header("References")]
    [SerializeField] private Camera mainCamera;          // assign Main Camera
    //[SerializeField] private Transform playerTransform;  // player / dog root transform

    [Header("Selection Settings")]
    [SerializeField] private LayerMask selectableMask;   // layers that can be selected
    [SerializeField] private float maxSelectDistanceTiles = 5f;
    [SerializeField] private float tileSize = 1f;        // world units per tile (probably 1.0f)

    [Header("Highlight Settings")]
    [SerializeField] private Color highlightColor = Color.yellow;

    private WorldObject currentSelection;
    private SelectableHighlight currentHighlight;

    private float MaxSelectDistanceWorld => maxSelectDistanceTiles * tileSize;


    private void Update_Selectable()
    {
        HandleSelectionClick();
        CheckSelectionDistance();
    }

    private void HandleSelectionClick()
    {
        // Shift + Left Click
        bool shift =
            Input.GetKey(KeyCode.LeftShift) ||
            Input.GetKey(KeyCode.RightShift);

        if (!shift) return;
        if (!Input.GetMouseButtonDown(0)) return;

        if (mainCamera == null) return;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, 1000f, selectableMask, QueryTriggerInteraction.Ignore))
        {
            // Find the WorldObject on this hit (or its parents)
            WorldObject wo = hit.collider.GetComponentInParent<WorldObject>();
            if (wo == null)
            {
                // Clicked something that isn't a WorldObject; treat as deselect
                Deselect();
                return;
            }

            // Distance check from player to object
            if (playerTransform != null)
            {
                float dist = Vector3.Distance(playerTransform.position, wo.transform.position);
                if (dist > MaxSelectDistanceWorld)
                {
                    // Too far to select
                    // Debug.Log($"Object {wo.DisplayName} is too far to select ({dist:F1}m).");
                    return;
                }
            }

            Select(wo);
        }
        else
        {
            // Clicked empty space; optional: deselect
            Deselect();
        }
    }

    private void CheckSelectionDistance()
    {
        if (currentSelection == null || playerTransform == null)
            return;

        float dist = Vector3.Distance(playerTransform.position, currentSelection.transform.position);
        if (dist > MaxSelectDistanceWorld)
        {
            // Auto-unselect when we walk away
            Deselect();
        }
    }

    private void Select(WorldObject wo)
    {
        if (currentSelection == wo)
            return;

        // Clear previous
        Deselect();

        currentSelection = wo;

        currentHighlight = wo.GetComponent<SelectableHighlight>();
        if (currentHighlight == null)
        {
            currentHighlight = wo.gameObject.AddComponent<SelectableHighlight>();
        }

        currentHighlight.SetHighlighted(true, highlightColor);

        // TODO: hook here for future interactions UI (inspect panel, context menu, etc.)
        // e.g. dir.ui.ShowObjectInfo(wo);
        Debug.Log($"Selected: {wo.DisplayName}");
    }

    private void Deselect()
    {
        if (currentHighlight != null)
        {
            currentHighlight.SetHighlighted(false, highlightColor);
            currentHighlight = null;
        }

        currentSelection = null;

        // TODO: hide selection UI if you add one later
        // dir.ui.HideObjectInfo();
        Debug.Log($"Deselected");
    }
}