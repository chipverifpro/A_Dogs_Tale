using System.Collections;
using Cinemachine;
using DogGame.Modules;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public enum CameraModes { Unchanged = 0, FP, Overhead, Perspective, Nose };   // Unchanged is not a valid camera, just means leave as-is

public class CameraModeSwitcher : MonoBehaviour
{
    [Header("Game Dir")]
    public Dir dir;

    [Header("Current Modes")]
    public CameraModes cameraMode = CameraModes.Nose;    // More readable version of current_camera for use in other systems
    //public bool scentFogVisible = false;  //TODO: move contols/setup into camera script
    public bool playerVisible = true;

    public CinemachineBrain brain;
    public CinemachineVirtualCamera vcamFP, vcamPerspective, vcamOverhead, vcamNose, vcamFree;
    //public GameObject playerModel;
    public KeyCode toggleKey = KeyCode.Tab;
    public KeyCode freeCameraToggleKey = KeyCode.BackQuote;
    public WorldObject target;
    public float height = 20f;

    [Header("Free Camera")]
    public bool freeCameraActive = false;
    [SerializeField] private float freeCameraPriority = 20f;
    [SerializeField] private CameraModes freeCameraRestoreMode = CameraModes.Perspective;
    private FreeCameraController freeCameraController;

    private Coroutine waiter = null;
    private Coroutine startupZoomRoutine = null;
    private bool loggedTargetWarning = false;   // only display ONE target warning instead of spamming every frame.

    void Awake()
    {
        // Try to auto-find on first load
        InitializeConnections();

        // Re-connect whenever a new scene is loaded
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        InitializeConnections();
    }

    void InitializeConnections()
    {
        // --- Find the CinemachineBrain on the main camera ---
        if (!brain)
        {
            var mainCam = Camera.main;
            if (mainCam)
                brain = mainCam.GetComponent<CinemachineBrain>();
        }

        // --- Find all virtual cameras in the scene by name ---
        if (!vcamFP)
            vcamFP = GameObject.Find("vcamFP")?.GetComponent<CinemachineVirtualCamera>();

        if (!vcamPerspective)
            vcamPerspective = GameObject.Find("vcamPerspective")?.GetComponent<CinemachineVirtualCamera>();

        if (!vcamOverhead)
            vcamOverhead = GameObject.Find("vcamOverhead")?.GetComponent<CinemachineVirtualCamera>();

        if (!vcamNose)
            vcamNose = GameObject.Find("vcamNose")?.GetComponent<CinemachineVirtualCamera>();

        EnsureFreeCamera();

        // --- Find the player model ---
        //if (!playerModel)
        //    playerModel = GameObject.Find("PlayerModel");

        // --- Verify everything was found ---
        Debug.Log(
            $"[CameraModeSwitcher] Initialized in scene '{SceneManager.GetActiveScene().name}'\n" +
            $"Brain: {(brain ? brain.name : "❌ None")}\n" +
            $"FP: {(vcamFP ? vcamFP.name : "❌ None")}\n" +
            $"Nose: {(vcamNose ? vcamNose.name : "❌ None")}\n" +
            $"Top: {(vcamPerspective ? vcamPerspective.name : "❌ None")}\n" +
            $"Overhead: {(vcamOverhead ? vcamOverhead.name : "❌ None")}\n"
            //$"Player: {(playerModel ? playerModel.name : "❌ None")}"
        );
    }

    void Start()
    {
        if (zoomPerspectiveInOnStartup)
            startupZoomRoutine = StartCoroutine(ZoomPerspectiveInOnStartup());

        //if (player == null) player = FindFirstObjectByType<Player>();
    }

