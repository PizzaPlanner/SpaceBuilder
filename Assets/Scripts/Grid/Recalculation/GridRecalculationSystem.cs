using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public struct IntAABB
{
    public int3 Min;
    public int3 Max;

    public int Size => (Max - Min).x * (Max - Min).y * (Max - Min).z;

    public bool Contains(int3 cell) => math.all(cell >= Min) && math.all(cell <= Max);
    
    public static implicit operator IntAABB(BoundingBox bbox) => new IntAABB { Min = bbox.Min, Max = bbox.Max };
}

[UpdateInGroup(typeof(GridRecalculationSystemGroup), OrderFirst = true)]
public partial class GridRecalculationSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<GridUpdate>();
    }

    protected override void OnUpdate()
    {
        foreach(var (update, grid, roomsRO) in 
                SystemAPI.Query<GridUpdate, RefRW<GridStructure>, DynamicBuffer<Rooms>>())
        {
            Debug.Log("Recalculating Grid!");
            // Update bounds
            grid.ValueRW.BoundsMin = math.min(update.Area.Min, grid.ValueRW.BoundsMin);
            grid.ValueRW.BoundsMin = math.max(update.Area.Max, grid.ValueRW.BoundsMax);

            // Update Rooms
            var rooms = roomsRO;
            var bbox = update.Area;
            var exposedRooms = new NativeList<short>(Allocator.TempJob);
            var cellToRoomMap = new NativeParallelHashMap<int3, short>(bbox.Size, Allocator.TempJob);
            var cellsNeedUpdate = new NativeQueue<int3>(Allocator.TempJob);
            for (int x = bbox.Min.x; x <= bbox.Max.x; x++)
            for (int y = bbox.Min.y; y <= bbox.Max.y; y++)
            for (int z = bbox.Min.z; z <= bbox.Max.z; z++)
            {
                var point = new int3(x, y, z);
                cellsNeedUpdate.Enqueue(point);
                cellToRoomMap[point] = short.MaxValue;
                foreach(var room in rooms)
                    if (room.Room.Contains(point))
                        cellToRoomMap[point] = room.Room.Id;
            }
            
            var currentRoom = (short)0;
            while(cellsNeedUpdate.TryDequeue(out var cell))
                RoomSystem.FindRoom(ref currentRoom, cell, bbox, cellToRoomMap, cellsNeedUpdate);
            
            foreach (var item in cellToRoomMap)
                if (math.any(item.Key == bbox.Min | item.Key == bbox.Max) && !exposedRooms.Contains(item.Value))
                    exposedRooms.Add(item.Value);
            
            foreach (var item in cellToRoomMap)
            {
                if (exposedRooms.Contains(item.Value))
                    continue;
                bool roomListed = false;
                for (int i = 0; i < rooms.Length; i++)
                {
                    if (rooms[i].Room.Id != item.Value)
                        continue;
                        
                    roomListed = true;
                    var r = rooms[i].Room;
                    // r.Cells.Add(item.Key);
                    r.BoundingBox.Min = math.min(r.BoundingBox.Min, item.Key);
                    r.BoundingBox.Max = math.max(r.BoundingBox.Max, item.Key);
                    rooms[i] = new Rooms{Room = r};
                }
                
                if (!roomListed)
                {
                    Debug.Log($"MAKING NEW ROOM #{item.Value}");   
                    rooms.Add(new Rooms{ Room = new Room
                    {
                        Id = item.Value, 
                        // Cells = new NativeList<int3>(Allocator.Persistent){ item.Key },
                        BoundingBox = new IntAABB { Min = item.Key, Max = item.Key }
                    }});
                }
            }
        }
    }
}
