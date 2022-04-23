using Unity.Entities;

[GenerateAuthoringComponent]
public struct ElectricityConsumer : IComponentData
{
    public float Value;
    public bool DemandSatisfied;
    public Entity Network;
}