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
        
        var newPart = new BuildingPartInfo
        {
            Id = builder.ValueRW.PrefabIdToBuild,
            Orientation = new RightAngleRotation(builder.ValueRW.CurrentRotation),
            IsGhost = true,
            Area = builder.ValueRW.Area
        };
        
        var wantScaleGhost = Input.GetMouseButton(0) && builder.ValueRW.PrefabPart.Scalable;
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
            pendingParts.Add(newPart);
        }
        else
        {
            ecb.SetComponent(ghost, new Ghost { Buildable = buildable });
            ecb.SetComponent(ghost, LocalTransform.FromPositionRotation(
                GridStructure.CellToLocal(builder.ValueRW.DragStartCell),
                builder.ValueRW.CurrentRotation));
        }
        if (Input.GetMouseButtonUp(0))
        {
            newPart.IsGhost = false;
            pendingParts.Add(newPart);
            ecb.DestroyEntity(ghost);
        }
        
        var input = SystemAPI.GetSingleton<InputSystem.InputData>();
        if (input.ScrollMode == InputSystem.ScrollMode.Rotate)
        {
            var sign = (int)math.sign(input.Scroll);
            var rotation = quaternion.identity;
            if (Input.GetKey(KeyCode.Alpha1))
                rotation = math.mul(rotation, quaternion.RotateX(math.PI * sign * 0.5f));
            if (Input.GetKey(KeyCode.Alpha2))
                rotation = math.mul(rotation, quaternion.RotateY(math.PI * sign * 0.5f));
            if (Input.GetKey(KeyCode.Alpha3))
                rotation = math.mul(rotation, quaternion.RotateZ(math.PI * sign * 0.5f));

            builder.ValueRW.CurrentRotation = rotation;
        }
        ecb.Playback(EntityManager);
    }
}