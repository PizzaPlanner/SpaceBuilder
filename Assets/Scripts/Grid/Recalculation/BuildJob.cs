using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Extensions;
using Unity.Rendering;
using Unity.Transforms;
using Debug = UnityEngine.Debug;


[WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)]
public partial struct BuildJob : IJobEntity
{
    public EntityCommandBuffer ecb;
    public RoomsLookup Rooms; 
    [ReadOnly] public NativeParallelHashMap<ulong, Entity> prefabMap;
    [ReadOnly] public ComponentLookup<PhysicsCollider> colliders;

    public void Execute(Entity grid, DynamicBuffer<BuildingPartInfo> parts, GridUpdateAspect update)
    {
        if (parts.Length == 0)
            return;
        
        var updates = 0;
        var affectedArea = new IntAABB
        {
            Min = int.MaxValue,
            Max = int.MinValue
        };
        foreach (var part in parts)
        {
            // Get prefab from id
            if (!prefabMap.TryGetValue(part.Id, out var prefab))
            {
                Debug.LogWarning($"Discarded pending part with invalid prefab ID {part.Id}");
                continue;
            }
            
            var newGridItem = part.ToGridItem;
            var area = newGridItem.Area;
                
            // Check if space is already occupied
            var canBuild = area.IsValid;
            for (int x = area.Min.x; x <= area.Max.x && canBuild; x++)
            for (int y = area.Min.y; y <= area.Max.y && canBuild; y++)
            for (int z = area.Min.z; z <= area.Max.z && canBuild; z++)
                canBuild &= !Rooms.OccupiedCells.Contains((grid, new int3(x, y, z)));
            
            if (!canBuild && !part.IsGhost)
            {
                Debug.LogWarning($"Discarded pending part in invalid or occupied area {area.Min}/{area.Max}");
                continue;
            }

            // Create Part
            var entity = ecb.Instantiate(prefab);
            
            if (newGridItem.Scalable)
            {
                ecb.SetComponent(entity, part.ToScale);
                if (math.any(area.Size != 1) && colliders.HasComponent(prefab))
                {
                    var collider = colliders[prefab].Value;
                    // area is in grid-space, but the collider operates in local-space
                    var localScale = math.abs(((int3)area.Size).Rotated(math.inverse(part.Orientation)));
                    switch (collider.Value.Type)
                    {
                        case ColliderType.Convex:
                            ecb.SetComponent(entity,
                                new PhysicsCollider { Value = collider.As<ConvexCollider>().Scaled(localScale) });
                            break;
                        case ColliderType.Box:
                            ecb.SetComponent(entity,
                                new PhysicsCollider { Value = collider.As<BoxCollider>().Scaled(localScale) });
                            break;
                        default:
                            Debug.LogError($"No collider scaling method provided for type {collider.Value.Type}");
                            break;
                    }
                }
            }

            ecb.AddComponent(entity, new Parent { Value = grid });
            ecb.SetComponent(entity, part.ToTransform);
            ecb.SetComponent(entity, newGridItem);
            
            if (part.Color != default)
            {
                ecb.AddComponent(entity, new URPMaterialPropertyBaseColor{Value = new float4(part.Color.r, part.Color.g, part.Color.b, 1) });
            }

            if (part.IsGhost)
            {
                ecb.AddComponent(entity, new Ghost { Buildable = canBuild });
                ecb.AddComponent(entity,
                    new FixGhostVisualsAndPhysicsSystem.FixGhostandChildren { GhostBuildable = canBuild });
            }
            else
            {
                updates++;
                affectedArea.Min = math.min(area.Min, affectedArea.Min);
                affectedArea.Max = math.max(area.Max, affectedArea.Max);
                
                for (int x = area.Min.x; x <= area.Max.x; x++)
                for (int y = area.Min.y; y <= area.Max.y; y++)
                for (int z = area.Min.z; z <= area.Max.z; z++)
                {
                    Rooms.OccupiedCells.Add((grid, new int3(x, y, z)));
                    if(newGridItem.IsWall)
                        Rooms.Walls.Add(grid, new int3(x,y,z));
                }
            }
        }
        parts.Clear();

        if (updates > 0)
        {
            update.Update = new GridUpdate
            {
                Area = affectedArea,
                Type = GridUpdate.UpdateType.Add
            };
        }
    }
}