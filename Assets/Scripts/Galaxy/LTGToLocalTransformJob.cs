using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public struct TooFarAway : IComponentData, IEnableableComponent { }

[WithNone(typeof(ReferenceFrameOrigin), typeof(Parent), typeof(Docked))]
[WithOptions(EntityQueryOptions.IncludeDisabledEntities)]
public partial struct LTGToLocalTransformJob : IJobEntity
{
    public ReferenceFrame refFrame;
    public float4x4 refFramePreviousLtwInverse;

    void Execute(ref LocalTransform transform, in LocalToGalaxy ltg, EnabledRefRW<TooFarAway> tooFarAway)
    {
        var delta = ltg - refFrame.PreviousLtg;
        var ltgDiff = delta.Distance;
        tooFarAway.ValueRW = !math.all(delta.DistanceMeasureable);
        var relativeRotation = math.mul(new quaternion(refFramePreviousLtwInverse), transform.Rotation);

        transform.Position = (float3)ltgDiff;
        transform.Rotation = math.mul(relativeRotation, refFrame.CurrentLtw.Rotation);
    }
}