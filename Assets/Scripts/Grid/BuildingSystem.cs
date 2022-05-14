using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

public partial class BuildingSystem : SystemBase
{
    private EntityCommandBufferSystem bufferSystem;
    private EntityQuery query;
    protected override void OnCreate()
    {
        bufferSystem = World.GetExistingSystem<EndSimulationEntityCommandBufferSystem>();
        RequireSingletonForUpdate<IdentifiableMappingSystem.Loaded>();
        query = GetEntityQuery(typeof(PendingPart), ComponentType.Exclude<Ghost>());
        RequireForUpdate(query);
    }

    public partial struct BuildJob : IJobEntity
    {
        public EntityCommandBuffer buffer;
        [ReadOnly] public NativeHashMap<long, Entity> prefabMap;
        [ReadOnly] public ComponentDataFromEntity<Grid> grids;
        [ReadOnly] public ComponentDataFromEntity<GridItem> gridItems;
        [ReadOnly] public BufferFromEntity<OccupationBuffer> occupations;

        public bool IsCreateGhostJob;
        public bool IsFinishConstructionJob;

        public void Execute(Entity e, in PendingPart part)
        {
            if (!grids.HasComponent(part.Grid))
                return;

            // Get prefab from id
            if (!prefabMap.TryGetValue(part.Data.Id, out var prefab))
                return;

            var originalGridItem = gridItems[prefab];
            var newGridItem = RotateItem(originalGridItem, part.Data.Orientation);
            newGridItem.Grid = part.Grid;
            newGridItem.Area.Origin = part.Data.Position;

            // Check if space is already occupied
            if (!IsCreateGhostJob && !IsFinishConstructionJob)
            {
                var canBuild = true;
                foreach (var item in occupations[part.Grid])
                    canBuild &= !(item.Size == newGridItem.Type && newGridItem.Area.Contains(item.Location) &&
                                  (item.Value & newGridItem.Occupied) > 0);
                if (!canBuild)
                    return;
            }

            // Create Part
            var entity = buffer.Instantiate(prefab);
            buffer.AddComponent(entity, new UpdateRotation {Value = part.Data.Orientation, ReferencePrefab = prefab});
            buffer.SetComponent(entity, newGridItem);
            buffer.SetComponent(entity, new Parent {Value = part.Grid});
            buffer.SetComponent(entity, new Rotation {Value = part.Data.Orientation});
            buffer.SetComponent(entity, new Translation {Value = Grid.PositionFromCell(originalGridItem.Type, part.Data.Position)});

            if (part.Data.BuildStatus == 0)
            {
                if(!IsCreateGhostJob)
                {
                    buffer.AddComponent<Blueprinted>(entity);
                    buffer.AddComponent<UnderConstruction>(entity);
                }
                else
                {
                    buffer.AddComponent<Ghost>(entity);
                    buffer.AddComponent(entity, new GhostPlacementBuilder
                    {
                        Original = originalGridItem,
                        Range = 10,
                        TargetGrid = part.Grid
                    });
                }
            } else if(!IsCreateGhostJob && part.Data.BuildStatus < ushort.MaxValue)
                buffer.AddComponent(entity, new UnderConstruction{Progress = part.Data.BuildStatus});

            buffer.DestroyEntity(e);
        }
    }

    protected override void OnUpdate()
    {
        var buffer = bufferSystem.CreateCommandBuffer();
        Dependency = new BuildJob
        {
            buffer = buffer,
            grids = GetComponentDataFromEntity<Grid>(true),
            gridItems = GetComponentDataFromEntity<GridItem>(true),
            occupations = GetBufferFromEntity<OccupationBuffer>(true),
            prefabMap = IdentifiableMappingSystem.EntitiesById,
            IsCreateGhostJob = false
        }.Schedule(query);

        bufferSystem.AddJobHandleForProducer(Dependency);
        EntityManager.CreateEntity(typeof(GridsNeedUpdate));
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
    private EntityCommandBufferSystem bufferSystem;
    protected override void OnCreate()
    {
        bufferSystem = World.GetExistingSystem<EndSimulationEntityCommandBufferSystem>();
        query = GetEntityQuery(typeof(Ghost), typeof(GridItem));
        RequireForUpdate(query);
    }

    protected override void OnUpdate()
    {
        var buffer = bufferSystem.CreateCommandBuffer();
        Entities
            .ForEach((Entity e, in DynamicBuffer<Child> childBuffer, in Ghost ghost) =>
            {
                for (int i = 0; i < childBuffer.Length; i++)
                {
                    var e2 = childBuffer[i].Value;
                    if (HasComponent<Ghost>(e2))
                        buffer.SetComponent(e2, ghost);
                    else
                        buffer.AddComponent(e2, ghost);
                }
            }).Schedule();

        Entities
            .ForEach((Entity e, in DynamicBuffer<Child> childBuffer, in Blueprinted bp) =>
            {
                for (int i = 0; i < childBuffer.Length; i++)
                {
                    var e2 = childBuffer[i].Value;
                    if (HasComponent<Blueprinted>(e2))
                        continue;

                    buffer.AddComponent(e2, bp);
                }
            }).Schedule();

        Entities
            .WithAny<Ghost, Blueprinted>()
            .ForEach((ref PhysicsCollider collider) =>
            {
                collider.Value.Value.Filter = new CollisionFilter{BelongsTo = 1 << 12, CollidesWith = 1<< 12};
            }).Schedule();

        bufferSystem.AddJobHandleForProducer(Dependency);
    }
}

public partial class GhostVisualsUpdateSystem : SystemBase
{
    private GhostPlacementSettings settings;

    protected override void OnCreate()
    {
        settings = Resources.Load<GhostPlacementSettings>("GhostPlacementSettings");
        RequireForUpdate(GetEntityQuery(new EntityQueryDesc{Any = new[]
        {
            ComponentType.ReadOnly<BuilderMode_Blueprint>(),
            ComponentType.ReadOnly<BuilderMode_Build>()
        }}));
    }

    protected override void OnStartRunning()
    {
        EntityManager.RemoveComponent<DisableRendering>(GetEntityQuery(typeof(DisableRendering), typeof(Blueprinted)));
    }

    protected override void OnStopRunning()
    {
        EntityManager.AddComponent<DisableRendering>(GetEntityQuery(ComponentType.Exclude<DisableRendering>(), typeof(Blueprinted)));
    }

    protected override void OnUpdate()
    {
        Entities.WithAll<RenderMesh>().ForEach((Entity e, in Ghost ghost) =>
        {
            var renderMesh = EntityManager.GetSharedComponentData<RenderMesh>(e);
            renderMesh.material = ghost.Buildable ? settings.GhostPlacementMaterial : settings.GhostRemovalMaterial;
            EntityManager.SetSharedComponentData(e, renderMesh);
        }).WithoutBurst().WithStructuralChanges().Run();

        Entities.WithAll<RenderMesh, Blueprinted>().ForEach((Entity e) =>
        {
            var renderMesh = EntityManager.GetSharedComponentData<RenderMesh>(e);
            renderMesh.material = settings.GhostBlueprintedMaterial;
            EntityManager.SetSharedComponentData(e, renderMesh);
        }).WithoutBurst().WithStructuralChanges().Run();
    }
}
