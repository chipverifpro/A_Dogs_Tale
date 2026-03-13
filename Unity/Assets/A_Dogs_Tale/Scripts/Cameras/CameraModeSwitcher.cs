using System.Collections;
using Cinemachine;
using UnityEngine;
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
    public CinemachineVirtualCamera vcamFP, vcamPerspective, vcamOverhead, vcamNose;
    //public GameObject playerModel;
    public KeyCode toggleKey = KeyCode.Tab;
    public WorldObject target;
    public float height = 20f;

    private Coroutine waiter = null;
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

        // --- Find the player model ---
        //if (!playerModel)
        //    playerModel = GameObject.Find("PlayerModel");

        // --- Verify everything was found ---
        Debug.Log(
            $"[CameraModeSwitcher] Initialized in scene '{SceneManager.GetActiveScene().name}'\n" +
            $"Brain: {(brain ? brain.name : "❌ None")}\n" +
            $"FP: {(vcamFP ? vcamFP.name : "❌ None")}\n" +
            $"FP: {(vcamNose ? vcamNose.name : "❌ None")}\n" +
            $"Top: {(vcamPerspective ? vcamPerspective.name : "❌ None")}\n" +
            $"Overhead: {(vcamOverhead ? vcamOverhead.name : "❌ None")}\n"
            //$"Player: {(playerModel ? playerModel.name : "❌ None")}"
        );
    }

    void Start()
    {
        //if (player == null) player = FindFirstObjectByType<Player>();
    }

    void Update()
    {
        // Temporary warning during development until we start doing cinematic sequences that would violate this.
        if(target != dir.playerPack.packLeader && loggedTargetWarning==false)
        {
            Debug.LogWarning($"Cameras are NOT configured to target playerPack.packLeader ({dir.playerPack.packLeader.DisplayName}, but instead {target.DisplayName})");
            loggedTargetWarning = true;
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

        if (target.appearanceModule == null) 
        {
            Debug.LogError($"Camera target set to WorldObject {target.DisplayName} which has no AppearanceModule attached.  Cameras not changed.");
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
    
        target.appearanceModule.cameraFollowingMe = true;
    }

    public void SelectView(CameraModes newMode)
    {
        if ((newMode != cameraMode) && (newMode != CameraModes.Unchanged))
        {
            cameraMode = newMode;
            vcamPerspective.Priority = 0;
            vcamFP.Priority = 0;
            vcamOverhead.Priority = 0;
            vcamNose.Priority = 0;
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

        // '+' is usually Shift+'='
        bool plus = Input.GetKey(KeyCode.Equals) || Input.GetKey(KeyCode.Plus);
        bool minus = Input.GetKey(KeyCode.Minus) || Input.GetKey(KeyCode.Underscore);

        if (continuous)
        {
            if (plus) delta += step * Time.deltaTime * 10f;
            if (minus) delta -= step * Time.deltaTime * 10f;
        }
        else
        {
            if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.Plus)) delta += step;
            if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.Underscore)) delta -= step;
        }

        if (Mathf.Approximately(delta, 0f)) return;

        // Pick which of your three is currently live
        CinemachineVirtualCamera targetVcam = GetLiveOfThree();
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

    CinemachineVirtualCamera GetLiveOfThree()
    {
        // Prefer the one that is live according to Cinemachine
        if (IsLive(vcamFP)) return vcamFP;
        if (IsLive(vcamPerspective)) return vcamPerspective;
        if (IsLive(vcamOverhead)) return vcamOverhead;
        if (IsLive(vcamNose)) return vcamNose;

        // Fallback: highest Priority
        CinemachineVirtualCamera best = null;
        int bestP = int.MinValue;
        foreach (var v in new[] { vcamFP, vcamPerspective, vcamOverhead, vcamNose })
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
    public float zoomStep = 2f;           // how fast to zoom
    public float minZoom = 2f;            // clamp limits
    public float maxZoom = 50f;
    public float minFOV = 30f;       // narrowest FOV
    public float maxFOV = 60f;       // widest FOV


    public void ApplyZoomDelta(float delta)
    {
        Debug.Log($"ApplyZoomDelta({delta}) cameraMode={cameraMode}");
        //float delta = 0f;

        // '+' = zoom in (closer), '-' = zoom out (farther)
        //if (Input.GetKey(KeyCode.Equals) || Input.GetKey(KeyCode.Plus))
        //    delta -= zoomStep * Time.deltaTime * 10f;
        //if (Input.GetKey(KeyCode.Minus) || Input.GetKey(KeyCode.Underscore))
        //    delta += zoomStep * Time.deltaTime * 10f;

        if (Mathf.Approximately(delta, 0f))
            return;

        // --- First Person: change FOV ---
        if (vcamFP)
        {
            float fov = vcamFP.m_Lens.FieldOfView;
            fov = Mathf.Clamp(fov + delta, minFOV, maxFOV);
            vcamFP.m_Lens.FieldOfView = fov;
        }

        // Top cam (Transposer): adjust FollowOffset.z for zoom effect
        if (vcamPerspective)
        {
            var transposer = vcamPerspective.GetCinemachineComponent<CinemachineTransposer>();
            if (transposer != null)
            {
                var off = transposer.m_FollowOffset;
                off.z = Mathf.Clamp(off.z + delta, -maxZoom, -minZoom);
                transposer.m_FollowOffset = off;
            }
        }

        // Overhead cam (Transposer): keep old behavior = change height (FollowOffset.y)
        if (vcamOverhead)
        {
            var transposer = vcamOverhead.GetCinemachineComponent<CinemachineTransposer>();
            if (transposer != null)
            {
                var off = transposer.m_FollowOffset;
                off.y += delta;
                transposer.m_FollowOffset = off;
            }
        }
    }
}
