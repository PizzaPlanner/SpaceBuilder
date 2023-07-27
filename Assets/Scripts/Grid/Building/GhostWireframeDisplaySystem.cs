using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

public struct GhostWireframe : IComponentData { }

[MaterialProperty("_GhostStatus", -1)]
public struct GhostDisplayBox : IComponentData, IQueryTypeParameter
{
    public float Value;
}

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
            var buildable = SystemAPI.GetComponent<Ghost>(ghost).Buildable;
            SystemAPI.SetComponent(displayBox, new Parent { Value = builder.Target.Grid });
            SystemAPI.SetComponent(displayBox, builder.ToPartInfo.ToTransform);
            SystemAPI.SetComponent(displayBox, builder.ToPartInfo.ToScaleForGhostBox);
            SystemAPI.SetComponent(displayBox, new GhostDisplayBox { Value = buildable ? 0 : 1 });
        }
        
        var up = math.mul(builder.PlaneRotation, math.up());
        var forward = math.mul(builder.PlaneRotation, math.forward());
        var position = GridStructure.CellCenterToLocal(builder.DragStartCell);
        position += up * 0.01f - up * GridStructure.CELL_SIZE * 0.5f;

        EntityManager.SetComponentData(wireframe, new LocalTransform
        {
            Position = position,
            Rotation = quaternion.LookRotation(forward, up),
            Scale = 1
        });
    }
}