using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

public partial struct UnloadSectorsJob : IJob
{
    public PendingSectors pendingSectors;
    public EntityCommandBuffer ecb;
    
    public void Execute()
    {
        Unload(pendingSectors.PlanetSectors);
        Unload(pendingSectors.StarSectors);
        Unload(pendingSectors.LightSectors);
    }

    void Unload(SectorLoadState state)
    {
        while (state.ToUnload.TryDequeue(out var ltg))
        {
            if(!state.Contains(ltg))
                continue;

            var values = pendingSectors.entitiesPerSector.GetValuesForKey(ltg);
            foreach (var value in values)
                ecb.DestroyEntity(value);

            pendingSectors.entitiesPerSector.Remove(ltg);
            state.Unload(ltg);
        }
    }
}

public partial struct CheckUnloadSectorsJob : IJob
{
    public PendingSectors pendingSectors;
    public LoadSectorSystem.SectorLoadSettings sectorLoadSettings;
    public LocalToGalaxy playerLtg;
    
    public void Execute()
    {
        foreach (var ltg in pendingSectors.LightSectors.Loaded)
        {
            var diff = ltg - playerLtg;
            var delta = math.length(diff.Distance);
            var ltgIsInStarRange =
                delta <= sectorLoadSettings.MaxLoadedLightSectorDistance * LocalToGalaxy.DISTANCE_PER_LIGHTSECTOR;
            if(!ltgIsInStarRange)
                pendingSectors.LightSectors.Dequeue(ltg);
        }
        
        foreach (var ltg in pendingSectors.StarSectors.Loaded)
        {
            var diff = ltg - playerLtg;
            var delta = math.length(diff.Distance);
            var ltgIsInStarRange =
                delta <= sectorLoadSettings.MaxLoadedStarSectorDistance * LocalToGalaxy.DISTANCE_PER_STARSECTOR;
            if(!ltgIsInStarRange)
                pendingSectors.StarSectors.Dequeue(ltg);
        }
        
        foreach (var ltg in pendingSectors.PlanetSectors.Loaded)
        {
            var diff = ltg - playerLtg;
            var delta = math.length(diff.Distance);
            var ltgIsInStarRange =
                delta <= sectorLoadSettings.MaxLoadedPlanetSectorDistance * LocalToGalaxy.DISTANCE_PER_PLANETSECTOR;
            if(!ltgIsInStarRange)
                pendingSectors.PlanetSectors.Dequeue(ltg);
        }
    }
}

[UpdateAfter(typeof(LoadSectorSystem))]
public partial class UnloadSectorSystem : SystemBase
{
    private LocalToGalaxy lastPlayerLtg;

    protected override void OnCreate()
    {
        RequireForUpdate<LocalPlayer>();
        lastPlayerLtg = new LocalToGalaxy {StarSector = int.MaxValue, PlanetSector = int.MaxValue};
    }

    protected override void OnUpdate()
    {
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(World.Unmanaged);

        var pendingSectors = SystemAPI.GetSingletonRW<PendingSectors>();
        
        Dependency = new UnloadSectorsJob
        {
            pendingSectors = pendingSectors.ValueRW,
            ecb = ecb
        }.Schedule(Dependency);
        
        var playerLtg = SystemAPI.GetComponent<LocalToGalaxy>(SystemAPI.GetSingletonEntity<LocalPlayer>());
        var shouldRecalculateSectors = playerLtg != lastPlayerLtg;
        lastPlayerLtg = playerLtg;

        if (!shouldRecalculateSectors)
            return;
        
        var sectorLoadSettings = SystemAPI.GetSingleton<LoadSectorSystem.SectorLoadSettings>();
        Dependency = new CheckUnloadSectorsJob
        {
            pendingSectors = pendingSectors.ValueRW,
            playerLtg = playerLtg,
            sectorLoadSettings = sectorLoadSettings
        }.Schedule(Dependency);
    }
}
