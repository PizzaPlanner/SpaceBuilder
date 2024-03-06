using Galaxy.SectorLoading;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public struct ReferenceFrame : IComponentData
{
    public LocalToGalaxy PreviousLtg;
    public LocalToGalaxy CurrentLtg;
    public LocalToWorld PreviousLtw;
    public LocalToWorld CurrentLtw;
    public Entity Origin;

    public float DurationSinceLastChange;
    public double3 DeltaLtg => (CurrentLtg - PreviousLtg).Distance;
}


public struct ReferenceFrameOrigin : IComponentData { }

public struct PromoteToReferenceFrame : IComponentData
{
    public bool MakePreviousFrameChildOfNewFrame;
}

[UpdateInGroup(typeof(InitializationSystemGroup), OrderLast = true)]
[UpdateAfter(typeof(EndInitializationEntityCommandBufferSystem))]
partial struct UpdateReferenceFrameTransformComponents : ISystem
{
    EntityQuery _refOriginParentQuery, _ltgParentQuery;

    public void OnCreate(ref SystemState state)
    {
        _refOriginParentQuery = state.GetEntityQuery(typeof(ReferenceFrameOrigin), typeof(Parent));
        _ltgParentQuery = state.GetEntityQuery(typeof(LocalToGalaxy), typeof(Parent));
    }

    public void OnUpdate(ref SystemState state)
    {
        state.EntityManager.RemoveComponent<Parent>(_refOriginParentQuery);
        state.EntityManager.RemoveComponent<LocalToGalaxy>(_ltgParentQuery);
    }
}

[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial struct EarlyReferenceFrameSystem : ISystem
{
    EntityQuery _promotionQuery;
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<ReferenceFrame>();
        state.EntityManager.CreateEntity(typeof(ReferenceFrame));

        _promotionQuery = SystemAPI.QueryBuilder().WithAll<PromoteToReferenceFrame>().Build();
        
        var defaultLtw = new LocalToWorld { Value = float4x4.TRS(0, quaternion.identity, 1) };
        SystemAPI.SetSingleton(new ReferenceFrame
        {
            CurrentLtw = defaultLtw, 
            PreviousLtw = defaultLtw,
            DurationSinceLastChange = float.PositiveInfinity
        });
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var ecb = SystemAPI.GetSingleton<EndInitializationEntityCommandBufferSystem.Singleton>()
                           .CreateCommandBuffer(state.WorldUnmanaged);
        
        ecb.RemoveComponent<PromoteToReferenceFrame>(_promotionQuery, EntityQueryCaptureMode.AtPlayback);
        
        var hasNewFrame = SystemAPI.TryGetSingletonEntity<PromoteToReferenceFrame>(out var newFrame);
        var makeOldFrameAsChild = false;
        if (hasNewFrame)
        {
            makeOldFrameAsChild = SystemAPI.GetComponent<PromoteToReferenceFrame>(newFrame).MakePreviousFrameChildOfNewFrame;
        }
        var refFrame = SystemAPI.GetSingletonRW<ReferenceFrame>();
        refFrame.ValueRW.PreviousLtg = refFrame.ValueRW.CurrentLtg;
        refFrame.ValueRW.PreviousLtw = refFrame.ValueRW.CurrentLtw;
        refFrame.ValueRW.DurationSinceLastChange += SystemAPI.Time.DeltaTime;

        if (!hasNewFrame)
            return;

        if (refFrame.ValueRW.DurationSinceLastChange < 1)
            return;

        var prevFrame = refFrame.ValueRW.Origin;
        var localToNew = SystemAPI.GetComponent<LocalToWorld>(newFrame).Value;
        var newToLocal = math.inverse(localToNew);
        var localToPrev = refFrame.ValueRW.PreviousLtw.Value;

        if (prevFrame != Entity.Null)
        {
            ecb.RemoveComponent<ReferenceFrameOrigin>(prevFrame);
            
            // Player -> Ship: Parent player to ship and update transform so LTW of player is the same as before
            if (makeOldFrameAsChild)
            {
                var newToPrev = math.mul(newToLocal, localToPrev);
                var transform = LocalTransform.FromMatrix(newToPrev);
                ecb.SetComponent(prevFrame, transform);
                ecb.AddComponent(prevFrame, new Parent { Value = newFrame });
            }
            
            // Ship -> Player: Get new LTG for player as ShipLTG + PlayerPosition in Ship
            if (!SystemAPI.HasComponent<LocalToGalaxy>(newFrame))
            {
                var diffToLocal = math.mul(localToPrev.Rotation(),
                    SystemAPI.GetComponent<LocalTransform>(newFrame).Position);
                var ltgForNewFrame = refFrame.ValueRW.PreviousLtg;
                ltgForNewFrame += diffToLocal;
                
                ecb.AddComponent(newFrame, ltgForNewFrame);
            }
        }
        
        ecb.AddComponent<ReferenceFrameOrigin>(newFrame);
        ecb.SetComponent(newFrame, LocalTransform.FromRotation(localToNew.Rotation()));
        
        refFrame.ValueRW.DurationSinceLastChange = 0;
        refFrame.ValueRW.Origin = newFrame;
    }
}


[UpdateInGroup(typeof(TransformSystemGroup))]
[UpdateBefore(typeof(TRSToLocalToGalaxySystem))]
[UpdateAfter(typeof(LocalToWorldSystem))]
public partial struct LateReferenceFrameSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<ReferenceFrame>();
        state.RequireForUpdate<ReferenceFrameOrigin>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var refFrame = SystemAPI.GetSingletonRW<ReferenceFrame>();
        var gridLtg = SystemAPI.GetComponent<LocalToGalaxy>(refFrame.ValueRW.Origin);
        var gridLtw = SystemAPI.GetComponent<LocalToWorld>(refFrame.ValueRW.Origin);
        refFrame.ValueRW.CurrentLtg = gridLtg;
        refFrame.ValueRW.CurrentLtw = new LocalToWorld { Value = float4x4.TRS(gridLtw.Position, gridLtw.Rotation, 1) };
    }
}