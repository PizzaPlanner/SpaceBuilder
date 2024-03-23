using System;
using Unity.Entities;
using Unity.Physics;

public struct NoclipConsoleCommand : AdminConsole.IConsoleCommand
{
    public EntityManager EntityManager;
    public string BaseCommand => "noclip";
    public void Execute(string[] parameters, Entity executor)
    {
        bool on = !(parameters.Length == 1 && ushort.TryParse(parameters[0], out var i) && i == 0);
        EntityManager.SetSharedComponentManaged(executor, new PhysicsWorldIndex{Value = on ? 0u : 1u});
    }

    public string[] GetHints(int parameter)
    {
        return parameter == 0 ? new[]{"0", "1"} : Array.Empty<string>();
    }
}