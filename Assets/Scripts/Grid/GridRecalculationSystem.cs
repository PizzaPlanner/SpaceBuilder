using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public struct Grid : IComponentData
{
    public const float CELL_SIZE = 0.25f;
    public int3 BoundsMin;
    public int3 BoundsMax;

    public AABB AABB;

    public static int3 CellFromPosition(LocalToWorld ltw, float3 worldPosition) => (int3) math.floor(math.mul(math.inverse(ltw.Rotation), worldPosition - ltw.Position) / CELL_SIZE);
    public static int3 CellFromPosition(float3 localPosition) => (int3) math.floor(localPosition / CELL_SIZE);
    public static float3 PositionFromCell(LocalToWorld ltw, int3 cell) =>  math.mul(ltw.Rotation, (float3)cell * CELL_SIZE) + ltw.Position;
    public static float3 GridToWorld(LocalToWorld gridLtw, float3 localPosition) =>  math.mul(gridLtw.Rotation, localPosition * CELL_SIZE) + gridLtw.Position;
    public static float3 WorldToGrid(LocalToWorld gridLtw, float3 worldPosition) => math.mul(math.inverse(gridLtw.Rotation), worldPosition - gridLtw.Position) / CELL_SIZE;
    public static float3 PositionFromCell(int3 cell) =>  (float3)cell * CELL_SIZE;
}

public struct OccupationBuffer : IBufferElementData
{
    public GridItem.Side Value;
    public int3 Location;
}

public struct NeedsUpdate : IComponentData { }
public struct GridNeedsUpdate : IComponentData { }

[UpdateInGroup(typeof(GridRecalculationSystemGroup), OrderFirst = true)]
public partial class GridRecalculationSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate(GetEntityQuery(typeof(GridNeedsUpdate)));
    }

    protected override unsafe void OnUpdate()
    {
        Debug.Log("Recalculating Grids!");
        Entities.WithAll<Grid>().ForEach((DynamicBuffer<OccupationBuffer> obuff, DynamicBuffer<Walls> walls, DynamicBuffer<Rooms> rooms) =>
        {
            obuff.Clear();
            walls.Clear();
            rooms.Clear();
        }).Run();

        Entities.ForEach((ref Grid grid) =>
        {
            grid.BoundsMax = int.MinValue;
            grid.BoundsMin = int.MaxValue;
        }).Run();

        var occupationBuffers = GetBufferLookup<OccupationBuffer>();
        var wallBuffers = GetBufferLookup<Walls>();
        var grids = GetComponentLookup<Grid>();
        Entities
            .WithName("CalculateOccupation")
            .ForEach((Entity e, in GridItem item) =>
        {
            if (occupationBuffers.HasComponent(item.Grid))
            {
                var min = item.Area.Min;
                var max = item.Area.Max;
                var occupations = occupationBuffers[item.Grid];
                var walls = wallBuffers[item.Grid];
                for (int x = min.x; x <= max.x; x++)
                for (int y = min.y; y <= max.y; y++)
                for (int z = min.z; z <= max.z; z++)
                {
                    var pos = new int3(x, y, z);
                    bool found = false;
                    for (int i = 0; i < occupations.Length; i++)
                        found |= math.all(occupations[i].Location == pos);

                    if (!found)
                    {
                        occupations.Add(new OccupationBuffer { Location = pos });
                        if(item.IsWall && !HasComponent<Blueprinted>(e))
                            walls.Add(new Walls { Cell = pos });
                    }
                }
            }

            if (grids.HasComponent(item.Grid))
            {
                var gTemp = grids[item.Grid];
                gTemp.BoundsMin = math.min(gTemp.BoundsMin, item.Area.Min);
                gTemp.BoundsMax = math.max(gTemp.BoundsMax, item.Area.Max);
                grids[item.Grid] = gTemp;
            }
        }).Run();

        var cellToRoomMap = new NativeParallelHashMap<int3, short>(0, Allocator.TempJob);
        var exposedRooms = new NativeList<short>(Allocator.TempJob);
        var cellsNeedUpdate = new NativeQueue<int3>(Allocator.TempJob);
        Entities
            .WithDisposeOnCompletion(cellToRoomMap)
            .WithDisposeOnCompletion(exposedRooms)
            .WithDisposeOnCompletion(cellsNeedUpdate)
            .ForEach((DynamicBuffer<Walls> walls, DynamicBuffer<Rooms> rooms, in Grid grid) =>
        {
            cellToRoomMap.Clear();
            cellsNeedUpdate.Clear();
            exposedRooms.Clear();
            var bbox = new BoundingBox { Origin = grid.BoundsMin, Extents = grid.BoundsMax - grid.BoundsMin }; 
            cellToRoomMap.Capacity = bbox.Size.x * bbox.Size.y * bbox.Size.z;
            for (int x = bbox.Min.x; x <= bbox.Max.x; x++)
            for (int y = bbox.Min.y; y <= bbox.Max.y; y++)
            for (int z = bbox.Min.z; z <= bbox.Max.z; z++)
                if (!RoomSystem.IsWall(new int3(x, y, z), walls))
                {
                    cellToRoomMap[new int3(x, y, z)] = -1;
                    cellsNeedUpdate.Enqueue(new int3(x, y, z));
                }
            
            while(cellsNeedUpdate.TryDequeue(out var cell))
                RoomSystem.FindRoom(cell, bbox, cellToRoomMap, cellsNeedUpdate);

            foreach (var item in cellToRoomMap)
                if (math.any(item.Key == bbox.Min | item.Key == bbox.Max) && !exposedRooms.Contains(item.Value))
                    exposedRooms.Add(item.Value);
            
            foreach (var item in cellToRoomMap)
                rooms.Add(new Rooms{Cell = item.Key, Id = item.Value, IsExposedToSpace = exposedRooms.Contains(item.Value)});
        }).Run();

        var buffer = GetSingleton<EndInitializationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
        buffer.DestroyEntity(GetEntityQuery(typeof(GridNeedsUpdate)));
    }
}
