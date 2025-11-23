using UnityEngine;

// LocationModule allows placing an object precisely on the map (or in the world)
// Lots of helpers for position conversions between 2D/3D, int/float, world/grid space, etc.
public class LocationModule : WorldModule
{
    public Cell cell;           // current cell occupied
    public Vector3 pos3d_f;     // master position in map grid space (z=height)
    public float yawDeg = 0f;   // facing direction in degrees, 0 = north (+y), clockwise
    public Quaternion tilt = Quaternion.identity; // optional tilt (when on a slope, etc.)
        
        // position helpers (2D/3D, int/float, world/grid space) for convenience:
        public float x_f => pos3d_f.x;
        public float y_f => pos3d_f.y;
        public float z_f => pos3d_f.z;
        public float height_f => pos3d_f.z;
        public int x => Mathf.FloorToInt(pos3d_f.x);
        public int y => Mathf.FloorToInt(pos3d_f.y);
        public int z => Mathf.FloorToInt(pos3d_f.z);
        public int height => z;
        public Vector3Int pos3d => new(x, y, z);
        public Vector3 pos3d_world => new(x_f, z_f, y_f); // axis swapped
        public Vector2Int pos2 => new(x, y);
        public Vector2 pos2_f => new(x_f, y_f);
        public float yawRad => yawDeg * Mathf.Deg2Rad;

    public void SetGridPosition(float x, float y, float z)
    {
        pos3d_f = new Vector3(x, y, z);
        cell = dir.gen.GetCellFromHf((int)x, (int)y, (int)z, 50); 
    }

    public void SetWorldPosition(Vector3 worldPos)
    {
        pos3d_f = new Vector3(worldPos.x, worldPos.z, worldPos.y);
    }
}
