using Unity.Collections;
using Unity.Entities;

public struct Name : IComponentData
{
    public FixedString64Bytes Value;
}