    void Update()
    {
        // Temporary warning during development until we start doing cinematic sequences that would violate this.
        if (!freeCameraActive &&
            dir != null &&
            dir.playerPack != null &&
            dir.playerPack.packLeader != null &&
            target != null &&
            target != dir.playerPack.packLeader &&
            loggedTargetWarning == false)
        {
            Debug.LogWarning($"Cameras are NOT configured to target playerPack.packLeader ({dir.playerPack.packLeader.DisplayName}, but instead {target.DisplayName})");
            loggedTargetWarning = true;
        }

        if (WasFreeCameraTogglePressed())
        {
            if (freeCameraActive)
                freeCameraController?.FocusLeaderNow();
            else
                EnableFreeCamera();
        }

        if (freeCameraActive && WasFreeCameraExitPressed())
            DisableFreeCamera(restoreFollow: true);

        if (freeCameraActive)
        {
            playerVisible = true;
            if (dir != null && dir.playerPack != null && dir.playerPack.packLeader != null && dir.playerPack.packLeader.appearanceModule != null)
                dir.playerPack.packLeader.appearanceModule.SetVisible(true);
            return;
        }

        // ===> camera_refresh_needed 
        //if (target.appearanceModule.camera_refresh_needed)
        //{
            //if (waiter!=null) StopCoroutine(waiter);  // in case WaitForArrival was already running, kill it.

            playerVisible = (vcamFP.Priority == 10) ? false : true; // hide player in first person mode

            if (!playerVisible)
            {
                // Wait for camera to arrive at first person before disabling player visibility
                //waiter = StartCoroutine(WaitForArrival(vcamFP, onArrived: onArrivedAtFP));
            }
            else
            {
                //playerModel.SetActive(true);
                //player.agent.DogPrefab.SetActive(true);
                dir.playerPack.packLeader.appearanceModule.SetVisible(true);
                var mainCam = Camera.main;
                mainCam.cullingMask &= ~(1 << LayerMask.NameToLayer("Ceiling")); // hide ceiling in non-first person
            }
        //    target.appearanceModule.camera_refresh_needed = false;
        //}
    }

    public void SetViewTarget(WorldObject cameraTarget)
    {
        //if (target==cameraTarget) return; // nothing needed

        if ((target != cameraTarget) && (target != null) && (target.appearanceModule != null))
        {
            // tell the old target we aren't following it anymore.
            target.appearanceModule.cameraFollowingMe = false;
        }

        if (cameraTarget == null)
        {
            Debug.LogError("Camera target cannot be null.");
            return;
        }

        if (cameraTarget.appearanceModule == null)
        {
            Debug.LogError($"Camera target set to WorldObject {cameraTarget.DisplayName} which has no AppearanceModule attached.  Cameras not changed.");
            // Note that we could create one, but without the right safeguards, that might end in a nasty recursive loop.
            return;
        }

        // set target to new WorldObject (player), update all vcams,
        // and let agent know it is being followed.
        target = cameraTarget;
        
        dir.vcamFP.Follow = target.appearanceModule.head.transform;
        dir.vcamFP.LookAt = target.appearanceModule.eyesForward.transform;
        
        dir.vcamNose.Follow = target.appearanceModule.eyesForward.transform;
        dir.vcamNose.LookAt = target.appearanceModule.head.transform;

        dir.vcamOverhead.Follow = target.transform;
        dir.vcamOverhead.LookAt = target.transform;

        dir.vcamPerspective.Follow = target.transform;
        dir.vcamPerspective.LookAt = target.transform;

        target.appearanceModule.cameraFollowingMe = !freeCameraActive;
    }

