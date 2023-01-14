using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Extensions;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;
using BoxCollider = Unity.Physics.BoxCollider;
using MeshCollider = Unity.Physics.MeshCollider;

public struct GridChangedEvent : IComponentData
{
    public Entity Grid;
    public int3 Position;
}

[ExecuteAlways]
[UpdateInGroup(typeof(TransformSystemGroup), OrderFirst = true)]
public class BeforeReparentingEntitiyCommandBufferSystem : EntityCommandBufferSystem { }

public partial class BuildingSystem : SystemBase
{
    private EntityCommandBufferSystem bufferSystem;
    private EntityQuery query;
    protected override void OnCreate()
    {
        bufferSystem = World.GetExistingSystemManaged<BeforeReparentingEntitiyCommandBufferSystem>();
        RequireForUpdate<IdentifiableMappingSystem.Loaded>();
        query = GetEntityQuery(typeof(PendingPart), ComponentType.Exclude<Ghost>());
        RequireForUpdate(query);
    }

    public partial struct BuildJob : IJobEntity
    {
        public EntityCommandBuffer buffer;
        [ReadOnly] public NativeParallelHashMap<ulong, Entity> prefabMap;
        [ReadOnly] public ComponentLookup<Grid> grids;
        [ReadOnly] public ComponentLookup<GridItem> gridItems;
        [ReadOnly] public ComponentLookup<PhysicsCollider> colliders;
        [ReadOnly] public BufferLookup<OccupationBuffer> occupations;

        public bool IsCreateGhostJob;
        public bool IsFinishConstructionJob;
        public bool RenderBlueprint;

        public void Execute(Entity e, in PendingPart part)
        {
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
            var newGridItem = originalGridItem;
            newGridItem.Grid = part.Grid;
            newGridItem.Area.Origin = part.Data.Area.Origin;
            newGridItem.Area.Extents = originalGridItem.Scalable ? part.Data.Area.Extents : gridItems[prefab].Area.Extents.Rotated(part.Data.Orientation);
            
            // Check if space is already occupied
            if (!IsCreateGhostJob && !IsFinishConstructionJob)
            {
                var canBuild = newGridItem.Area.IsValid;
                foreach (var item in occupations[part.Grid])
                    canBuild &= !newGridItem.Area.Contains(item.Location);
                if (!canBuild)
                {
                    Debug.Log($"Discarded pending part in invalid or occupied area {newGridItem.Area}");
                    return;
                }
            }

            // Create Part
            var entity = buffer.Instantiate(prefab);

            var transform = LocalTransform.Identity
                                          .WithPosition(Grid.PositionFromCell(newGridItem.Area.Origin))
                                          .WithRotation(part.Data.Orientation);
            switch (originalGridItem.Scalable)
            {
                // Scalable Ghosts always have NonUniformScale to make checks easier. Performance doesn't matter since there is only one Ghost at once.
                case true when IsCreateGhostJob:
                    buffer.AddComponent(entity, newGridItem.Area.ToScale(part.Data.Orientation));
                    break;
                case true when math.any(newGridItem.Area.Extents != 0):
                {
                    if (math.any(newGridItem.Area.Size != 1) && colliders.HasComponent(prefab))
                    {
                        var collider = colliders[prefab].Value;
                        var scale3x3 = newGridItem.Area.ToScale(part.Data.Orientation).Value;
                        var scale = new float3(scale3x3.c0.x, scale3x3.c1.y, scale3x3.c2.z);
                        switch (collider.Value.Type)
                        {
                            case ColliderType.Convex:
                                buffer.SetComponent(entity, new PhysicsCollider{ Value = collider.As<ConvexCollider>().Scaled(scale) });
                                break;
                            case ColliderType.Box:
                                buffer.SetComponent(entity, new PhysicsCollider{ Value = collider.As<BoxCollider>().Scaled(scale) });
                                break;
                            default:
                                Debug.LogError($"No collider scaling method provided for type {collider.Value.Type}");
                                break;
                        }
                    }

                    if (part.Data.Area.Size.x == part.Data.Area.Size.y &&
                        part.Data.Area.Size.x == part.Data.Area.Size.z)
                        transform.Scale = part.Data.Area.Size.x;
                    else
                        buffer.AddComponent(entity, newGridItem.Area.ToScale(part.Data.Orientation));
                    break;
                }
            }

            buffer.SetComponent(entity, new Parent {Value = part.Grid});
            buffer.SetComponent(entity, transform);
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
                        TargetGrid = part.Grid
                    });
                }
            }
            else
            {
                for (int x = newGridItem.Area.Min.x; x <= newGridItem.Area.Max.x; x++)
                for (int y = newGridItem.Area.Min.y; y <= newGridItem.Area.Max.y; y++)
                for (int z = newGridItem.Area.Min.z; z <= newGridItem.Area.Max.z; z++)
                {
                    var eChange = buffer.CreateEntity();
                    buffer.AddComponent(eChange, new GridChangedEvent{Grid = part.Grid, Position = new int3(x,y,z)});
                }
            }
        }
    }

    protected override void OnUpdate()
    {
        var buffer = new EntityCommandBuffer(Allocator.TempJob);
        new BuildJob
        {
            buffer = buffer,
            grids = GetComponentLookup<Grid>(true),
            gridItems = GetComponentLookup<GridItem>(true),
            colliders = GetComponentLookup<PhysicsCollider>(true),
            occupations = GetBufferLookup<OccupationBuffer>(true),
            prefabMap = IdentifiableMappingSystem.EntitiesById,
            IsCreateGhostJob = false,
            RenderBlueprint = HasSingleton<BuilderMode_Blueprint>() || HasSingleton<BuilderMode_Build>() || HasSingleton<BuilderMode_Destroy>()
        }.Run(query);

        buffer.DestroyEntity(query);
        buffer.Playback(EntityManager);
        var e = EntityManager.CreateEntity(typeof(GridNeedsUpdate));
    }
}

public partial class GeneratedBufferToPartsSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var ecb = GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
        Entities.ForEach((Entity e, in DynamicBuffer<BuildingPartInfo> parts) =>
        {
            for (int i = 0; i < parts.Length; i++)
            {
                var part = ecb.CreateEntity();
                ecb.AddComponent(part, new PendingPart{Grid = e, Data = parts[i]});
            }
        }).Schedule();

        EntityManager.RemoveComponent<BuildingPartInfo>(GetEntityQuery(typeof(BuildingPartInfo)));
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
                        EntityManager.SetSharedComponentManaged(entity, new PhysicsWorldIndex {Value = isGhost ? 215u : 2u});
                    else
                        EntityManager.SetSharedComponentManaged(entity, new PhysicsWorldIndex {Value = isGhost ? 215u : 2u});
                }

                if (EntityManager.HasComponent<RenderMesh>(entity))
                {
                    var renderMesh = EntityManager.GetSharedComponentManaged<RenderMesh>(entity);
                    if (isGhost)
                    {
                        renderMesh.material = GetComponent<Ghost>(entity).Buildable
                            ? settings.GhostPlacementMaterial
                            : settings.GhostRemovalMaterial;
                    }
                    else
                        renderMesh.material = settings.GhostBlueprintedMaterial;
                    EntityManager.SetSharedComponentManaged(entity, renderMesh);
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
