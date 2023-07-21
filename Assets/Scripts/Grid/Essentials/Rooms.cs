using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

public struct Rooms : IBufferElementData, IDisposable
{
    public Room Room;
    
    public void Dispose()
    {
        Room.Dispose();
    }
}

public struct Room : IDisposable
{
    public short Id;
    // public NativeList<int3> Cells;
    public IntAABB BoundingBox;

    public bool Contains(int3 cell)
    {
        if (!BoundingBox.Contains(cell))
            return false;
        // foreach(var inside in Cells)
        //     if (math.all(inside == cell))
        //         return true;
        return false;
    }

    public void Dispose()
    {
        // Cells.Dispose();
    }
}

public static class RoomSystem
{
    private static readonly int3[] Neighbours =
    {
        new(1, 0, 0), new(0, 1, 0), new(0, 0, 1),
        new(-1, 0, 0), new(0, -1, 0), new(0, 0, -1)
    };
    
    public static void FindRoom(ref short currentRoomId, int3 origin, IntAABB area, NativeParallelHashMap<int3, short> rooms, NativeQueue<int3> pendingCells)
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
            
            if(id == nid && id != short.MaxValue)
                continue;
            
            if (id == short.MaxValue && nid == short.MaxValue)
            {
                id = ++currentRoomId;
                rooms[origin] = id;
                rooms[neighbour] = id;
                pendingCells.Enqueue(neighbour);
            }
            else if (id == short.MaxValue && nid != short.MaxValue)
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
}

