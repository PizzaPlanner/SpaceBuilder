using Unity.Entities;

public struct GenerateIntoSector : IComponentData, IEnableableComponent
{
    public LocalToGalaxy LinkedSector;
}