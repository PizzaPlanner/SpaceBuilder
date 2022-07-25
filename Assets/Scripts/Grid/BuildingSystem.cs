using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

[ExecuteAlways]
[UpdateInGroup(typeof(TransformSystemGroup), OrderFirst = true)]
public class BeforeReparentingEntitiyCommandBufferSystem : EntityCommandBufferSystem { }

public partial class BuildingSystem : SystemBase
{
    private EntityCommandBufferSystem bufferSystem;
    private EntityQuery query;
    protected override void OnCreate()
    {
        bufferSystem = World.GetExistingSystem<BeforeReparentingEntitiyCommandBufferSystem>();
        RequireSingletonForUpdate<IdentifiableMappingSystem.Loaded>();
        query = GetEntityQuery(typeof(PendingPart), ComponentType.Exclude<Ghost>());
        RequireForUpdate(query);
    }

    public partial struct BuildJob : IJobEntity
    {
        public EntityCommandBuffer buffer;
        [ReadOnly] public NativeParallelHashMap<ulong, Entity> prefabMap;
        [ReadOnly] public ComponentDataFromEntity<Grid> grids;
        [ReadOnly] public ComponentDataFromEntity<GridItem> gridItems;
        [ReadOnly] public BufferFromEntity<OccupationBuffer> occupations;

        public bool IsCreateGhostJob;
        public bool IsFinishConstructionJob;
        public bool RenderBlueprint;

        public void Execute(Entity e, in PendingPart part)
        {
            buffer.DestroyEntity(e);

            if (!grids.HasComponent(part.Grid))
            {
                Debug.Log($"Discarded pending part for invalid grid {part.Grid}");
                return;
            }

            // Get prefab from id
            if (!prefabMap.TryGetValue(part.Data.Id, out var prefab))
            {
                Debug.Log($"Discarded pending part with invalid prefab ID {part.Data.Id}");
                return;
            }

            var originalGridItem = gridItems[prefab];
            var newGridItem = RotateItem(originalGridItem, part.Data.Orientation);
            newGridItem.Grid = part.Grid;
            newGridItem.Area.Origin = part.Data.Position;

            // Check if space is already occupied
            if (!IsCreateGhostJob && !IsFinishConstructionJob)
            {
                var canBuild = true;
                foreach (var item in occupations[part.Grid])
                    canBuild &= !(newGridItem.Area.Contains(item.Location) &&
                                  (item.Value & newGridItem.Occupied) > 0);
                if (!canBuild)
                {
                    Debug.Log($"Discarded pending part on occupied area {newGridItem.Area}");
                    return;
                }
            }


            // Create Part
            var entity = buffer.Instantiate(prefab);

            switch (originalGridItem.Scalable)
            {
                // Scalable Ghosts always have NonUniformScale to make checks easier. Performance doesn't matter since there is only one Ghost at once.
                case true when IsCreateGhostJob:
                    buffer.AddComponent(entity, new NonUniformScale{Value = part.Data.Scale});
                    break;
                case true when math.any(part.Data.Scale != 1):
                {
                    if(part.Data.Scale.x == part.Data.Scale.y && part.Data.Scale.x == part.Data.Scale.z)
                        buffer.AddComponent(entity, new Scale{Value = part.Data.Scale.x});
                    else
                        buffer.AddComponent(entity, new NonUniformScale{Value = part.Data.Scale});
                    newGridItem.Area.Extents = part.Data.Scale - 1;
                    break;
                }
            }

            // buffer.AddComponent(entity, new UpdateRotation {Value = part.Data.Orientation, ReferencePrefab = prefab});
            newGridItem = RotateItem(newGridItem, part.Data.Orientation);

            buffer.SetComponent(entity, new Parent {Value = part.Grid});
            buffer.SetComponent(entity, new Rotation {Value = part.Data.Orientation});
            buffer.SetComponent(entity, new Translation {Value = Grid.PositionFromCell(part.Data.Position)});
            buffer.SetComponent(entity, newGridItem);

            if (part.Data.Color != default)
            {
                buffer.AddComponent(entity, new URPMaterialPropertyBaseColor{Value = new float4(part.Data.Color.r, part.Data.Color.g, part.Data.Color.b, 1) });
            }

            if (part.Data.BuildStatus < ushort.MaxValue)
            {
                buffer.AddComponent<Blueprinted>(entity);
                buffer.AddComponent<NeedsUpdate>(entity);
                if(!IsCreateGhostJob)
                {
                    buffer.AddComponent(entity, new UnderConstruction{Progress = part.Data.BuildStatus});
                    if(!RenderBlueprint)
                        buffer.AddComponent<DisableRendering>(entity);
                }
                else
                {
                    buffer.RemoveComponent<Targetable>(entity);
                    buffer.AddComponent<Ghost>(entity);
                    buffer.AddComponent(entity, new GhostPlacementBuilder
                    {
                        Original = originalGridItem,
                        Range = 10,
                        TargetGrid = part.Grid
                    });
                }
            }
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
            IsCreateGhostJob = false,
            RenderBlueprint = HasSingleton<BuilderMode_Blueprint>() || HasSingleton<BuilderMode_Build>() || HasSingleton<BuilderMode_Destroy>()
        }.Schedule(query);

