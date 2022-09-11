using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public struct Rooms : IBufferElementData
{
    public short Id;
    public bool IsExposedToSpace;
    public int3 Cell;
}

public struct Walls : IBufferElementData
{
    public int3 Cell;
}

public partial class RoomSystem : SystemBase
{
    private EntityCommandBufferSystem _bufferSystem;
    protected override void OnCreate()
    {
        _bufferSystem = World.GetExistingSystem<EndInitializationEntityCommandBufferSystem>();
    }

    protected override void OnUpdate() { }

    private static readonly int3[] Neighbours =
    {
        new(1, 0, 0), new(0, 1, 0), new(0, 0, 1),
        new(-1, 0, 0), new(0, -1, 0), new(0, 0, -1)
    };
    
    public static void FindRoom(int3 origin, BoundingBox area, NativeParallelHashMap<int3, short> rooms, NativeQueue<int3> pendingCells)
    {
        if (!rooms.ContainsKey(origin))
            return;
        
        foreach(var offset in Neighbours)
        {
            var neighbour = offset + origin;
            if(math.any(neighbour < area.Min | neighbour > area.Max))
                continue;
            if(!rooms.ContainsKey(neighbour))
                continue;

            var id = rooms[origin];
            var nid = rooms[neighbour];
            
            if(id == nid && id != -1)
                continue;
            
            if (id == -1 && nid == -1)
            {
                id = GetFreeId(rooms);
                rooms[origin] = id;
                rooms[neighbour] = id;
                pendingCells.Enqueue(neighbour);
            }
            else if (id == -1 && nid != -1)
            {
                rooms[origin] = nid;
            }
            else if (id != nid)
            {
                rooms[neighbour] = id;
                pendingCells.Enqueue(neighbour);
            }
        }
    }

    private static short GetFreeId(NativeParallelHashMap<int3, short> rooms)
    {
        var nextId = (short)1;
        foreach(var item in rooms)
            nextId = (short)math.max(nextId, item.Value + 1);
        return nextId;
    }

    public static bool IsWall(int3 cell, DynamicBuffer<Walls> walls)
    {
        var isWall = false;
        for (int i = 0; i < walls.Length; i++)
            isWall |= math.all(walls[i].Cell == cell);
        return isWall;
    }
}

