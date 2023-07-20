using Unity.Entities;
using Unity.Transforms;

[WriteGroup(typeof(LocalToWorld))]
public struct AsteroidData : IComponentData
{
    public uint Seed;
}