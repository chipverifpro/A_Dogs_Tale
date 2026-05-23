using System.Collections.Generic;
using DogGame.Modules;
using DogGame.Tasks;
using UnityEngine;

namespace DogGame.Lua
{
    public class RoomState
    {
        public bool IsValid = false;
        public int Id = -1;
        public string Name = "";
        public List<int> Doors = new();
        public int DoorCount = 0;
        private readonly List<(int doorId, float distSqr)> sortedDoors = new();

        public WorldObject worldObject;
        public AgentState state;

        public void InitState(WorldObject worldObject, AgentState state)
        {
            this.worldObject = worldObject;
            this.state = state;
        }

        public void UpdateState(Detail detail)
        {
            IsValid = false;
            Id = -1;
            Name = "";
            Doors.Clear();
            DoorCount = 0;

            if (worldObject == null || worldObject.locationModule == null)
                return;

            Cell currentCell = worldObject.locationModule.cell;
            if (currentCell == null)
                return;

            if (worldObject.dir == null || worldObject.dir.gen == null || worldObject.dir.gen.rooms == null)
                return;

            int roomIndex = currentCell.room_number;
            if (roomIndex < 0 || roomIndex >= worldObject.dir.gen.rooms.Count)
                return;

            Room room = worldObject.dir.gen.rooms[roomIndex];
            if (room == null)
                return;

            Vector3 currentMap = worldObject.pos3d_map;
            sortedDoors.Clear();

            for (int i = 0; i < room.cells.Count; i++)
            {
                Cell cell = room.cells[i];
                if (cell.doors == DirFlags.None)
                    continue;

                foreach (DirFlags direction in DirFlagsEx.AllCardinals)
                {
                    if ((cell.doors & direction) == 0)
                        continue;

                    int doorId = DoorIdUtility.Build(cell.pos, direction);
                    Vector3 doorMap = cell.center3d_f;
                    sortedDoors.Add((doorId, (doorMap - currentMap).sqrMagnitude));
                }
            }

            sortedDoors.Sort((a, b) => a.distSqr.CompareTo(b.distSqr));

            for (int i = 0; i < sortedDoors.Count; i++)
                Doors.Add(sortedDoors[i].doorId);

            DoorCount = Doors.Count;
            Id = roomIndex;
            Name = room.name ?? "";
            IsValid = true;
        }

        public int GetDoorId(int oneBasedIndex)
        {
            int zeroBasedIndex = oneBasedIndex - 1;
            if (zeroBasedIndex < 0 || zeroBasedIndex >= Doors.Count)
                return -1;

            return Doors[zeroBasedIndex];
        }

        public void Tick(float interval)
        {
            DoorCount = Doors.Count;
        }
    }
}
