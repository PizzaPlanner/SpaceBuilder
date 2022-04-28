using Unity.Entities;

[GenerateAuthoringComponent]
public struct Door : IComponentData
{
    public bool Open;
    public bool Locked;
}