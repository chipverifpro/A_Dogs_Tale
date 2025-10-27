using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerOrbitMover : MonoBehaviour
{
    [Header("Refs")]
    public ArenaRig arena;              // Assign your ArenaRig
    public Camera cam;                  // Main camera; used to project input onto ground plane

    [Header("Movement")]
    public float orbitSlewSpeed = 720f; // deg/sec to snap to new angle (higher = snappier)
    public float radialSpeed    = 3.5f; // m/sec in/out
    public float heightY        = 0f;   // keep on ground plane

    [Header("Input")]
    public float minDragPixelsToMove = 2f; // ignore micro jitter
    public int   groundLayerMask = ~0;     // raycast mask if you have a ground layer

    CharacterController cc;
    float targetAngleDeg;     // where on the ring we want to be (around center)
    float currentRadius;

    // cached
    Plane groundPlane;
    Vector3 centerPos => arena ? arena.CenterPos : Vector3.zero;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        if (!cam) cam = Camera.main;
        groundPlane = new Plane(Vector3.up, Vector3.zero);

        currentRadius = arena ? Mathf.Clamp(arena.orbitRadius, arena.minRadius, arena.maxRadius) : 4f;
        targetAngleDeg = GetAngleDeg(transform.position);
    }

    void Update()
    {
        if (!arena) return;

        HandleInput();

        // Smoothly rotate around the center toward target angle
        float curAngle = GetAngleDeg(transform.position);
        float newAngle = Mathf.MoveTowardsAngle(curAngle, targetAngleDeg, orbitSlewSpeed * Time.deltaTime);

        // Clamp radius each frame
        currentRadius = Mathf.Clamp(currentRadius, arena.minRadius, arena.maxRadius);

        // Compute new desired position on ring
        Vector3 desired = centerPos + AngleToDir(newAngle) * currentRadius;
        desired.y = heightY;

        // Move controller (no physics pop)
        Vector3 delta = desired - transform.position;
        cc.Move(delta);
    }

    void HandleInput()
    {
        // Mouse (desktop)
        if (Input.mousePresent)
        {
            if (Input.GetMouseButton(0))
            {
                Debug.Log("Mouse Button 0");
                if (ScreenDragToWorld(Input.mousePosition, out Vector3 world))
                    UpdateOrbitFromPointer(world);
            }
        }

        // Touch (mobile)
        if (Input.touchSupported && Input.touchCount > 0)
        {
            var t = Input.GetTouch(0);
            Debug.Log($"Get Touch: TouchPhase = {t.phase}");
            if (t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary)
            {
                if (ScreenDragToWorld(t.position, out Vector3 world))
                    UpdateOrbitFromPointer(world);
            }
        }
    }

    void UpdateOrbitFromPointer(Vector3 pointerWorld)
    {
        Debug.Log("Update Orbit From Pointer");
        // Vector from center -> pointer
        Vector3 toPointer = pointerWorld - centerPos; toPointer.y = 0f;
        if (toPointer.sqrMagnitude < 0.0001f) return;

        // Update target angle
        targetAngleDeg = Mathf.Atan2(toPointer.z, toPointer.x) * Mathf.Rad2Deg;

        // Update radius relative to pointer distance (small nudge = slight in/out)
        float pointerRadius = toPointer.magnitude;
        // Move radius toward pointer radius with a gentle rate
        currentRadius = Mathf.MoveTowards(currentRadius, pointerRadius, radialSpeed * Time.deltaTime);
    }

    bool ScreenDragToWorld(Vector2 screen, out Vector3 world)
    {
        Debug.Log("Screen Drag To World");
        world = Vector3.zero;
        if (!cam) return false;

        Ray ray = cam.ScreenPointToRay(screen);
        if (groundPlane.Raycast(ray, out float enter))
        {
            world = ray.GetPoint(enter);
            return true;
        }
        return false;
    }

    float GetAngleDeg(Vector3 pos)
    {
        Vector3 d = pos - centerPos; d.y = 0f;
        if (d.sqrMagnitude < 0.0001f) return 0f;
        return Mathf.Atan2(d.z, d.x) * Mathf.Rad2Deg;
    }

    Vector3 AngleToDir(float angleDeg)
    {
        float r = angleDeg * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(r), 0f, Mathf.Sin(r));
    }
}