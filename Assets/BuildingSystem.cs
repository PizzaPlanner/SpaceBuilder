using System.Linq;
using SnapPoints;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Rendering;
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
        EntityManager.AddComponentData(entity, new UpdateRotation {Value = rotation, ReferencePrefab = prefab});
        var newGridItem = RotateItem(originalGridItem, rotation);
        newGridItem.Grid = gridEntity;
        newGridItem.Area.Origin = position;
        EntityManager.SetComponentData(entity, newGridItem);

        EntityManager.AddComponentData(entity, new LocalToParent());
        EntityManager.AddComponentData(entity, new Parent { Value = gridEntity });
        EntityManager.AddComponentData(entity, new Rotation{ Value = rotation });
        EntityManager.AddComponentData(entity, new Translation{ Value = Grid.PositionFromCell(originalGridItem.Type, position) });

#if UNITY_EDITOR
        if(HasComponent<Name>(entity))
            EntityManager.SetName(entity, GetComponent<Name>(entity).Value);
#endif
        if (asGhost)
        {
            EntityManager.AddComponentData(entity, new GhostPlacementBuilder{
                TargetGrid = GetComponent<Grid>(gridEntity),
                Range = 10,
                Original = originalGridItem
            });
            EntityManager.AddComponent<Ghost>(entity);

#if UNITY_EDITOR
            EntityManager.SetName(entity, "[GHOST]");
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

[UpdateAfter(typeof(TransformSystemGroup))]
[UpdateBefore(typeof(LateSimulationSystemGroup))]
public partial class GhostChildrenUpdateSystem : SystemBase
{
    private EntityQuery query;
    protected override void OnCreate()
    {
        query = GetEntityQuery(typeof(Ghost), typeof(GridItem));
        RequireForUpdate(query);
    }

    protected override void OnUpdate()
    {
        var childBuffer = GetBufferFromEntity<Child>(true);
        var entities = query.ToEntityArray(Allocator.Temp);
        foreach (var ghost in entities)
        {
            foreach (var child in childBuffer.GetAllChildren(ghost).ToArray())
            {
                if (!HasComponent<Ghost>(child))
                    EntityManager.AddComponent<Ghost>(child);

                if (EntityManager.HasComponent<PhysicsCollider>(child))
                    EntityManager.RemoveComponent<PhysicsCollider>(child);
            }
        }
    }
}

public partial class GhostVisualsUpdateSystem : SystemBase
{
    private GhostPlacementSettings settings;

    protected override void OnCreate()
    {
        settings = Resources.Load<GhostPlacementSettings>("GhostPlacementSettings");
        RequireSingletonForUpdate<GhostPlacementBuilder>();
    }
    protected override void OnUpdate()
    {
        var buildable = GetSingleton<GhostPlacementBuilder>().Buildable;
        Entities.WithAll<Ghost, RenderMesh>().ForEach((Entity e) =>
        {
            var renderMesh = EntityManager.GetSharedComponentData<RenderMesh>(e);
            renderMesh.material = buildable ? settings.GhostPlacementMaterial : settings.GhostRemovalMaterial;
            EntityManager.SetSharedComponentData(e, renderMesh);
        }).WithoutBurst().WithStructuralChanges().Run();
    }
}
