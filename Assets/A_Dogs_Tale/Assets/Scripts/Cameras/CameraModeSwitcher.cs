using System.Collections;
using Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CameraModeSwitcher : MonoBehaviour
{
    public enum CameraModes { FP, Overhead, Perspective };

    [Header("Current Modes")]
    public CameraModes cameraMode = CameraModes.Perspective;    // More readable version of current_camera for use in other systems
    //public bool scentFogVisible = false;  //TODO: move contols/setup into camera script
    public bool playerVisible = true;

    public CinemachineBrain brain;
    public CinemachineVirtualCamera vcamFP, vcamPerspective, vcamOverhead;
    public GameObject playerModel;
    public KeyCode toggleKey = KeyCode.Tab;
    int current_camera;                     //TODO: replace with cameraMode above.
    //public Transform player;
    public Player player;
    public float height = 20f;

    private Coroutine waiter = null;


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

        // --- Find the player model ---
        if (!playerModel)
            playerModel = GameObject.Find("PlayerModel");

        // --- Verify everything was found ---
        Debug.Log(
            $"[CameraModeSwitcher] Initialized in scene '{SceneManager.GetActiveScene().name}'\n" +
            $"Brain: {(brain ? brain.name : "❌ None")}\n" +
            $"FP: {(vcamFP ? vcamFP.name : "❌ None")}\n" +
            $"Top: {(vcamPerspective ? vcamPerspective.name : "❌ None")}\n" +
            $"Overhead: {(vcamOverhead ? vcamOverhead.name : "❌ None")}\n" +
            $"Player: {(playerModel ? playerModel.name : "❌ None")}"
        );
    }

    void Start()
    {
        if (player == null) player = FindFirstObjectByType<Player>();
    }

    void Update()
    {
        UpdateZoom();

        if (Input.GetKeyDown(toggleKey))
        {
            current_camera = (current_camera + 1) % 3;
            vcamPerspective.Priority = 0;
            vcamFP.Priority = 0;
            vcamOverhead.Priority = 0;
            playerVisible = true;
            player.camera_refresh_needed = true;

            switch (current_camera)
            {
                case 0:
                    vcamPerspective.Priority = 10;
                    cameraMode = CameraModes.Perspective;
                    break;
                case 1:
                    vcamFP.Priority = 10;
                    cameraMode = CameraModes.FP;
                    //playerVisible = false;   // hide player in first person mode
                    break;
                case 2:
                    vcamOverhead.Priority = 10;
                    cameraMode = CameraModes.Overhead;
                    break;
            }
        }

        if (player.camera_refresh_needed)
        {
            //if (waiter!=null) StopCoroutine(waiter);  // in case WaitForArrival was already running, kill it.

            playerVisible = (vcamFP.Priority == 10) ? false : true; // hide player in first person mode

            if (!playerVisible)
            {
                // Wait for camera to arrive at first person before disabling player visibility
                waiter = StartCoroutine(WaitForArrival(vcamFP, onArrived: onArrivedAtFP));
            }
            else
            {
                //playerModel.SetActive(true);
                player.agent.DogPrefab.SetActive(true);
                var mainCam = Camera.main;
                mainCam.cullingMask &= ~(1 << LayerMask.NameToLayer("Ceiling")); // hide ceiling in non-first person
            }
            player.camera_refresh_needed = false;
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
        player.agent.DogPrefab.SetActive(playerVisible);
        mainCam.cullingMask |= (1<<LayerMask.NameToLayer("Ceiling")); // show ceiling in first person
        return;
    }




    void LateUpdate()
    {
        // all cameras point to current agent
        if (player == null) return;
        vcamPerspective.transform.position = new Vector3(
            player.agent.transform.position.x,
            player.agent.height,
            player.agent.transform.position.z
        );
        // top camera override angle so north is top of screen
        vcamPerspective.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // always north up
    }


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

        // Fallback: highest Priority
        CinemachineVirtualCamera best = null;
        int bestP = int.MinValue;
        foreach (var v in new[] { vcamFP, vcamPerspective, vcamOverhead })
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


    void UpdateZoom()
    {
        float delta = 0f;

        // '+' = zoom in (closer), '-' = zoom out (farther)
        if (Input.GetKey(KeyCode.Equals) || Input.GetKey(KeyCode.Plus))
            delta -= zoomStep * Time.deltaTime * 10f;
        if (Input.GetKey(KeyCode.Minus) || Input.GetKey(KeyCode.Underscore))
            delta += zoomStep * Time.deltaTime * 10f;

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
