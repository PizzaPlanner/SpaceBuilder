using Unity.Entities;

public readonly partial struct RegisterForUnloadAspect : IAspect
{
    readonly EnabledRefRW<GenerateIntoSector> toGenerateFlag;
    readonly RefRW<GenerateIntoSector> targetSector;
    readonly Entity e;

    public void RegisterForUnload(PendingSectors target)
    {
        target.entitiesPerSector.Add(targetSector.ValueRO.LinkedSector, e);
        toGenerateFlag.ValueRW = false;
    }
}

public partial struct AddPendingEntitiesToSectorsJob : IJobEntity
{
    public PendingSectors pendingSectors;
    
    void Execute(RegisterForUnloadAspect aspect)
    {
        aspect.RegisterForUnload(pendingSectors);
    }
}