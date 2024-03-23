using System;
using Unity.Entities;
using Unity.Transforms;

public struct SpawnCommand : AdminConsole.IConsoleCommand
{
    public EntityManager EntityManager;
    public string BaseCommand => "spawn";
    public void Execute(string[] parameters, Entity executor)
    {
        if (parameters.Length == 0 || executor == Entity.Null || !EntityManager.HasComponent<LocalToGalaxy>(executor))
            return;

        var executorTransform = EntityManager.GetComponentData<LocalTransform>(executor);
        
        var path = parameters[0];
        BlueprintGeneratorSystem.QueueBlueprintSpawn(path, EntityManager.GetComponentData<LocalToGalaxy>(executor), executorTransform.Rotation);
    }

    public string[] GetHints(int parameter)
    {
        return parameter == 0 ? BlueprintGeneratorSystem.GetAllSpawnables : Array.Empty<string>();
    }
}