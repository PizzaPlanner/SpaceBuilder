using Unity.Burst;
using Unity.Collections;
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

    internal float DurationSinceLastChange;
}


public struct ReferenceFrameOrigin : IComponentData { }

public struct PromoteToReferenceFrame : IComponentData
{
    public bool MakePreviousFrameChildOfNewFrame;
}

[UpdateInGroup(typeof(InitializationSystemGroup))]
[UpdateAfter(typeof(EarlyReferenceFrameSystem))]
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
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<ReferenceFrame>();
        state.EntityManager.CreateEntity(typeof(ReferenceFrame));
        
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
        var hasNewFrame = SystemAPI.TryGetSingletonEntity<PromoteToReferenceFrame>(out var newFrame);
        var makeOldFrameAsChild = false;
        if (hasNewFrame)
        {
            makeOldFrameAsChild = SystemAPI.GetComponent<PromoteToReferenceFrame>(newFrame).MakePreviousFrameChildOfNewFrame;
            state.EntityManager.RemoveComponent<PromoteToReferenceFrame>(newFrame);
        }
        var refFrame = SystemAPI.GetSingletonRW<ReferenceFrame>();
        refFrame.ValueRW.PreviousLtg = refFrame.ValueRW.CurrentLtg;
        refFrame.ValueRW.PreviousLtw = refFrame.ValueRW.CurrentLtw;
        refFrame.ValueRW.DurationSinceLastChange += SystemAPI.Time.DeltaTime;

        if (!hasNewFrame)
            return;

        if (refFrame.ValueRW.DurationSinceLastChange < 1)
            return;

        var ecb = new EntityCommandBuffer(Allocator.Temp);
        
        var prevFrame = refFrame.ValueRW.Origin;
        var newToWorld = SystemAPI.GetComponent<LocalToWorld>(newFrame).Value;
        var worldToNew = math.inverse(newToWorld);
        var prevToWorld = refFrame.ValueRW.PreviousLtw.Value;
        var worldToPrev = math.inverse(prevToWorld);
        
        ecb.AddComponent<ReferenceFrameOrigin>(newFrame);
        ecb.SetComponent(newFrame, LocalTransform.Identity); // Identity = newToLocal * localToNew

        Debug.Log($"Origin Change: {prevFrame} -> {newFrame}");
        if (prevFrame != Entity.Null)
        {
            ecb.RemoveComponent<ReferenceFrameOrigin>(prevFrame);
            
            // Player -> Ship: Parent player to ship and update transform so LTW of player is the same as before
            if (makeOldFrameAsChild)
            {
                ecb.AddComponent(prevFrame, new Parent { Value = newFrame });
                var prevToNew = math.mul(prevToWorld, worldToNew);
                ecb.SetComponent(prevFrame, LocalTransform.FromMatrix(prevToNew));
                Debug.DrawRay(prevToWorld.TransformPoint(0), math.mul(worldToNew, prevToNew).Forward(), Color.yellow, 5);
            }
            
             
            // Ship -> Player: Get new LTG for player as ShipLTG + PlayerPosition in Ship
            if (!SystemAPI.HasComponent<LocalToGalaxy>(newFrame))
            {
                var ltgForNewFrame = refFrame.ValueRW.PreviousLtg;
                ltgForNewFrame += math.mul(prevToWorld.Rotation(),
                    SystemAPI.GetComponent<LocalTransform>(newFrame).Position);
                ecb.AddComponent(newFrame, ltgForNewFrame);
            }
        }
        
        refFrame.ValueRW.DurationSinceLastChange = 0;
        refFrame.ValueRW.Origin = newFrame;
        ecb.Playback(state.EntityManager);
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
        refFrame.ValueRW.CurrentLtw = new LocalToWorld { Value = float4x4.Translate(gridLtw.Position) };
        Debug.DrawRay(gridLtw.Position, gridLtw.Up, Color.green);
        Debug.DrawRay(gridLtw.Position, gridLtw.Forward, Color.blue);
    }
}