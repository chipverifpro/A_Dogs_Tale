using UnityEngine;

public sealed class EmoteIconSpinner : MonoBehaviour
{
    [SerializeField] private Vector3 spinDegreesPerSecond = new(0f, 180f, 0f);

    public Vector3 SpinDegreesPerSecond
    {
        get => spinDegreesPerSecond;
        set => spinDegreesPerSecond = value;
    }

    void Update()
    {
        transform.Rotate(spinDegreesPerSecond * Time.deltaTime, Space.Self);
    }
}
