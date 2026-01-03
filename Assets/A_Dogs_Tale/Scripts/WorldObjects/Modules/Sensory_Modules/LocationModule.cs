using UnityEngine;
using Unity.Mathematics;

/* 
LocationModule is the sensor.

It should not move the dog at all.
It should inform MotionModule and AgentMovementModule.

LocationModule can answer questions MotionModule cannot, such as:
	•	“What cell am I standing in?”
	•	“Is this walkable floor?”
	•	“Is the ground sloped?”
	•	“Am I on a staircase? Ramp?”
	•	“Should I perform a landing animation?”
	•	“What is the world y-offset for snapped ground height?”
	•	“What objects can I interact with from here?”
	•	“Am I inside a certain region or zone?”
	•	“Should the minimap show this spot?”
	•	“Is the dog’s current pose above/below ground?”
    */

namespace DogGame.Modules
{
    public class LocationModule : WorldModule
    {
        public Vector3 pos3d_world => this.transform.position;

        // translate and decompose pos3d_world into map space
        public float x_f => pos3d_world.x;
        public float y_f => pos3d_world.z;  // map Y is world Z
        public float z_f => pos3d_world.y;  // map Z (height) is world Y
        public float height_f => z_f;

        public int x => Mathf.FloorToInt(x_f);
        public int y => Mathf.FloorToInt(y_f);
        public int z => Mathf.FloorToInt(z_f);
        public int height => z;

        public Vector3 pos3d_f => new(x_f, y_f, z_f);
        public Vector3Int pos3d => new(x, y, z);
        public Vector2 pos2_f => new(x_f, y_f);
        public Vector2Int pos2 => new(x, y);

        public Cell cell => dir.gen.GetCellFromHf(x, y, z, 50);

        /// <summary>
        /// Full world rotation (includes yaw + pitch/roll tilt).
        /// If you only want "tilt without yaw", see TiltNoYaw below.
        /// </summary>
        public quaternion tilt => (quaternion)transform.rotation;

        /// <summary>
        /// Facing direction in degrees, 0 = north (+mapY = +worldZ), clockwise.
        /// Computed from the transform forward projected onto the ground plane.
        /// </summary>
        public float yawDeg
        {
            get
            {
                Vector3 forward = transform.forward;
                forward.y = 0f; // remove vertical component

                if (forward.sqrMagnitude < 1e-8f)
                    return 0f;

                forward.Normalize();

                // 0° when forward points to +worldZ (map +Y).
                // Clockwise means +worldX should be +90°.
                float yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;

                // Normalize to [0, 360)
                if (yaw < 0f) yaw += 360f;

                return yaw;
            }
        }

        public float yawRad => yawDeg * Mathf.Deg2Rad;

        /// <summary>
        /// Optional helper: tilt rotation with yaw removed (pitch/roll only).
        /// Useful if you want "Up on slope" independent of facing direction.
        /// </summary>
        public quaternion TiltNoYaw
        {
            get
            {
                // Remove yaw by premultiplying inverse yaw rotation.
                float yaw = yawDeg;
                Quaternion yawRot = Quaternion.Euler(0f, yaw, 0f);
                Quaternion tiltOnly = transform.rotation * Quaternion.Inverse(yawRot);
                return (quaternion)tiltOnly;
            }
        }
    }
}