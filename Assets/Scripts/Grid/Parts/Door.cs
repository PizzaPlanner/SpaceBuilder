using Unity.Entities;

public struct Door : IComponentData
{
    public bool Open;
    public bool Locked;
}