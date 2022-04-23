using Unity.Entities;

[GenerateAuthoringComponent]
public struct ElectricityNetworkConnection : IComponentData
{
    public Entity Network;
}