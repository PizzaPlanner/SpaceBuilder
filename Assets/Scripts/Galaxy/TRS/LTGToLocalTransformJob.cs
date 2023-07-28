using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public struct TooFarAway : IComponentData, IEnableableComponent { }

[WithNone(typeof(ReferenceFrameOrigin), typeof(Parent), typeof(CelestialBody))]
[WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IgnoreComponentEnabledState)]
public partial struct LTGToLocalTransformJob : IJobEntity
{
    public ReferenceFrame refFrame;

    void Execute(ref LocalTransform transform, ref LocalToWorld ltw, in LocalToGalaxy ltg, EnabledRefRW<TooFarAway> tooFarAway)
    {
        var delta = ltg - refFrame.CurrentLtg;
        var ltgDiff = delta.Distance;
        tooFarAway.ValueRW = !math.all(delta.DistanceMeasureable);
        transform.Position = (float3)ltgDiff;

        ltw.Value = transform.ToMatrix();
    }
}