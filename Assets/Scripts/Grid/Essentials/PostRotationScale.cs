using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[WriteGroup(typeof(LocalToWorld))]
public struct PostRotationScale : IComponentData
{
    public float3 Value;

    public static PostRotationScale Identity => new PostRotationScale { Value = 1, };
}