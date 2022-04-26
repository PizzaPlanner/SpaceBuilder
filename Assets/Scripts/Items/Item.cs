using Unity.Entities;

public struct Item : IComponentData
{
    public Entity WorldRepresentation;
}

public struct Stackable : IComponentData
{
    public ushort Size;
    public ushort MaxSize;
}
