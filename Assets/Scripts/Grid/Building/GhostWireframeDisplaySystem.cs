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
        RequireForUpdate<RoomsLookup>();
    }

    protected override void OnStopRunning()
    {
        EntityManager.DestroyEntity(GetEntityQuery(typeof(GhostWireframe)));
        EntityManager.DestroyEntity(GetEntityQuery(typeof(GhostDisplayBox)));
    }

    protected override void OnUpdate()
    {
        var roomsLookup = SystemAPI.GetSingleton<RoomsLookup>();
        var builder = SystemAPI.GetSingleton<GhostPlacementBuilder>();
        var hasWireframe = SystemAPI.TryGetSingletonEntity<GhostWireframe>(out var wireframe);
        var hasDisplayBox = SystemAPI.TryGetSingletonEntity<GhostDisplayBox>(out var displayBox);
        var hasGhost = SystemAPI.TryGetSingletonEntity<Ghost>(out var ghost);
        var wantScaleGhost = SystemAPI.GetSingleton<PlayerTargetModeSettings>().Mode == PlayerTargetSystem.PlayerTargetMode.ScaleGhost;
        if (!hasGhost)
        {
            EntityManager.DestroyEntity(GetEntityQuery(typeof(GhostDisplayBox)));
            hasDisplayBox = false;
        }
        if (builder.Target.Grid == Entity.Null || roomsLookup.OccupiedCells.Contains((builder.Target.Grid, builder.DragStartCell)))
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
            var position = GridStructure.CellCenterToLocal(builder.DragStartCell);
            position -= (float3)builder.Target.Normal * GridStructure.CELL_SIZE * 0.5f;
            EntityManager.SetComponentData(wireframe, new LocalTransform
            {
                Position = position,
                Rotation = builder.CurrentRotation,
                Scale = 1
            });
        }
    }
}