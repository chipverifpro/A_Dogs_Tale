using UnityEngine;

public class LeashVisualizer : MonoBehaviour
{
    // Which local axis represents "length"
    // Z is common; change if your mesh uses Y.
    private static readonly Vector3 LengthAxis = Vector3.forward;

    public void SetEndpoints(Vector3 a, Vector3 b)
    {
        a.y += 0.3f;  // move both endpoints up off the floor
        b.y += 0.3f;

        Vector3 delta = b - a;
        float length = delta.magnitude;

        if (length < 0.0001f)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

        // Position at midpoint
        transform.position = (a + b) * 0.5f;

        // Rotate so forward points from A to B
        transform.rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);

        // Scale to match length (assumes unit length mesh)
        Vector3 scale = transform.localScale;
        scale.z = length;
        scale.y = 0.05f;
        transform.localScale = scale;
    }
}