using UnityEngine;

public partial class DungeonGenerator
{
    void AddSurfaceTiles(
        int roomNumber,
        Cell cell,
        Vector3 world,
        Color colorFloor,
        bool useTriangleFloor,
        int triangleFloorDirection)
    {
        int x = cell.x;
        int z = cell.y;
        int ySteps = cell.height;

        if (floorPrefab != null && triangleFloorPrefab != null)
        {
            Quaternion tilt = cell.tiltFloor;
            Vector3 position = world + new Vector3(0f, ySteps * cfg.unitHeight, 0f);

            float rollRadians = tilt.eulerAngles.z * Mathf.Deg2Rad;
            float pitchRadians = tilt.eulerAngles.x * Mathf.Deg2Rad;
            float cosRoll = Mathf.Cos(rollRadians);
            float cosPitch = Mathf.Cos(pitchRadians);
            float scaleX = (Mathf.Abs(cosRoll) > 1e-4f) ? 1f / cosRoll : 1f;
            float scaleZ = (Mathf.Abs(cosPitch) > 1e-4f) ? 1f / cosPitch : 1f;

            Vector3 finalScale = new Vector3(scaleX, 1f, scaleZ);

            Color checkerboardColor = (Mathf.Floor(world.x + world.z) % 2 == 0) ? Color.white : Color.black;
            colorFloor = (colorFloor * (1 - checkerFloorStrength)) + (checkerboardColor * checkerFloorStrength);

            if (useTriangleFloor)
            {
                Quaternion triangleFloorRotation = Quaternion.Euler(-90f, triangleFloorDirection * 90f +180f, 90f);
                Quaternion finalRotation = tilt * triangleFloorRotation;
                Vector3 triangleScale = finalScale * 50f; // keep your existing fudge factor for now

                elementStore.AddFloorTile(
                    archetypeId: "TriangleFloor",
                    isTriangle: true,
                    roomIndex: roomNumber,
                    cellCoord: new Vector2Int(x, z),
                    heightSteps: ySteps,
                    worldPos: position,
                    rotation: finalRotation,
                    scale: triangleScale,
                    color: colorFloor
                );
            }
            else
            {
                elementStore.AddFloorTile(
                    archetypeId: "Floor",
                    isTriangle: false,
                    roomIndex: roomNumber,
                    cellCoord: new Vector2Int(x, z),
                    heightSteps: ySteps,
                    worldPos: position,
                    rotation: tilt,
                    scale: finalScale,
                    color: colorFloor
                );
            }
        }

        if (rooms[roomNumber].ceilingHeight > 0f)
        {
            float ceilingY = rooms[roomNumber].ceilingHeight + ceilingZOffset;
            Vector3 ceilingPosition = world + new Vector3(0f, ceilingY, 0f);

            elementStore.AddCeilingTile(
                archetypeId: "Ceiling",
                roomIndex: roomNumber,
                cellCoord: new Vector2Int(x, z),
                worldPos: ceilingPosition,
                rotation: Quaternion.Euler(90f, 0f, 0f),
                scale: new Vector3(1f, 1f, 1f),
                color: rooms[roomNumber].colorCeiling
            );
        }
    }
}
