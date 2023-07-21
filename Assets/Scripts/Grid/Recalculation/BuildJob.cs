using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Extensions;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using BoxCollider = Unity.Physics.BoxCollider;

[WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)]
public partial struct BuildJob : IJobEntity
{
    public EntityCommandBuffer ecb;
    [ReadOnly] public NativeParallelHashMap<ulong, Entity> prefabMap;
    [ReadOnly] public ComponentLookup<GridItem> gridItems;
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

            var originalGridItem = gridItems[prefab];
            var newGridItem = originalGridItem;
            newGridItem.Area.Origin = part.Area.Origin;
            newGridItem.Area.Extents = originalGridItem.Scalable ? 
                part.Area.Extents : 
                gridItems[prefab].Area.Extents.Rotated(part.Orientation);
                
            // Check if space is already occupied
            var canBuild = newGridItem.Area.IsValid; // && !occupations[part.Grid].IsAnyOccupied(newGridItem.Area);
            if (!canBuild)
            {
                Debug.LogWarning($"Discarded pending part in invalid or occupied area {newGridItem.Area}");
                continue;
            }

            // Create Part
            var entity = ecb.Instantiate(prefab);

            var transform = LocalTransform.FromPositionRotation(
                GridStructure.PositionFromCell(newGridItem.Area.Origin),
                part.Orientation);
            if (originalGridItem.Scalable && math.any(newGridItem.Area.Extents != 0))
            {
                if (math.any(newGridItem.Area.Size != 1) && colliders.HasComponent(prefab))
                {
                    var collider = colliders[prefab].Value;
                    var scale3X3 = newGridItem.Area.ToScale(part.Orientation).Value;
                    var scale = new float3(scale3X3.c0.x, scale3X3.c1.y, scale3X3.c2.z);
                    switch (collider.Value.Type)
                    {
                        case ColliderType.Convex:
                            ecb.SetComponent(entity, new PhysicsCollider{ Value = collider.As<ConvexCollider>().Scaled(scale) });
                            break;
                        case ColliderType.Box:
                            ecb.SetComponent(entity, new PhysicsCollider{ Value = collider.As<BoxCollider>().Scaled(scale) });
                            break;
                        default:
                            Debug.LogError($"No collider scaling method provided for type {collider.Value.Type}");
                            break;
                    }
                }

                if (part.Area.Size.x == part.Area.Size.y &&
                    part.Area.Size.x == part.Area.Size.z)
                    transform.Scale = part.Area.Size.x;
                else
                    ecb.AddComponent(entity, newGridItem.Area.ToScale(part.Orientation));
            }

            ecb.AddComponent(entity, new Parent { Value = grid });
            ecb.SetComponent(entity, transform);
            ecb.SetComponent(entity, newGridItem);
            
            if (part.Color != default)
            {
                ecb.AddComponent(entity, new URPMaterialPropertyBaseColor{Value = new float4(part.Color.r, part.Color.g, part.Color.b, 1) });
            }

            updates++;
            affectedArea.Min = math.min(newGridItem.Area.Min, affectedArea.Min);
            affectedArea.Max = math.max(newGridItem.Area.Max, affectedArea.Max);
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