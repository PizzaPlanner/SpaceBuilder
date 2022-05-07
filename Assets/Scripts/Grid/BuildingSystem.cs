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

        // Get prefab from id
        var prefabQuery = GetEntityQuery(typeof(Prefab), typeof(Identifieable));
        var identifiables = prefabQuery.ToComponentDataArray<Identifieable>(Allocator.Temp);
        var prefabs = prefabQuery.ToEntityArray(Allocator.Temp);
        var prefab = Entity.Null;
        for(int i = 0; i < identifiables.Length; i++)
            if (identifiables[i].ItemId == id)
                prefab = prefabs[i];
        if (prefab == Entity.Null)
            return Entity.Null;

        var originalGridItem = GetComponent<GridItem>(prefab);
        var newGridItem = RotateItem(originalGridItem, rotation);
        newGridItem.Grid = gridEntity;
        newGridItem.Area.Origin = position;

        // Check if space is already occupied
        if (!asGhost)
        {
            var canBuild = true;
            foreach (var item in GetBuffer<OccupationBuffer>(gridEntity))
                canBuild &= !(item.Size == newGridItem.Type && newGridItem.Area.Contains(item.Location) && (item.Value & newGridItem.Occupied) > 0);
            if (!canBuild)
                return Entity.Null;
        }

        // Create Part
        var entity = EntityManager.Instantiate(prefab);
        EntityManager.AddComponentData(entity, new UpdateRotation {Value = rotation, ReferencePrefab = prefab});
        SetComponent(entity, newGridItem);
        SetComponent(entity, new Parent { Value = gridEntity });
        SetComponent(entity, new Rotation{ Value = rotation });
        SetComponent(entity, new Translation{ Value = Grid.PositionFromCell(originalGridItem.Type, position) });

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
            var gParent = GetComponent<Ghost>(ghost);
            foreach (var child in childBuffer.GetAllChildren(ghost).ToArray())
            {
                if (!HasComponent<Ghost>(child))
                    EntityManager.AddComponentData(child, gParent);
                else
                    EntityManager.SetComponentData(child, gParent);

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
        Entities.WithAll<RenderMesh>().ForEach((Entity e, in Ghost ghost) =>
        {
            var renderMesh = EntityManager.GetSharedComponentData<RenderMesh>(e);
            renderMesh.material = ghost.Buildable ? settings.GhostPlacementMaterial : settings.GhostRemovalMaterial;
            EntityManager.SetSharedComponentData(e, renderMesh);
        }).WithoutBurst().WithStructuralChanges().Run();
    }
}
