using System;
using Unity.Collections;
using Unity.Entities;

public struct GridItem : IComponentData
{
    [Flags]
    public enum Side : byte
    {
        Up = 1<<0,
        Down = 1<<1,
        Right = 1<<2,
        Left = 1<<3,
        Front = 1<<4,
        Back = 1<<5,
        Center = 1<<6
    }
    
    public BoundingBox Area;
    public bool Scalable;
    public bool IsWall;
    public float Mass;
}

[UpdateInGroup(typeof(InitializationSystemGroup))]
[UpdateAfter(typeof(IdentifiableMappingSystem))]
public partial class GridItemMappingSystem : SystemBase
{
    public struct Loaded : IComponentData { }
    static NativeParallelHashMap<ulong, GridItem> GridItemsById;

    public static GridItem GetPrefabFromId(ulong id) => GridItemsById.TryGetValue(id, out var gridItem) ? gridItem : new GridItem
    {
        Area = new BoundingBox{Origin = int.MaxValue}
    };
    
    protected override void OnCreate()
    {
        GridItemsById = new NativeParallelHashMap<ulong, GridItem>(0, Allocator.Persistent);
        RequireForUpdate<IdentifiableMappingSystem.Loaded>();
    }

    protected override void OnDestroy()
    {
        GridItemsById.Dispose();
    }

    protected override void OnStopRunning()
    {
        EntityManager.CreateEntity(typeof(Loaded));
    }

    protected override void OnUpdate()
    {
        foreach(var entry in IdentifiableMappingSystem.EntitiesById)
            if(SystemAPI.HasComponent<GridItem>(entry.Value))
                GridItemsById.Add(entry.Key, SystemAPI.GetComponent<GridItem>(entry.Value));
        Enabled = false;
    }
}