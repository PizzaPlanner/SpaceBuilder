using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine.LowLevel;

public struct LocalPlayer : IComponentData { }

public struct ReferenceFrameOrigin : IComponentData { }

public struct ReferenceFrame : IComponentData
{
    public LocalToGalaxy PreviousLtg;
    public LocalToGalaxy CurrentLtg;
    public LocalToWorld PreviousLtw;
    public LocalToWorld CurrentLtw;
    public Entity Target;
}

[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial class EarlyReferenceFrameSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<ReferenceFrame>();
        RequireForUpdate<LocalPlayer>();

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
public partial class LateReferenceFrameSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<ReferenceFrame>();
        RequireForUpdate<LocalPlayer>();
    }

    protected override void OnUpdate()
    {
        var localPlayer = SystemAPI.GetSingletonEntity<LocalPlayer>();
        var refFrame = SystemAPI.GetSingletonRW<ReferenceFrame>();

        var prevTarget = refFrame.ValueRW.Target;
        if (SystemAPI.HasComponent<Docked>(localPlayer))
            refFrame.ValueRW.Target = SystemAPI.GetComponent<Docked>(localPlayer).Target;
        else
            refFrame.ValueRW.Target = localPlayer;

        var gridLtg = SystemAPI.GetComponent<LocalToGalaxy>(refFrame.ValueRW.Target);
        var gridLtw = SystemAPI.GetComponent<LocalToWorld>(refFrame.ValueRW.Target);
        refFrame.ValueRW.CurrentLtg = gridLtg;
        refFrame.ValueRW.CurrentLtw = new LocalToWorld { Value = float4x4.Translate(gridLtw.Position) };
        
        if (refFrame.ValueRW.Target != prevTarget)
        {
            if (prevTarget != Entity.Null)
                EntityManager.RemoveComponent<ReferenceFrameOrigin>(prevTarget);
            if(refFrame.ValueRW.Target != Entity.Null)
                EntityManager.AddComponent<ReferenceFrameOrigin>(refFrame.ValueRW.Target);
        }
    }
}