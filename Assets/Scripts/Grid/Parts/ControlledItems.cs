using Unity.Entities;

public struct ControlledItems : IBufferElementData
{
    public Entity Target;
    public ToggleType Type;
}