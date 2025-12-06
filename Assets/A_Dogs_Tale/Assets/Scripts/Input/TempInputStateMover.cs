using UnityEngine;

public class TempInputStateMover : MonoBehaviour
{
    [SerializeField] private NewInputAdapter inputAdapter;
    [SerializeField] private float moveSpeed = 5f;

    private PlayerInputState inputState;

    private void Start()
    {
        if (inputAdapter == null)
        {
            inputAdapter = FindFirstObjectByType<NewInputAdapter>();
            if (inputAdapter == null)
            {
                Debug.LogError("[TempInputStateMover] No NewInputAdapter found in scene.", this);
                enabled = false;
                return;
            }
        }

        inputState = inputAdapter.InputState;
        if (inputState == null)
        {
            Debug.LogError("[TempInputStateMover] InputState is null on NewInputAdapter.", this);
            enabled = false;
        }
    }

    private void Update()
    {
        if (inputState == null)
            return;

        // Use moveVector from PlayerInputState: assumed to be camera-relative or world-relative WASD/stick
        Vector2 move = inputState.moveAxis;

        // Basic XZ plane movement, no rotation logic yet
        Vector3 move3 = new Vector3(move.x, 0f, move.y);

        if (move3.sqrMagnitude > 0.0001f)
        {
            transform.position += move3 * moveSpeed * Time.deltaTime;
            Debug.Log($"TempInputStateMover: move {move3}");
        }
    }
}