        bufferSystem.AddJobHandleForProducer(Dependency);
        var e = EntityManager.CreateEntity(typeof(GridNeedsUpdate));
    }

    public static GridItem RotateItem(GridItem original, quaternion rotation)
    {
        original.Airtight = original.Airtight.Rotate(rotation);
        original.Occupied = original.Occupied.Rotate(rotation);
        original.Area.RotatedExtents = (int3)math.round(math.mul(rotation, original.Area.Extents));
        return original;
    }
}

[UpdateInGroup(typeof(LateSimulationSystemGroup))]
public partial class BlueprintedChildrenUpdateSystem : SystemBase
{
    private GhostPlacementSettings settings;
    private EntityQuery ghostQuery;

    protected override void OnCreate()
    {
        base.OnCreate();
        settings = Resources.Load<GhostPlacementSettings>("GhostPlacementSettings");
        ghostQuery = GetEntityQuery(typeof(Blueprinted), typeof(NeedsUpdate));
        RequireForUpdate(ghostQuery);
    }

    protected override void OnUpdate()
    {
        while (ghostQuery.CalculateChunkCount() > 0)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var entities = ghostQuery.ToEntityArray(Allocator.Temp);
            foreach (var entity in entities)
            {
                var isGhost = HasComponent<Ghost>(entity);
                if (EntityManager.HasComponent<Child>(entity))
                {
                    var childBuffer = GetBuffer<Child>(entity);
                    for (int i = 0; i < childBuffer.Length; i++)
                    {
                        var e2 = childBuffer[i].Value;
                        if (!HasComponent<Blueprinted>(e2))
                            ecb.AddComponent<Blueprinted>(e2);
                        else if(!isGhost)
                            continue;

                        if (isGhost)
                        {
                            var ghost = GetComponent<Ghost>(entity);
                            if (HasComponent<Ghost>(e2))
                                ecb.SetComponent(e2, ghost);
                            else
                                ecb.AddComponent(e2, ghost);
                        }

                        ecb.AddComponent<NeedsUpdate>(e2);
                    }
                }

                if (HasComponent<PhysicsCollider>(entity))
                {
                    if(EntityManager.HasComponent<PhysicsWorldIndex>(entity))
                        EntityManager.SetSharedComponentData(entity, new PhysicsWorldIndex {Value = isGhost ? 215u : 2u});
                    else
                        EntityManager.AddSharedComponentData(entity, new PhysicsWorldIndex {Value = isGhost ? 215u : 2u});
                }

                if (EntityManager.HasComponent<RenderMesh>(entity))
                {
                    var renderMesh = EntityManager.GetSharedComponentData<RenderMesh>(entity);
                    if (isGhost)
                    {
                        renderMesh.material = GetComponent<Ghost>(entity).Buildable
                            ? settings.GhostPlacementMaterial
                            : settings.GhostRemovalMaterial;
                    }
                    else
                        renderMesh.material = settings.GhostBlueprintedMaterial;
                    EntityManager.SetSharedComponentData(entity, renderMesh);
                }
                ecb.RemoveComponent<NeedsUpdate>(entity);
            }
            ecb.Playback(EntityManager);
            ecb.Dispose();
            entities.Dispose();
        }
    }
}

[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class BlueprintedVisualsUpdateSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate(GetEntityQuery(new EntityQueryDesc{Any = new[]
        {
            ComponentType.ReadOnly<BuilderMode_Blueprint>(),
            ComponentType.ReadOnly<BuilderMode_Build>(),
            ComponentType.ReadOnly<BuilderMode_Destroy>()
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

    protected override void OnUpdate() { }
}
