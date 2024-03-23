using System;
using Unity.Entities;
using Unity.Mathematics;

public struct MoveSectorCommand : AdminConsole.IConsoleCommand
{
    public EntityManager EntityManager;
    public string BaseCommand => "warp";
    public void Execute(string[] parameters, Entity executor)
    {
        if (parameters.Length != 3 && parameters.Length != 6)
            return;

        int ssx = 0;
        int ssy = 0;
        int ssz = 0;

        if (!int.TryParse(parameters[0], out var x) ||
            !int.TryParse(parameters[1], out var y) ||
            !int.TryParse(parameters[2], out var z))
            return;
        if(parameters.Length == 6)
            if (!int.TryParse(parameters[3], out ssx) ||
                !int.TryParse(parameters[4], out ssy) ||
                !int.TryParse(parameters[5], out ssz))
                return;

        var ltg2 = new LocalToGalaxy{StarSector = new int3(x, y, z), PlanetSector = new int3(ssx, ssy, ssz)};
        EntityManager.SetComponentData(executor, ltg2);
    }

    public string[] GetHints(int parameter)
    {
        return parameter < 6 ? new[]{"0"} : Array.Empty<string>();
    }
}