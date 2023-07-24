using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Graphics;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Extensions;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;


[WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)]
public partial struct BuildJob : IJobEntity
{
    public EntityCommandBuffer ecb;
    public RoomsLookup Rooms; 
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
                originalGridItem.Area.Extents.Rotated(part.Orientation);
                
            // Check if space is already occupied
            var canBuild = newGridItem.Area.IsValid;
            for (int x = newGridItem.Area.Min.x; x <= newGridItem.Area.Max.x; x++)
            for (int y = newGridItem.Area.Min.y; y <= newGridItem.Area.Max.y; y++)
            for (int z = newGridItem.Area.Min.z; z <= newGridItem.Area.Max.z; z++)
                canBuild &= !Rooms.CellRoomMap.ContainsKey((grid, new int3(x, y, z)));
            
            if (!canBuild && !part.IsGhost)
            {
                Debug.LogWarning($"Discarded pending part in invalid or occupied area {newGridItem.Area}");
                continue;
            }

            // Create Part
            var entity = ecb.Instantiate(prefab);

            var transform = LocalTransform.FromPositionRotation(
                GridStructure.CellToLocal(newGridItem.Area.Origin),
                part.Orientation);
            if (originalGridItem.Scalable && math.any(newGridItem.Area.Extents != 0))
            {
                if (math.any(newGridItem.Area.Size != 1) && colliders.HasComponent(prefab))
                {
                    transform.Position -= GridStructure.CellCenterToLocal(0);
                    var collider = colliders[prefab].Value;
                    var scaleUnrotated = newGridItem.Area.Size;
                    switch (collider.Value.Type)
                    {
                        case ColliderType.Convex:
                            ecb.SetComponent(entity,
                                new PhysicsCollider { Value = collider.As<ConvexCollider>().Scaled(scaleUnrotated) });
                            break;
                        case ColliderType.Box:
                            ecb.SetComponent(entity,
                                new PhysicsCollider { Value = collider.As<BoxCollider>().Scaled(scaleUnrotated) });
                            break;
                        default:
                            Debug.LogError($"No collider scaling method provided for type {collider.Value.Type}");
                            break;
                    }
                }

                var postTransform = new PostTransformMatrix
                {
                    Value = float4x4.TRS(GridStructure.CellCenterToLocal(0), 
                        quaternion.identity, 
                        newGridItem.Area.Size)
                };
                ecb.SetComponent(entity, postTransform);
            }

            ecb.AddComponent(entity, new Parent { Value = grid });
            ecb.SetComponent(entity, transform);
            ecb.SetComponent(entity, newGridItem);
            
            if (part.Color != default)
            {
                ecb.AddComponent(entity, new URPMaterialPropertyBaseColor{Value = new float4(part.Color.r, part.Color.g, part.Color.b, 1) });
            }

            if (part.IsGhost)
            {
                ecb.AddComponent(entity,  
                    new FixGhostVisualsAndPhysicsSystem.FixGhostandChildren { GhostBuildable = canBuild });
            }
            else
            {
                updates++;
                affectedArea.Min = math.min(newGridItem.Area.Min, affectedArea.Min);
                affectedArea.Max = math.max(newGridItem.Area.Max, affectedArea.Max);
                
                for (int x = newGridItem.Area.Min.x; x <= newGridItem.Area.Max.x; x++)
                for (int y = newGridItem.Area.Min.y; y <= newGridItem.Area.Max.y; y++)
                for (int z = newGridItem.Area.Min.z; z <= newGridItem.Area.Max.z; z++)
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