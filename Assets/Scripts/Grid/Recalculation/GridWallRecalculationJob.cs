using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public partial struct GridRoomRecalculationJob : IJobEntity
{
    public RoomsLookup roomLookup;
    void Execute(Entity e, in GridUpdate update, in GridStructure grid)
    {
        var bbox = grid.LocalAABB;
        var walls = roomLookup.Walls.GetValuesForKey(e);
        var toCheck = new NativeQueue<int3>(Allocator.Temp);
        var checkedCells = new NativeHashMap<int3, int>(0, Allocator.Temp);
        var sameRoomMap = new NativeHashSet<(int, int)>(0, Allocator.Temp);
        var roomRemapMap = new NativeHashMap<int, int>(0, Allocator.Temp);
        
        // Flood-fill grid, starting with all neighbours or walls
        roomLookup.GridRooms.Remove(e);
        while (walls.MoveNext())
        {
            foreach (var neighbour in Neighbours)
                toCheck.Enqueue(walls.Current + neighbour);
            checkedCells.Add(walls.Current, -1);
        }

        // Assign cell room values depending on neighbours
        var nextRoom = 0;
        while (toCheck.TryDequeue(out var cell))
        {
            if(!bbox.Contains(cell) || checkedCells.ContainsKey(cell))
                continue;

            var lastNeighbour = 0;
            foreach (var neighbour in Neighbours)
            {
                var otherCell = cell + neighbour;
                if (!checkedCells.TryGetValue(otherCell, out var value))
                {
                    toCheck.Enqueue(otherCell);
                    continue;
                }

                if (value == -1) continue;
                // If cell has multiple neighbours with different rooms,
                // Mark rooms as the same
                if(lastNeighbour != value && lastNeighbour != 0)
                    sameRoomMap.Add((lastNeighbour, value));
                lastNeighbour = value;
            }

            if (lastNeighbour == 0)
                lastNeighbour = ++nextRoom;
            
            checkedCells.Add(cell, lastNeighbour);
        }

        // Combine neighbouring rooms
        roomRemapMap.Add(-1, -1);
        foreach (var remap in sameRoomMap.ToNativeArray(Allocator.Temp))
        {
            if (!roomRemapMap.TryGetValue(remap.Item1, out var newRoom) &&
                !roomRemapMap.TryGetValue(remap.Item2, out newRoom))
                newRoom = ++nextRoom;
            roomRemapMap[remap.Item1] = newRoom;
            roomRemapMap[remap.Item2] = newRoom;
        }
        
        // Calculate final room AABBs
        var finalRoomMap = new NativeHashMap<int, Room>(0, Allocator.Temp);
        foreach (var cell in checkedCells.GetKeyArray(Allocator.Temp))
        {
            var roomId = checkedCells[cell];
            if(roomId == -1)
                continue;
            roomId = roomRemapMap.TryGetValue(roomId, out var remap) ? remap : roomId;
            roomLookup.CellRoomMap[(e, cell)] = roomId;
            var aabb = new IntAABB { Max = cell, Min = cell };
            if (finalRoomMap.TryGetValue(roomId, out var existingRoom))
                aabb = new IntAABB
                {
                    Max = math.max(aabb.Max, existingRoom.BoundingBox.Max),
                    Min = math.min(aabb.Min, existingRoom.BoundingBox.Min)
                };
            finalRoomMap[roomId] = new Room { BoundingBox = aabb };
        }
        
        // Add final rooms to roomLookup
        foreach (var kvp in finalRoomMap)
        {
            if(math.any(kvp.Value.BoundingBox.Min == grid.LocalAABB.Min | kvp.Value.BoundingBox.Max == grid.LocalAABB.Max))
                continue;
            
            roomLookup.GridRooms.Add(e, (kvp.Key, kvp.Value));
        }

    }
    
    private static readonly int3[] Neighbours =
    {
        new(1, 0, 0), new(0, 1, 0), new(0, 0, 1),
        new(-1, 0, 0), new(0, -1, 0), new(0, 0, -1)
    };
}