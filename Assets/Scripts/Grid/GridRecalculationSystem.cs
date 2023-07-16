using System.Dynamic;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
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

public struct GridStructure : IComponentData
{
    public const float CELL_SIZE = 0.25f;
    public int3 BoundsMin, BoundsMax;
    
    // Bounds in World Space
    public AABB AABB;

    public static int3 CellFromPosition(LocalToWorld ltw, float3 worldPosition) => (int3) math.floor(math.mul(math.inverse(ltw.Rotation), worldPosition - ltw.Position) / CELL_SIZE);
    public static int3 CellFromPosition(float3 localPosition) => (int3) math.floor(localPosition / CELL_SIZE);
    public static float3 PositionFromCell(LocalToWorld ltw, int3 cell) =>  math.mul(ltw.Rotation, (float3)cell * CELL_SIZE) + ltw.Position;
    public static float3 GridToWorld(LocalToWorld gridLtw, float3 localPosition) =>  math.mul(gridLtw.Rotation, localPosition * CELL_SIZE) + gridLtw.Position;
    public static float3 WorldToGrid(LocalToWorld gridLtw, float3 worldPosition) => math.mul(math.inverse(gridLtw.Rotation), worldPosition - gridLtw.Position) / CELL_SIZE;
    public static float3 PositionFromCell(int3 cell) =>  (float3)cell * CELL_SIZE;
}

public struct CellMap : ICleanupComponentData
{
    public FixedList4096Bytes<IntAABB> Walls;
    public FixedList4096Bytes<IntAABB> Objects;

    public bool IsWall(int3 position)
    {
        foreach(var wall in Walls)
            if (wall.Contains(position))
                return true;
        return false;
    }

    public bool IsOccupied(int3 position)
    {
        if (IsWall(position))
            return true;
        foreach(var thing in Objects)
            if (thing.Contains(position))
                return true;
        return false;
    }

    public bool IsAnyWall(IntAABB aabb)
    {
        for(int x = aabb.Min.x; x <= aabb.Max.x; x++)
        for(int y = aabb.Min.y; y <= aabb.Max.y; y++)
        for (int z = aabb.Min.z; z <= aabb.Max.z; z++)
        {
            var cell = new int3(x, y, z);
            if (IsWall(cell))
                return true;
        }

        return false;
    }
    
    public bool IsAnyOccupied(IntAABB aabb)
    {
        for(int x = aabb.Min.x; x <= aabb.Max.x; x++)
        for(int y = aabb.Min.y; y <= aabb.Max.y; y++)
        for (int z = aabb.Min.z; z <= aabb.Max.z; z++)
        {
            var cell = new int3(x, y, z);
            if (IsOccupied(cell))
                return true;
        }

        return false;
    }
}

public struct NeedsUpdate : IComponentData { }

public struct GridUpdate : IComponentData
{
    public enum UpdateType
    {
        Add,
        Remove,
        Change
    }

    public UpdateType Type;
    public IntAABB Area;
    public Entity Grid;
    public Entity GridItem;
}

[UpdateInGroup(typeof(GridRecalculationSystemGroup), OrderFirst = true)]
public partial class GridRecalculationSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate(GetEntityQuery(typeof(GridUpdate)));
    }

    protected override unsafe void OnUpdate()
    {
        Debug.Log("Recalculating Grids!");
        var cells = GetComponentLookup<CellMap>();
        Entities.ForEach((in GridUpdate update) =>
        {
            // Update bounds
            var grid = SystemAPI.GetComponent<GridStructure>(update.Grid);
            grid.BoundsMin = math.min(update.Area.Min, grid.BoundsMin);
            grid.BoundsMin = math.max(update.Area.Max, grid.BoundsMax);
            SystemAPI.SetComponent(update.Grid, grid);
            
            // Update walls
            var gridWalls = cells[update.Grid];
            var gridItem = SystemAPI.GetComponent<GridItem>(update.GridItem);
            if (gridItem.IsWall)
            {
                gridWalls.Walls.Add(update.Area);
            }

            // Update Rooms
            var rooms = SystemAPI.GetBuffer<Rooms>(update.Grid);
            var bbox = new IntAABB
            {
                Min = math.max(grid.BoundsMin, update.Area.Min),
                Max = math.min(grid.BoundsMax, update.Area.Max),
            };
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
                    r.Cells.Add(item.Key);
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
                        Cells = new NativeList<int3>(Allocator.Persistent){ item.Key },
                        BoundingBox = new IntAABB { Min = item.Key, Max = item.Key }
                    }});
                }
            }
        }).Run();

        var buffer = GetSingleton<EndInitializationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
        buffer.DestroyEntity(GetEntityQuery(typeof(GridUpdate)));
    }
}
