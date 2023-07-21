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
}


public struct ReferenceFrameOrigin : IComponentData { }

[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial class EarlyReferenceFrameSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<ReferenceFrame>();

        EntityManager.CreateEntity(typeof(ReferenceFrame));
        
        var defaultLtw = new LocalToWorld { Value = float4x4.TRS(0, quaternion.identity, 1) };
        SystemAPI.SetSingleton(new ReferenceFrame
        {
            CurrentLtw = defaultLtw, 
            PreviousLtw = defaultLtw
        });
    }

    protected override void OnUpdate()
    {
        var refFrame = SystemAPI.GetSingletonRW<ReferenceFrame>();
        refFrame.ValueRW.PreviousLtg = refFrame.ValueRW.CurrentLtg;
        refFrame.ValueRW.PreviousLtw = refFrame.ValueRW.CurrentLtw;
    }
}


[UpdateInGroup(typeof(TransformSystemGroup))]
[UpdateBefore(typeof(TRSToLocalToGalaxySystem))]
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
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                           .CreateCommandBuffer(state.WorldUnmanaged);

        var refFrame = SystemAPI.GetSingletonRW<ReferenceFrame>();
        var prevOrigin = refFrame.ValueRW.Origin;
        foreach (var (transform, e) in SystemAPI.Query<RefRW<LocalTransform>>()
                                        .WithChangeFilter<ReferenceFrameOrigin>()
                                        .WithAll<LocalToGalaxy, ReferenceFrameOrigin>().WithEntityAccess())
        {
            if (e != prevOrigin)
            {
                refFrame.ValueRW.Origin = e;
                transform.ValueRW = LocalTransform.Identity;
            }
        }

        var gridLtg = SystemAPI.GetComponent<LocalToGalaxy>(refFrame.ValueRW.Origin);
        var gridLtw = SystemAPI.GetComponent<LocalToWorld>(refFrame.ValueRW.Origin);
        refFrame.ValueRW.CurrentLtg = gridLtg;
        refFrame.ValueRW.CurrentLtw = new LocalToWorld { Value = float4x4.Translate(gridLtw.Position) };
        
        if (refFrame.ValueRW.Origin != prevOrigin)
        {
            if (prevOrigin != Entity.Null)
                ecb.RemoveComponent<ReferenceFrameOrigin>(prevOrigin);
        }
    }
}