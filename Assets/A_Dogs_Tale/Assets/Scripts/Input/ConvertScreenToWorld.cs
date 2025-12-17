using UnityEngine;
using System;
using DogGame.Language;

public class ConvertScreenToWorld : MonoBehaviour
{
    [Header("References")]
    public Directory dir;
    public Camera mainCamera;          // assign Main Camera
    
    [Header("Grid / Map")]
    public Vector3 origin = Vector3.zero;       // world-space origin of cell (0,0)
    public float cellSize = 1f;                 // world units per cell
    public float maxSelectDistanceTiles = 5f;   // only allow 'selecting' close objects
    private float MaxSelectDistanceWorld => maxSelectDistanceTiles * cellSize;

    [Header("Raycast")]
    [Tooltip("FirstPerson blocking mask should include ceiling")]
    [SerializeField] public LayerMask FP_BlockingMask;           // FirstPerson view: set to your targetable layers, including ceilings
    [Tooltip("Overhead/Perspective blocking mask should NOT include ceilings")]
    [SerializeField] public LayerMask Overhead_BlockingMask;     // Overhead views: set to your targetable layers, exclude invisible ceilings
    [Tooltip("NavigationMask isn't used yet, but would control what we can walk on (floors, some items)")]
    [SerializeField] public LayerMask navigationMask;           // we don't use this for navigation. (yet)
    [Tooltip("ObjectMask should include layers where selectable objects sit")]
    [SerializeField] public LayerMask objectsMask;   // layers where objects can be selected (objects, not walls/floors)
    public float rayMaxDistance = 200f;
    
    [Header("Current Status")]
    public Vector3 screenPos;       // note: x,y is screen coordinate, z is unused
    
    public bool mapWorldPos_valid = false;  // valid flag for mapWorldPosCenter/mapWorldPos/targetedCell
    public Vector3 mapWorldPosCenter;     // map location targeted (center of tile)
    public Vector3 mapWorldPos;     // map location targeted (exact floor location)
    public Cell targetedCell;       // cell at mapWorldPos and/or targetedWorldObject
    
    public bool targetedWorldObject_valid = false;  // valid flag for targetedWorldObject
    public WorldObject targetedWorldObject;     // object targeted

