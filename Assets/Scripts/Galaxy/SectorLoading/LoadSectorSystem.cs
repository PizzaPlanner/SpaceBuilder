using Galaxy;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;

public partial struct LoadSectorSystem : ISystem
{
    EntityQuery sectorChangeQuery;
    EntityQuery starPrefabQuery;

    public struct SectorLoadSettings : IComponentData
    {
        public int MaxLoadedLightSectorDistance;
        public int MaxLoadedStarSectorDistance;
        public int MaxLoadedPlanetSectorDistance;
        public int MaxSectorsLoadedPerFrame;
    }

    public void OnCreate(ref SystemState state)
    {
        sectorChangeQuery = state.GetEntityQuery(typeof(SectorChange));
        starPrefabQuery = SystemAPI.QueryBuilder().WithOptions(EntityQueryOptions.IncludePrefab)
                                   .WithAll<Star, Prefab>().Build();

        state.EntityManager.CreateEntity(typeof(PendingSectors));
        var sectors = new PendingSectors();
        sectors.Allocate();
        SystemAPI.SetSingleton(sectors);
        

        state.EntityManager.CreateEntity(typeof(SectorLoadSettings));
        SystemAPI.SetSingleton(new SectorLoadSettings
        {
            MaxLoadedLightSectorDistance = 5,
            MaxLoadedStarSectorDistance = 2,
            MaxLoadedPlanetSectorDistance = 3,
            MaxSectorsLoadedPerFrame = 50
        });

        state.RequireForUpdate<PendingSectors>();
        state.RequireForUpdate<LocalPlayer>();
        state.RequireForUpdate<SectorLoadSettings>();
        state.RequireForUpdate(starPrefabQuery);
    }

    public void OnDestroy(ref SystemState state)
    {
        var pendingSectors = SystemAPI.GetSingleton<PendingSectors>();
        pendingSectors.Dispose();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var ecb = SystemAPI.GetSingleton<BeginInitializationEntityCommandBufferSystem.Singleton>()
                           .CreateCommandBuffer(state.WorldUnmanaged);
        var sectorLoadSettings = SystemAPI.GetSingleton<SectorLoadSettings>();
        var sectorsToLoad = SystemAPI.GetSingletonRW<PendingSectors>();
        var starPrefab = starPrefabQuery.GetSingletonEntity();
        
        ecb.DestroyEntity(sectorChangeQuery, EntityQueryCaptureMode.AtRecord);

        state.Dependency = new AddPendingEntitiesToSectorsJob
        {
            pendingSectors = sectorsToLoad.ValueRW
        }.Schedule(state.Dependency);
        
        state.Dependency = new ProcessSectorChangesJob
        {
            pendingSectors = sectorsToLoad.ValueRW,
            sectorLoadSettings = sectorLoadSettings,
        }.Schedule(state.Dependency);

        state.Dependency = new StarSectorLoadJob
        {
            pendingSectors = sectorsToLoad.ValueRW,
            prefab = starPrefab,
            ecb = ecb,
        }.Schedule(state.Dependency);
    }
}