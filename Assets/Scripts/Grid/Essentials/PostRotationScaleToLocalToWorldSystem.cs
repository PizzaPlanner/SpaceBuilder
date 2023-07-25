using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

// Calculate LTW from TSR instead of TRS. This causes the scale to be in Parent-Space rather than Object-Space.
public partial struct PostRotationScaleJob : IJobEntity
{
    [ReadOnly][NativeDisableContainerSafetyRestriction] public ComponentLookup<LocalToWorld> ltwLookup;

    void Execute(in Parent parent, in LocalTransform transform, in PostRotationScale rotationScale, ref LocalToWorld ltw)
    {
        if (parent.Value == Entity.Null || !ltwLookup.HasComponent(parent.Value))
            return;
        var parentLtw = ltwLookup[parent.Value];
        var t = float4x4.Translate(transform.Position);
        var r = transform.WithPosition(0).ToMatrix();
        var s = float4x4.Scale(rotationScale.Value);
        ltw.Value = math.mul(parentLtw.Value, math.mul(t, math.mul(s, r)));
    }
}

[UpdateInGroup(typeof(TransformSystemGroup))]
[UpdateAfter(typeof(LocalToWorldSystem))]
public partial struct PostRotationScaleToLocalToWorldSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        new PostRotationScaleJob
        {
            ltwLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true)
        }.ScheduleParallel();
    }
}