    void Start()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }
    }


    // takes in a screen position, returns a world position (Y is height), or null if no hit.
    public Vector3 ?getWorldPointFromRaycast(Vector3 screenPosition)
    {
        if (mainCamera == null)
        {
            Debug.LogError("[ConvertScreenToWorld] No mainCamera set; cannot use raycast to convert screen cooudinates to world coordinates.");
            return null;
        }

        // Raycast to ground
        Ray ray = mainCamera.ScreenPointToRay(screenPosition);
        LayerMask groundMask = (dir.cameraModeSwitcher.cameraMode == CameraModes.FP) ? FP_BlockingMask : Overhead_BlockingMask;
        if (!Physics.Raycast(ray, out var hit, rayMaxDistance, groundMask))
        {
            // Optional: debug to see where you clicked
            Debug.DrawRay(ray.origin, ray.direction * 10f, Color.red, 0.25f);
            // Debug.Log("Click raycast missed ground.");
            return null;
        }

        // Convert world → cell
        //Vector3 p = hit.point;
        // Bias hit point toward camera so clicking walls always targets near cell
        Vector3 p = hit.point - ray.direction.normalized * 0.05f;  // bias amount adjustable
        int cx = Mathf.FloorToInt((p.x - origin.x) / cellSize);
        int cz = Mathf.FloorToInt((p.z - origin.z) / cellSize);

        // Bounds check
        if (cx < 0 || cz < 0 || cx >= dir.gen.cfg.mapWidth || cz >= dir.gen.cfg.mapHeight)
            return null;

        // Cell center (X,Z)
        float centerX = origin.x + (cx + 0.5f) * cellSize;
        float centerZ = origin.z + (cz + 0.5f) * cellSize;

        // Height Y
        //float y = agent.height * dir.gen.cfg.unitHeight;    // default height if no floor here
        //if (dir.gen.cellGrid != null)
        float y = SampleTiltedFloorY(new Vector2(centerX, centerZ), dir.gen.cellGrid); //TODO: use heightfield, not cellgrid

        Vector3 dest = new Vector3(centerX, y, centerZ);
        return dest;    // successful
    }

    // --- Tilted floor height sampling (uses Cell.tiltFloor & height) ---
    public float SampleTiltedFloorY(Vector2 worldXZ, Cell[,] grid)
    {
        int cx = Mathf.FloorToInt((worldXZ.x - origin.x) / cellSize);
        int cz = Mathf.FloorToInt((worldXZ.y - origin.z) / cellSize);
        int W = grid.GetLength(0);
        int H = grid.GetLength(1);
        if (cx < 0 || cz < 0 || cx >= W || cz >= H)
            return 0f;
            //return agent ? agent.pos2.y : 0f;   // out of map bounds

        var cell = grid[cx, cz];    // TODO: use heightfield, not grid

        // Plane normal from tilt
        Vector3 n = (cell.tiltFloor * Vector3.up).normalized;

        // Cell center point on plane at base height
        float centerX = origin.x + (cx + 0.5f) * cellSize;
        float centerZ = origin.z + (cz + 0.5f) * cellSize;
        Vector3 P0 = new Vector3(centerX, cell.height, centerZ);

        // Solve n·(X - P0)=0 for y, where X=(x,y,z)
        float ny = Mathf.Abs(n.y) < 1e-5f ? Mathf.Sign(n.y) * 1e-5f : n.y;
        float x = worldXZ.x;
        float z = worldXZ.y;
        float y = P0.y - (n.x * (x - P0.x) + n.z * (z - P0.z)) / ny;
        return y;
    }

    public WorldObject GetWorldObjectFromRaycast(Vector3 screenPosition)
    {
        Debug.Log($"GetWorldObjectFromRaycast({screenPosition})");
        if (mainCamera == null)
        {
            Debug.LogError("[ConvertScreenToWorld] No mainCamera set; cannot use raycast to convert screen cooudinates to world object.");
            return null;
        }
            // YES, this is an ugly chunk of code.  Someday it will be cleaned up, but it works for now...
            // This cleaned up code could also be used to unify FP_BlockingMask and Overhead_BlockingMask which is basically this same function.
        // Tweak objectsMask
        // if we are in FP(FirstPerson) view, then ceilings should block ray.
        // if we are Overhead or Perspective view, then ceilings shoul NOT block ray (looking down through them)
        LayerMask ceiling_check = (dir.cameraModeSwitcher.cameraMode == CameraModes.FP) ? FP_BlockingMask : Overhead_BlockingMask;
        int ceiling_bit_num = LayerMask.NameToLayer("Ceiling");
        int ceiling_set_mask = ceiling_check.value & 1<<ceiling_bit_num; 
        int finalMask = objectsMask.value & ~(1<<ceiling_bit_num) | ceiling_set_mask;
        //Debug.Log($"CeilingCheck: ObjectsMask before: {Convert.ToString(objectsMask.value, 2)}. ceiling_bit_num: {ceiling_bit_num}. ceiling_set_mask: {Convert.ToString(ceiling_set_mask, 2)}. final_mask: {Convert.ToString(finalMask,2)}.");        
        objectsMask.value = finalMask;
            // end ugly code

        Ray ray = mainCamera.ScreenPointToRay(screenPosition);
        //if (ray==null) Debug.LogError("ray == null");
        if (Physics.Raycast(ray, out RaycastHit hit, 1000f, objectsMask, QueryTriggerInteraction.Ignore))
        {
            // we should block/unblock ceiling based on 
            // this is the GameObject's name
          //  Debug.Log($"hit.collider = {hit.collider.name}");

            // Find the WorldObject on this hit (or its parents)
            WorldObject wo = hit.collider.GetComponent<WorldObject>();
            targetedWorldObject = wo;
            if (wo == null)
            {
                // Clicked something that isn't a WorldObject
                //Debug.Log("worldObject is null");
                targetedWorldObject_valid = false;
                return null;
            }
            targetedWorldObject_valid = true;
            Debug.Log($"Hit wo = {wo.DisplayName}");
            
            dir.demo_Speech.TestSpeak(wo,null);
            PromoteToPackMemberOptions options = new();
            WorldObjectAgentPromoter.PromoteToFollower(wo.gameObject, options);

            return wo;
        }
        else
        {
            Debug.Log($"Raycast returned false");
            // Clicked empty space
            return null;
        }
    }

    private bool CheckSelectionDistance(WorldObject currentSelection, WorldObject player)
    {
        if (currentSelection == null || player.transform == null)
            return false;

        float dist = Vector3.Distance(player.transform.position, currentSelection.transform.position);
        if (dist > MaxSelectDistanceWorld)
        {
            // Auto-unselect when we walk away
            return false;
        }
        return true;
    }

    public Cell ConvertWorldLocationToCell(Vector3 worldLocation)
    {
        int x = Mathf.FloorToInt(worldLocation.x);
        int y = Mathf.FloorToInt(worldLocation.y);
        int z = Mathf.FloorToInt(worldLocation.z);
        int threshold = 100;     // how tolerant should we be?  Maybe as high as a tall object or wall?
                                // TODO: maybe only allow actualZ to be 'below' clicked on z for tall objects?
        DungeonGenerator.NeighborMatch match;   // heightfield search result
        bool success;
        Room room;                             // heightfield only gives room number, not cell so we'll search that room
        Vector2Int pos2int = new(x,y);
        int actualZ;
        Cell cell = null;                       // return result from this variable
        success = dir.gen.hf.TryQueryAt(x, z, y, threshold, out match);     // SWAPPED
        if (success)
        {
            room = dir.gen.rooms[match.roomId]; // found room number that contains the Cell
            actualZ = match.z;                  // exact cell height.  Use it?
            cell = room.cells[match.cellId];
            //cell = room.cells.Find(c => c.pos == pos2int);  // find just by (x,y)
            if(cell==null) 
                Debug.LogWarning($"[ConvertWorldLocationToCell] Heightfield.TryQueryAt({x},{y},{z}) returned roomId={match.roomId}, cellId={match.cellId} with actualZ={actualZ} but we couldn't find a matching cell in that room.");
            else 
                Debug.Log($"[ConvertWorldLocationToCell] Heightfield.TryQueryAt({x},{y},{z}) returned roomId={match.roomId}, cellId={match.cellId} with actualZ={actualZ} where we found a cell at ({cell.pos})");
        }
        return cell;    // may return null if not found, but the heightfield said it is there.
    }
}
