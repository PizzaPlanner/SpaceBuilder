using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public partial class BuildingSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate(GetEntityQuery(typeof(PendingPart)));
    }

    protected override void OnUpdate()
    {
        Entities.ForEach((in PendingPart part) =>
        {
            Build(part.Data.Id, part.Grid, part.Data.Position, part.Data.Orientation, false);
        }).WithoutBurst().WithStructuralChanges().Run();

        EntityManager.DestroyEntity(GetEntityQuery(typeof(PendingPart)));
        EntityManager.CreateEntity(typeof(GridsNeedUpdate));
    }

    public Entity Build(long id, Entity gridEntity, int3 position, quaternion rotation, bool asGhost)
    {
        Debug.Log($"Building {id}@{position} on {gridEntity}");
        if (!HasComponent<Grid>(gridEntity))
            return Entity.Null;

        var prefabQuery = GetEntityQuery(typeof(Prefab), typeof(Identifieable));
        var identifiables = prefabQuery.ToComponentDataArray<Identifieable>(Allocator.Temp);
        var prefabs = prefabQuery.ToEntityArray(Allocator.Temp);
        var prefab = Entity.Null;
        for(int i = 0; i < identifiables.Length; i++)
            if (identifiables[i].ItemId == id)
                prefab = prefabs[i];
        if (prefab == Entity.Null)
            return Entity.Null;

        var entity = EntityManager.Instantiate(prefab);
        var originalGridItem = GetComponent<GridItem>(prefab);
        EntityManager.AddComponentData(entity, new Parent { Value = gridEntity});
        EntityManager.AddComponentData(entity, new Rotation{ Value = rotation});
        EntityManager.AddComponentData(entity, new Translation{ Value = Grid.PositionFromCell(originalGridItem.Type, position) });
        EntityManager.AddComponentData(entity, new UpdateRotation {Value = rotation});
        EntityManager.AddComponent<LocalToParent>(entity);

        var newGridItem = RotateItem(originalGridItem, rotation);
        newGridItem.Area.Origin = position;
        EntityManager.SetComponentData(entity, newGridItem);

        if (asGhost)
        {
            EntityManager.AddComponentData(entity, new GhostPlacementBuilder{
                TargetGrid = GetComponent<Grid>(gridEntity),
                Range = 3,
                Original = originalGridItem
            });
            EntityManager.AddComponent<Ghost>(entity);

#if UNITY_EDITOR
            EntityManager.SetName(entity, "[GHOST]Placement");
#endif
        }

        return entity;
    }

    public static GridItem RotateItem(GridItem original, quaternion rotation)
    {
        original.Airtight = original.Airtight.Rotate(rotation);
        original.Occupied = original.Occupied.Rotate(rotation);
        original.Area.RotatedExtents = (int3)math.round(math.mul(rotation, original.Area.Extents));
        return original;
    }
}
