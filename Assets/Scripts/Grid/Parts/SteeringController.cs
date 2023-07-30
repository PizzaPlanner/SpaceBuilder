using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public struct SteeringController : IComponentData
{
    public float3 SteeringAngle;
    public float3 Thrust;
}