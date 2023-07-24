using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public struct GhostWireframe : IComponentData { }
public struct GhostDisplayBox : IComponentData { }

[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class GhostWireframeDisplaySystem : SystemBase
{
    EntityQuery _wireframePrefabQuery, _boxPrefabQuery;

    protected override void OnCreate()
    {
        _wireframePrefabQuery = SystemAPI.QueryBuilder().WithAll<GhostWireframe, Prefab>()
                                         .WithOptions(EntityQueryOptions.IncludePrefab)
                                         .Build();
        _boxPrefabQuery = SystemAPI.QueryBuilder().WithAll<GhostDisplayBox, Prefab>()
                                         .WithOptions(EntityQueryOptions.IncludePrefab)
                                         .Build();
        RequireForUpdate(_wireframePrefabQuery);
        RequireForUpdate(_boxPrefabQuery);
        RequireForUpdate<GhostPlacementBuilder>();
    }

    protected override void OnStopRunning()
    {
        EntityManager.DestroyEntity(GetEntityQuery(typeof(GhostWireframe)));
        EntityManager.DestroyEntity(GetEntityQuery(typeof(GhostDisplayBox)));
    }

    protected override void OnUpdate()
    {
        var builder = SystemAPI.GetSingleton<GhostPlacementBuilder>();
        var hasWireframe = SystemAPI.TryGetSingletonEntity<GhostWireframe>(out var wireframe);
        var hasDisplayBox = SystemAPI.TryGetSingletonEntity<GhostDisplayBox>(out var displayBox);
        var hasGhost = SystemAPI.TryGetSingletonEntity<Ghost>(out var ghost);
        var wantScaleGhost = SystemAPI.GetSingleton<PlayerTargetModeSettings>().Mode == PlayerTargetSystem.PlayerTargetMode.ScaleGhost;
        if(!hasGhost)
            EntityManager.DestroyEntity(GetEntityQuery(typeof(GhostDisplayBox)));
        if (builder.Target.Grid == Entity.Null)
        {
            EntityManager.DestroyEntity(GetEntityQuery(typeof(GhostWireframe)));
            return;
        }

        if (!hasWireframe)
        {
            wireframe = EntityManager.Instantiate(_wireframePrefabQuery.GetSingletonEntity());
            EntityManager.AddComponentData(wireframe, new Parent { Value = builder.Target.Grid });
        }
        else
            SystemAPI.SetComponent(wireframe, new Parent { Value = builder.Target.Grid });
        
        if (!hasDisplayBox && hasGhost)
        {
            displayBox = EntityManager.Instantiate(_boxPrefabQuery.GetSingletonEntity());
            EntityManager.AddComponent<Parent>(displayBox);
            EntityManager.AddComponent<PostTransformMatrix>(displayBox);
            hasDisplayBox = true;
        }
        
        if (hasDisplayBox)
        {
            SystemAPI.SetComponent(displayBox, new Parent { Value = builder.Target.Grid });
            var area = builder.Area;
            SystemAPI.SetComponent(displayBox, new PostTransformMatrix
            {
                Value = float4x4.TRS(
                    GridStructure.CellToLocal(area.Origin),
                    quaternion.identity, 
                    area.Size)
            });
        }

        if (!wantScaleGhost)
        {
            var up = (float3)builder.Target.Normal;
            var forward = up.x == 0 ? new float3(1, 0, 0) : new float3(0, 1, 0);
            if (Input.GetKey(KeyCode.Alpha2))
            {
                up = forward;
                forward = builder.Target.Normal;
            }
            
            var position = GridStructure.CellCenterToLocal(builder.DragStartCell);
            position -= up * GridStructure.CELL_SIZE * 0.5f;
            EntityManager.SetComponentData(wireframe, new LocalTransform
            {
                Position = position,
                Rotation = quaternion.LookRotation(forward, up),
                Scale = 1
            });
        }
    }
}