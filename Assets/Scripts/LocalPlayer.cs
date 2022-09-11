using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine.LowLevel;

[GenerateAuthoringComponent]
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
        RequireSingletonForUpdate<ReferenceFrame>();
        RequireSingletonForUpdate<LocalPlayer>();

        EntityManager.CreateEntity(typeof(ReferenceFrame));
        
        var defaultLtw = new LocalToWorld { Value = float4x4.TRS(0, quaternion.identity, 1) };
        SetSingleton(new ReferenceFrame
        {
            CurrentLtw = defaultLtw, 
            PreviousLtw = defaultLtw
        });
    }

    protected override void OnUpdate()
    {
        var refFrame = GetSingleton<ReferenceFrame>();
        refFrame.PreviousLtg = refFrame.CurrentLtg;
        refFrame.PreviousLtw = refFrame.CurrentLtw;
        SetSingleton(refFrame);
    }
}


[UpdateInGroup(typeof(TransformSystemGroup))]
[UpdateBefore(typeof(TRSToLocalToGalaxySystem))]
public partial class LateReferenceFrameSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireSingletonForUpdate<ReferenceFrame>();
        RequireSingletonForUpdate<LocalPlayer>();
    }

    protected override void OnUpdate()
    {
        var localPlayer = GetSingletonEntity<LocalPlayer>();
        var refFrame = GetSingleton<ReferenceFrame>();

        var prevTarget = refFrame.Target;
        if (HasComponent<Docked>(localPlayer))
            refFrame.Target = GetComponent<Docked>(localPlayer).Target;
        else
            refFrame.Target = localPlayer;

        if (refFrame.Target != prevTarget)
        {
            if (prevTarget != Entity.Null)
                EntityManager.RemoveComponent<ReferenceFrameOrigin>(prevTarget);
            if(refFrame.Target != Entity.Null)
                EntityManager.AddComponent<ReferenceFrameOrigin>(refFrame.Target);
        }

        var gridLtg = GetComponent<LocalToGalaxy>(refFrame.Target);
        var gridLtw = GetComponent<LocalToWorld>(refFrame.Target);
        refFrame.CurrentLtg = gridLtg;
        refFrame.CurrentLtw = new LocalToWorld { Value = float4x4.Translate(gridLtw.Position) };

        SetSingleton(refFrame);
    }
}