    public void SelectView(CameraModes newMode)
    {
        if ((newMode != cameraMode) && (newMode != CameraModes.Unchanged))
        {
            if (freeCameraActive)
                DisableFreeCamera(restoreFollow: false);

            cameraMode = newMode;
            vcamPerspective.Priority = 0;
            vcamFP.Priority = 0;
            vcamOverhead.Priority = 0;
            vcamNose.Priority = 0;
            if (vcamFree != null)
                vcamFree.Priority = 0;
            playerVisible = true;
            target.appearanceModule.camera_refresh_needed = true;

            switch (cameraMode)
            {
                case CameraModes.Perspective:
                    vcamPerspective.Priority = 10;
                    cameraMode = CameraModes.Perspective;
                    break;
                case CameraModes.FP:
                    vcamFP.Priority = 10;
                    cameraMode = CameraModes.FP;
                    //playerVisible = false;   // hide player in first person mode
                    break;
                case CameraModes.Overhead:
                    vcamOverhead.Priority = 10;
                    cameraMode = CameraModes.Overhead;
                    break;
                case CameraModes.Nose:
                    vcamNose.Priority = 10;
                    cameraMode = CameraModes.Nose;
                    break;
            }
        }

        if (true)
        {
            //if (waiter!=null) StopCoroutine(waiter);  // in case WaitForArrival was already running, kill it.

            playerVisible = (cameraMode == CameraModes.FP) ? false : true; // hide player in first person mode

            if (!playerVisible)
            {
                // Wait for camera to arrive at first person before disabling player visibility
                waiter = StartCoroutine(WaitForArrival(vcamFP, onArrived: onArrivedAtFP));
            }
            else
            {
                //playerModel.SetActive(true);
                //player.agent.DogPrefab.SetActive(true);
                target.appearanceModule.SetVisible(true);
                var mainCam = Camera.main;
                mainCam.cullingMask &= ~(1 << LayerMask.NameToLayer("Ceiling")); // hide ceiling in non-first person
            }
            target.appearanceModule.camera_refresh_needed = false;
        }
    }  

    public void SelectNextView()
    {
        SelectView(GetNextViewMode(cameraMode));
    }

    public static CameraModes GetNextViewMode(CameraModes currentMode)
    {
        return currentMode switch
        {
            CameraModes.FP => CameraModes.Overhead,
            CameraModes.Overhead => CameraModes.Nose,
            CameraModes.Nose => CameraModes.Perspective,
            CameraModes.Perspective => CameraModes.FP,
            _ => CameraModes.FP
        };
    }

    IEnumerator WaitForArrival(ICinemachineCamera target, System.Action onArrived)
    {
        // let priorities propagate one frame
        yield return null;

        // Wait until the brain is not blending AND our target is actually live
        while (brain.ActiveBlend != null || !CinemachineCore.Instance.IsLive(target))
            yield return null;

        onArrived?.Invoke();
    }

    void onArrivedAtFP()
    {
        var mainCam = Camera.main;
        //player.agent.DogPrefab.SetActive(playerVisible);
        dir.playerPack.packLeader.appearanceModule.SetVisible(playerVisible);
        mainCam.cullingMask |= (1<<LayerMask.NameToLayer("Ceiling")); // show ceiling in first person
        return;
    }



/*
    void LateUpdate()
    {
        // all cameras point to current agent
        if (player == null || player.agent == null) return;
        
        vcamPerspective.transform.position = new Vector3(
                player.agent.transform.position.x,
                player.agent.height,
                player.agent.transform.position.z);
        // top camera override angle so north is top of screen
        vcamPerspective.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // always north up
    }
*/

    void Update_CameraHeight()
    {
        float delta = 0f;
        float step = 0.5f;
        bool continuous = true;

        // '+' is usually Shift+'=' on the main keyboard.
        bool plus = IsKeyboardKeyPressed(Key.Equals) || IsKeyboardKeyPressed(Key.NumpadPlus);
        bool minus = IsKeyboardKeyPressed(Key.Minus) || IsKeyboardKeyPressed(Key.NumpadMinus);

        if (continuous)
        {
            if (plus) delta += step * Time.deltaTime * 10f;
            if (minus) delta -= step * Time.deltaTime * 10f;
        }
        else
        {
            if (WasKeyboardKeyPressedThisFrame(Key.Equals) || WasKeyboardKeyPressedThisFrame(Key.NumpadPlus)) delta += step;
            if (WasKeyboardKeyPressedThisFrame(Key.Minus) || WasKeyboardKeyPressedThisFrame(Key.NumpadMinus)) delta -= step;
        }

        if (Mathf.Approximately(delta, 0f)) return;

        // Pick which of your three is currently live
        CinemachineVirtualCamera targetVcam = GetLiveVCam();
        if (targetVcam == null) return;

        // Adjust according to body type
        var transposer = targetVcam.GetCinemachineComponent<CinemachineTransposer>();
        if (transposer != null)
        {
            var off = transposer.m_FollowOffset;
            off.y += delta;
            transposer.m_FollowOffset = off;
            return;
        }

        // Hard Lock to Target: use Camera Offset extension for "height"
        var camOffset = targetVcam.GetComponent<CinemachineCameraOffset>();
        if (camOffset == null)
            camOffset = targetVcam.gameObject.AddComponent<CinemachineCameraOffset>();

        var o = camOffset.m_Offset;
        o.y += delta;
        camOffset.m_Offset = o;
    }

