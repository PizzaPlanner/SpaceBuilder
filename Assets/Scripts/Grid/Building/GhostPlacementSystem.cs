using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

public struct Ghost : IComponentData
{
    public bool Buildable;
}

[UpdateBefore(typeof(TransformSystemGroup))]
[UpdateBefore(typeof(BuildingSystem))]
[UpdateAfter(typeof(PlayerTargetSystem))]
public partial class GhostPlacementSystem : SystemBase
{
    EntityQuery _ghostQuery;

    protected override void OnCreate()
    {
        RequireForUpdate<BuilderMode_Blueprint>();
        RequireForUpdate<LocalPlayer>();
        RequireForUpdate<IdentifiableMappingSystem.Loaded>();
        RequireForUpdate<PlayerTargetModeSettings>();

        _ghostQuery = GetEntityQuery(typeof(Ghost));
    }

    protected override void OnStartRunning()
    {
        EntityManager.CreateEntity(typeof(GhostPlacementBuilder));
        SystemAPI.SetSingleton(new GhostPlacementBuilder{
            CurrentRotation = quaternion.identity
        });
        SystemAPI.SetSingleton(new PlayerTargetModeSettings
            { Mode = PlayerTargetSystem.PlayerTargetMode.Build });
    }

    protected override void OnStopRunning()
    {
        EntityManager.DestroyEntity(_ghostQuery.ToEntityArray(Allocator.Temp));
        if(SystemAPI.TryGetSingletonEntity<GhostPlacementBuilder>(out var builder))
            EntityManager.DestroyEntity(builder);
        SystemAPI.SetSingleton(new PlayerTargetModeSettings
            { Mode = PlayerTargetSystem.PlayerTargetMode.Interactions });
    }

    protected override void OnUpdate()
    {
        var builder = SystemAPI.GetSingletonRW<GhostPlacementBuilder>();
        var target = SystemAPI.GetComponent<Target>(SystemAPI.GetSingletonEntity<LocalPlayer>());

        var wantBuildGhost = Input.GetMouseButtonUp(0);
        var wantScaleGhost = (Input.GetMouseButton(0) || wantBuildGhost) && builder.ValueRW.PrefabPart.Scalable;
        var targetMode = wantScaleGhost
            ? PlayerTargetSystem.PlayerTargetMode.ScaleGhost
            : PlayerTargetSystem.PlayerTargetMode.Build;
        if (!wantScaleGhost)
        {
            builder.ValueRW.Target = target.GridHit;
            builder.ValueRW.DragStartCell = target.GridHit.Cell + target.GridHit.Normal;
        }
        if(target.GridHit.Grid == builder.ValueRW.Target.Grid)
            builder.ValueRW.DragEndCell = target.GridHit.Cell + target.GridHit.Normal;
        
        SystemAPI.SetSingleton(new PlayerTargetModeSettings { Mode =  targetMode });
        
        if (builder.ValueRW.RebuildGhost)
            EntityManager.DestroyEntity(_ghostQuery.ToEntityArray(Allocator.Temp));

        builder = SystemAPI.GetSingletonRW<GhostPlacementBuilder>();
        builder.ValueRW.RebuildGhost = false;
        if (builder.ValueRW.Target.Grid == Entity.Null || builder.ValueRW.PrefabIdToBuild == 0)
            return;

        var buildable = true; // TODO

        var ecb = new EntityCommandBuffer(WorldUpdateAllocator);
        var pendingParts = SystemAPI.GetBuffer<BuildingPartInfo>(builder.ValueRW.Target.Grid);
        if(!SystemAPI.TryGetSingletonEntity<Ghost>(out var ghost))
        {
            if(builder.ValueRW.ToPartInfo.ToGridItem.Area.IsValid)
                pendingParts.Add(builder.ValueRW.ToPartInfo);
        }
        else
        {
            ecb.SetComponent(ghost, new Ghost { Buildable = buildable });
            ecb.SetComponent(ghost, builder.ValueRW.ToPartInfo.ToTransform);
            if(builder.ValueRW.PrefabPart.Scalable)
                ecb.SetComponent(ghost, builder.ValueRW.ToPartInfo.ToScale);
            if (wantBuildGhost)
            {
                var newPart = builder.ValueRW.ToPartInfo;
                newPart.IsGhost = false;
                pendingParts.Add(newPart);
                ecb.DestroyEntity(ghost);
            }
        }
        
        if(Input.GetKeyDown(KeyCode.R))
        {
            var sign = Input.GetKeyDown(KeyCode.LeftShift) ? -1 : 1;
            var rotation = quaternion.RotateY(math.PI * sign * 0.5f);
            builder.ValueRW.CurrentRotation = math.mul(rotation, builder.ValueRW.CurrentRotation);
        }
        ecb.Playback(EntityManager);
    }
}