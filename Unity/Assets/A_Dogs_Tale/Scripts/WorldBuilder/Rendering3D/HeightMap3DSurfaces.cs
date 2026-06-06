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
        const float floorVisualYOffset = -0.5f;
        Quaternion tilt = cell.tiltFloor;

        float rollRadians = tilt.eulerAngles.z * Mathf.Deg2Rad;
        float pitchRadians = tilt.eulerAngles.x * Mathf.Deg2Rad;
        float cosRoll = Mathf.Cos(rollRadians);
        float cosPitch = Mathf.Cos(pitchRadians);
        float scaleX = (Mathf.Abs(cosRoll) > 1e-4f) ? 1f / cosRoll : 1f;
        float scaleZ = (Mathf.Abs(cosPitch) > 1e-4f) ? 1f / cosPitch : 1f;

        if (floorPrefab != null && triangleFloorPrefab != null)
        {
            Vector3 position = world + new Vector3(0f, ySteps * cfg.unitHeight + floorVisualYOffset, 0f);
            Vector3 finalScale = new Vector3(scaleX, 1f, scaleZ);

            Color checkerboardColor = (Mathf.Floor(world.x + world.z) % 2 == 0) ? Color.white : Color.black;
            colorFloor = (colorFloor * (1 - checkerFloorStrength)) + (checkerboardColor * checkerFloorStrength);

            if (useTriangleFloor)
            {
                int resolvedTriangleFloorDirection = ResolveTriangleFloorDirection(triangleFloorDirection);
                Quaternion triangleFloorRotation = Quaternion.Euler(-90f, resolvedTriangleFloorDirection * 90f, 90f);
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
            float ceilingY = ySteps * cfg.unitHeight + rooms[roomNumber].ceilingHeight + ceilingZOffset;
            Vector3 ceilingPosition = world + new Vector3(0f, ceilingY, 0f);
            Quaternion ceilingRotation = tilt * Quaternion.Euler(90f, 0f, 0f);
            Vector3 ceilingScale = new Vector3(scaleX, scaleZ, 1f);

            elementStore.AddCeilingTile(
                archetypeId: "Ceiling",
                roomIndex: roomNumber,
                cellCoord: new Vector2Int(x, z),
                heightSteps: ySteps,
                worldPos: ceilingPosition,
                rotation: ceilingRotation,
                scale: ceilingScale,
                color: rooms[roomNumber].colorCeiling
            );
        }
    }

    private int ResolveTriangleFloorDirection(int triangleFloorDirection)
    {
        triangleFloorDirection = ((triangleFloorDirection % 4) + 4) % 4;

        if (cfg != null && (!cfg.enableTiltedTiles || cfg.tiltFloorTilesMaxAngle == 0))
            return (triangleFloorDirection + 2) % 4;

        return triangleFloorDirection;
    }
}