    CinemachineVirtualCamera GetLiveVCam()
    {
        // Prefer the one that is live according to Cinemachine
        if (IsLive(vcamFree)) return vcamFree;
        if (IsLive(vcamFP)) return vcamFP;
        if (IsLive(vcamPerspective)) return vcamPerspective;
        if (IsLive(vcamOverhead)) return vcamOverhead;
        if (IsLive(vcamNose)) return vcamNose;

        // Fallback: highest Priority
        CinemachineVirtualCamera best = null;
        int bestP = int.MinValue;
        foreach (var v in new[] { vcamFree, vcamFP, vcamPerspective, vcamOverhead, vcamNose })
        {
            if (v != null && v.Priority > bestP) { best = v; bestP = v.Priority; }
        }
        return best;
    }

    bool IsLive(CinemachineVirtualCamera v)
    {
        if (v == null || brain == null) return false;
        return CinemachineCore.Instance.IsLive(v);
    }

    [Header("Zoom Controls")]
    public float zoomStep = 1f;           // how fast to zoom
    public float minZoom = 2f;            // clamp limits
    public float maxZoom = 50f;
    public float minFOV = 30f;       // narrowest FOV
    public float maxFOV = 60f;       // widest FOV

    [Header("Startup Zoom")]
    [SerializeField] private bool zoomPerspectiveInOnStartup = true;
    [SerializeField] private float startupZoomDelay = 0.35f;
    [SerializeField] private float startupZoomDuration = 1.25f;

    public void ApplyZoomDelta(float delta)
    {
        Debug.Log($"ApplyZoomDelta({delta}) cameraMode={cameraMode}");

        delta *= zoomStep;  // scales zoom speed

        // 'x' = zoom in (closer), 'z' = zoom out (farther)
        //if (Input.GetKey(KeyCode.Equals) || Input.GetKey(KeyCode.Plus))
        //    delta -= zoomStep * Time.deltaTime * 10f;
        //if (Input.GetKey(KeyCode.Minus) || Input.GetKey(KeyCode.Underscore))
        //    delta += zoomStep * Time.deltaTime * 10f;

        if (Mathf.Approximately(delta, 0f))
            return;

        if (startupZoomRoutine != null)
        {
            StopCoroutine(startupZoomRoutine);
            startupZoomRoutine = null;
        }

        if (freeCameraActive && vcamFree != null)
        {
            float fov = vcamFree.m_Lens.FieldOfView;
            fov = Mathf.Clamp(fov + delta, minFOV, maxFOV);
            vcamFree.m_Lens.FieldOfView = fov;
            if (freeCameraController != null)
                freeCameraController.NotifyZoomChanged();
            return;
        }

        // --- First Person: change FOV ---
        if (IsLive(vcamFP))
        {
            float fov = vcamFP.m_Lens.FieldOfView;
            fov = Mathf.Clamp(fov + delta, minFOV, maxFOV);
            vcamFP.m_Lens.FieldOfView = fov;
            Debug.Log($"First Person zoom fov = {fov:0.0}");
        }

        // Top cam (Transposer): adjust FollowOffset.z for zoom effect
        if (IsLive(vcamPerspective))
        {
            ApplyPerspectiveZoomDelta(delta);
        }

        // Overhead cam (Transposer): keep old behavior = change height (FollowOffset.y)
        if (IsLive(vcamOverhead))
        {
            var transposer = vcamOverhead.GetCinemachineComponent<CinemachineTransposer>();
            if (transposer != null)
            {
                var off = transposer.m_FollowOffset;
                off.y = Mathf.Clamp(off.y + delta, minZoom, maxZoom);
                transposer.m_FollowOffset = off;
                Debug.Log($"Overhead zoom transposer.y = {off.y:0.0}");
            }
        }
    }

