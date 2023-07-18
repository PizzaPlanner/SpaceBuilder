using Unity.Entities;
using Unity.Mathematics;

public struct Docked : IComponentData
{
    public Entity Target;
    public quaternion TargetRotation;
    public quaternion Rotation;
    public float3 Position;
    public float3 TargetPosition;
}

public struct AlignUpVector : IComponentData
{
    public float Progress;
}