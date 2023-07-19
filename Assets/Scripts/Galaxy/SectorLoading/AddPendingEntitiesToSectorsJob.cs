using Unity.Entities;

public partial struct AddPendingEntitiesToSectorsJob : IJobEntity
{
    public PendingSectors pendingSectors;
    
    void Execute(Entity e, in LocalToGalaxy ltg, EnabledRefRW<GenerateIntoSector> toGenerate)
    {
        pendingSectors.entitiesPerSector.Add(ltg, e);
        toGenerate.ValueRW = false;
    }
}