    private IEnumerator ZoomPerspectiveInOnStartup()
    {
        while (!freeCameraActive &&
               (dir == null ||
                dir.gen == null ||
                !dir.gen.buildComplete))
        {
            if (dir == null)
                dir = Dir.Instance ?? FindFirstObjectByType<Dir>();

            yield return null;
        }

        float waitStartedAt = Time.time;
        while (!freeCameraActive &&
               vcamPerspective != null &&
               !IsLive(vcamPerspective) &&
               Time.time - waitStartedAt < 5f)
        {
            yield return null;
        }

        if (freeCameraActive || !TryGetPerspectiveTransposer(out CinemachineTransposer transposer))
        {
            startupZoomRoutine = null;
            yield break;
        }

        if (startupZoomDelay > 0f)
            yield return new WaitForSeconds(startupZoomDelay);

        if (freeCameraActive)
        {
            startupZoomRoutine = null;
            yield break;
        }

        float startZ = transposer.m_FollowOffset.z;
        float targetZ = -minZoom;

        if (Mathf.Approximately(startZ, targetZ))
        {
            startupZoomRoutine = null;
            yield break;
        }

        float duration = Mathf.Max(0.01f, startupZoomDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (freeCameraActive || !TryGetPerspectiveTransposer(out transposer))
                break;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float smoothedT = Mathf.SmoothStep(0f, 1f, t);
            SetPerspectiveZoomZ(transposer, Mathf.Lerp(startZ, targetZ, smoothedT));
            yield return null;
        }

        if (!freeCameraActive && TryGetPerspectiveTransposer(out transposer))
            SetPerspectiveZoomZ(transposer, targetZ);

        startupZoomRoutine = null;
    }

    private void ApplyPerspectiveZoomDelta(float delta)
    {
        if (!TryGetPerspectiveTransposer(out CinemachineTransposer transposer))
            return;

        Vector3 off = transposer.m_FollowOffset;
        SetPerspectiveZoomZ(transposer, off.z + delta);
        off = transposer.m_FollowOffset;
        Debug.Log($"Perspective zoom transposer.z = {off.z:0.0} transposer.y = {off.y:0.0}");
    }

    private bool TryGetPerspectiveTransposer(out CinemachineTransposer transposer)
    {
        transposer = vcamPerspective != null
            ? vcamPerspective.GetCinemachineComponent<CinemachineTransposer>()
            : null;

        return transposer != null;
    }

    private void SetPerspectiveZoomZ(CinemachineTransposer transposer, float z)
    {
        Vector3 off = transposer.m_FollowOffset;
        off.z = Mathf.Clamp(z, -maxZoom, -minZoom);
        off.y = 5f - (off.z / 2f);
        transposer.m_FollowOffset = off;
    }

    public void ToggleFreeCamera()
    {
        if (freeCameraActive)
            DisableFreeCamera(restoreFollow: true);
        else
            EnableFreeCamera();
    }

    public void EnableFreeCamera()
    {
        EnsureFreeCamera();
        if (vcamFree == null)
            return;

        freeCameraRestoreMode = cameraMode;

        CopyMainCameraPoseToFreeCamera();

        if (target != null && target.appearanceModule != null)
            target.appearanceModule.cameraFollowingMe = false;

        if (target != null &&
            target.agentModule != null &&
            target.agentModule.currentDecisionModule != null &&
            target.agentModule.currentDecisionModule.DecisionType == AgentDecisionType.Player)
        {
            target.agentModule.SwitchDecisionModule(AgentDecisionType.Immobile);
        }

        vcamPerspective.Priority = 0;
        vcamFP.Priority = 0;
        vcamOverhead.Priority = 0;
        vcamNose.Priority = 0;
        vcamFree.Priority = (int)freeCameraPriority;

        freeCameraActive = true;
        if (freeCameraController != null)
            freeCameraController.SetActive(true);

        if (target != null && target.appearanceModule != null)
            target.appearanceModule.SetVisible(true);
    }

    public void DisableFreeCamera(bool restoreFollow)
    {
        if (vcamFree != null)
            vcamFree.Priority = 0;

        freeCameraActive = false;
        if (freeCameraController != null)
            freeCameraController.SetActive(false);

        if (restoreFollow)
        {
            if (target != null && target.appearanceModule != null)
                target.appearanceModule.cameraFollowingMe = true;

            SelectView(freeCameraRestoreMode);
        }
    }

