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
        RequireForUpdate<LocalPlayer>();
        RequireForUpdate<IdentifiableMappingSystem.Loaded>();
        RequireForUpdate<ControlScheme.Building.Build>();
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
        var rooms = SystemAPI.GetSingleton<RoomsLookup>();
        var input = SystemAPI.GetSingleton<InputData>();

        var wantBuildGhost = input.WantBuild;
        var wantScaleGhost = (input.WantDrag || wantBuildGhost) && builder.ValueRW.PrefabPart.Scalable;
        var targetMode = wantScaleGhost
            ? PlayerTargetSystem.PlayerTargetMode.ScaleGhost
            : PlayerTargetSystem.PlayerTargetMode.Build;
        if (!wantScaleGhost)
        {
            builder.ValueRW.Target = target.GridHit;
            builder.ValueRW.DragStartCell = GetNextBestCell(target.GridHit, rooms);
            builder.ValueRW.DragAreaUpOffset = 0;
        }
        else if(input.ScrollMode == ScrollMode.Scale)
            builder.ValueRW.DragAreaUpOffset += (int)math.sign(input.Scroll);

        if (target.GridHit.Grid == builder.ValueRW.Target.Grid)
            builder.ValueRW.DragEndCell = GetNextBestCell(target.GridHit, rooms);
        
        SystemAPI.SetSingleton(new PlayerTargetModeSettings { Mode =  targetMode });
        
        if (builder.ValueRW.RebuildGhost)
            EntityManager.DestroyEntity(_ghostQuery.ToEntityArray(Allocator.Temp));

        builder = SystemAPI.GetSingletonRW<GhostPlacementBuilder>();
        builder.ValueRW.RebuildGhost = false;
        if (builder.ValueRW.Target.Grid == Entity.Null || builder.ValueRW.PrefabIdToBuild == 0)
            return;

        // Check if space is already occupied
        var partArea = builder.ValueRW.ToPartInfo.ToArea;
        var buildable = partArea.IsValid;
        for (int x = partArea.Min.x; x <= partArea.Max.x && buildable; x++)
        for (int y = partArea.Min.y; y <= partArea.Max.y && buildable; y++)
        for (int z = partArea.Min.z; z <= partArea.Max.z && buildable; z++)
            buildable &= !rooms.OccupiedCells.Contains((builder.ValueRW.Target.Grid, new int3(x, y, z)));

        var ecb = new EntityCommandBuffer(WorldUpdateAllocator);
        var pendingParts = SystemAPI.GetBuffer<BuildingPartInfo>(builder.ValueRW.Target.Grid);
        if(!SystemAPI.TryGetSingletonEntity<Ghost>(out var ghost))
        {
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
                ecb.DestroyEntity(ghost);
                var newPart = builder.ValueRW.ToPartInfo;
                newPart.IsGhost = false;
                pendingParts.Add(newPart);
            }
        }
        
        if(input.ScrollMode == ScrollMode.Rotate)
        {
            var sign = input.Scroll;
            var rotation = quaternion.RotateY(math.PI * sign * 0.5f);
            builder.ValueRW.CurrentRotation = math.mul(rotation, builder.ValueRW.CurrentRotation);
        }
        ecb.Playback(EntityManager);
    }

    int3 GetNextBestCell(GridHitInfo hit, RoomsLookup rooms)
    {
        var candidate = hit.Cell + (rooms.OccupiedCells.Contains((hit.Grid, hit.Cell)) ? hit.Normal : 0);
        return candidate;
    }
}