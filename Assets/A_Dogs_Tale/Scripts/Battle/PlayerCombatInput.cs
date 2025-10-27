using UnityEngine;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(CombatActor))]
public class PlayerCombatInput : MonoBehaviour
{
    [Header("Scene Refs")]
    public Camera cam;
    public Transform enemy;                 // enemy/root
    public HitZoneSystem enemyZones;        // for multipliers (optional)
    public MonoBehaviour enemyHitReceiver;  // must implement IHitReceiver

    [Header("Gesture Settings")]
    public float startOnPlayerRadiusPx = 80f;   // how close to player’s screen pos to count as “on player”
    public float attackDragMinDistPx   = 60f;   // min drag from player to count as an attack
    public float attackAimConeDeg      = 70f;   // must generally aim toward enemy

    CombatActor actor;
    IHitReceiver hitTarget;
    Vector2 startScreenPos;
    bool tracking;
    bool startedOnPlayer;

    void Awake()
    {
        actor = GetComponent<CombatActor>();
        if (!cam) cam = Camera.main;
        hitTarget = enemyHitReceiver as IHitReceiver;
        if (!enemyZones && enemy) enemyZones = enemy.GetComponent<HitZoneSystem>();
    }

    void Update()
    {
        // Prefer touch if present
        if (Input.touchSupported && Input.touchCount > 0)
        {
            var t = Input.GetTouch(0);
            HandlePointer(t.phase == TouchPhase.Began, t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled, t.position);
        }
        else
        {
            bool down = Input.GetMouseButtonDown(0);
            bool up   = Input.GetMouseButtonUp(0);
            HandlePointer(down, up, Input.mousePosition);
        }
    }

    void HandlePointer(bool down, bool up, Vector2 pos)
    {
        //Debug.Log($"Handle Pointer: down={down}, up={up}, pos={pos}, tracking={tracking}");
        if (down)
        {
            tracking = true;
            startScreenPos = pos;
            startedOnPlayer = IsNearPlayerScreen(pos, startOnPlayerRadiusPx);
        }

        if (!tracking) return;
        Debug.Log($"startedOnPlayer = {startedOnPlayer}");
        if (up)
        {
            // Decide gesture only if it began on the player (otherwise it’s movement)
            if (startedOnPlayer)
            {
                Vector2 delta = pos - startScreenPos;
                float dragDist = delta.magnitude;

                if (dragDist < attackDragMinDistPx)
                {
                    // TAP -> Block
                    actor.TryBlock();
                }
                else
                {
                    // DRAG -> Attack if aimed toward enemy
                    if (IsAimedTowardEnemy(startScreenPos, pos, attackAimConeDeg))
                    {
                        float mult = enemyZones ? enemyZones.CurrentMultiplier : 1f;
                        actor.TryAttack(hitTarget, mult);
                    }
                    // else: ignore to allow movement controller to own it
                }
            }
            tracking = false;
        }
    }

    bool IsNearPlayerScreen(Vector2 screenPos, float radiusPx)
    {
        Debug.Log("Is Near Player Screen");
        if (!cam) return false;
        Vector3 playerScreen = cam.WorldToScreenPoint(transform.position);
        playerScreen.z = 0;
        return ( (Vector2)playerScreen - screenPos ).sqrMagnitude <= radiusPx * radiusPx;
    }

    bool IsAimedTowardEnemy(Vector2 start, Vector2 end, float coneDeg)
    {
        Debug.Log("Is Aimed Toward Enemy");
        if (!cam || !enemy) return true; // be permissive if missing refs
        Vector2 v = (end - start).normalized;

        Vector3 p0 = cam.WorldToScreenPoint(transform.position);
        Vector3 pe = cam.WorldToScreenPoint(enemy.position);
        Vector2 toEnemy = ((Vector2)pe - (Vector2)p0).normalized;

        float ang = Vector2.Angle(v, toEnemy);
        return ang <= coneDeg * 0.5f;
    }
}