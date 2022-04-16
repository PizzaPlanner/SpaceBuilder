using Unity.Entities;
using Unity.Mathematics;

[GenerateAuthoringComponent]
public struct Sun : IComponentData
{
    public float3 OrbitUp;
}