    private void EnsureFreeCamera()
    {
        if (vcamFree == null)
            vcamFree = GameObject.Find("vcamFree")?.GetComponent<CinemachineVirtualCamera>();

        if (vcamFree == null)
        {
            GameObject freeCameraObject = new("vcamFree");
            freeCameraObject.transform.SetParent(transform, false);
            vcamFree = freeCameraObject.AddComponent<CinemachineVirtualCamera>();
            vcamFree.Priority = 0;
            vcamFree.Follow = null;
            vcamFree.LookAt = null;
        }

        freeCameraController = vcamFree.GetComponent<FreeCameraController>();
        if (freeCameraController == null)
            freeCameraController = vcamFree.gameObject.AddComponent<FreeCameraController>();

        freeCameraController.SetSwitcher(this);
        freeCameraController.SetActive(freeCameraActive);
    }

    private void CopyMainCameraPoseToFreeCamera()
    {
        if (vcamFree == null)
            return;

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            vcamFree.transform.SetPositionAndRotation(mainCamera.transform.position, mainCamera.transform.rotation);
            LensSettings lens = vcamFree.m_Lens;
            lens.FieldOfView = mainCamera.fieldOfView;
            lens.Orthographic = mainCamera.orthographic;
            lens.OrthographicSize = mainCamera.orthographicSize;
            lens.NearClipPlane = mainCamera.nearClipPlane;
            lens.FarClipPlane = mainCamera.farClipPlane;
            vcamFree.m_Lens = lens;
        }
        else
        {
            CinemachineVirtualCamera live = GetLiveVCam();
            if (live != null)
            {
                vcamFree.transform.SetPositionAndRotation(live.transform.position, live.transform.rotation);
                vcamFree.m_Lens = live.m_Lens;
            }
        }

        if (freeCameraController != null)
            freeCameraController.SnapToCurrentTransform();
    }

    private bool WasFreeCameraTogglePressed()
    {
        return WasKeyCodePressedThisFrame(freeCameraToggleKey);
    }

    private static bool WasFreeCameraExitPressed()
    {
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
    }

    private static bool IsKeyboardKeyPressed(Key key)
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && key != Key.None && keyboard[key].isPressed;
    }

    private static bool WasKeyboardKeyPressedThisFrame(Key key)
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && key != Key.None && keyboard[key].wasPressedThisFrame;
    }

    private static bool WasKeyCodePressedThisFrame(KeyCode keyCode)
    {
        return keyCode switch
        {
            KeyCode.BackQuote => WasKeyboardKeyPressedThisFrame(Key.Backquote),
            KeyCode.Tab => WasKeyboardKeyPressedThisFrame(Key.Tab),
            KeyCode.Escape => WasKeyboardKeyPressedThisFrame(Key.Escape),
            KeyCode.F1 => WasKeyboardKeyPressedThisFrame(Key.F1),
            KeyCode.F2 => WasKeyboardKeyPressedThisFrame(Key.F2),
            KeyCode.F3 => WasKeyboardKeyPressedThisFrame(Key.F3),
            KeyCode.F4 => WasKeyboardKeyPressedThisFrame(Key.F4),
            KeyCode.F5 => WasKeyboardKeyPressedThisFrame(Key.F5),
            KeyCode.F6 => WasKeyboardKeyPressedThisFrame(Key.F6),
            KeyCode.F7 => WasKeyboardKeyPressedThisFrame(Key.F7),
            KeyCode.F8 => WasKeyboardKeyPressedThisFrame(Key.F8),
            KeyCode.F9 => WasKeyboardKeyPressedThisFrame(Key.F9),
            KeyCode.F10 => WasKeyboardKeyPressedThisFrame(Key.F10),
            KeyCode.F11 => WasKeyboardKeyPressedThisFrame(Key.F11),
            KeyCode.F12 => WasKeyboardKeyPressedThisFrame(Key.F12),
            _ => false
        };
    }
}
