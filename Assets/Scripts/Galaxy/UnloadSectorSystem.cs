using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public struct UnloadCandidate : IComponentData
{
    public float Timeout;
}

[UpdateInGroup(typeof(LateSimulationSystemGroup), OrderLast = true)]
public partial class UnloadSectorSystem : SystemBase
{
    private const float KEEP_CANDIDATE_FOR_MAX_SECONDS = 1;
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
        var deltaTime = World.Time.DeltaTime;

        var localToGalaxyEntities = GetEntityQuery(typeof(LocalToGalaxy)).ToEntityArray(Allocator.Temp);
        var localToGalaxyBuffer = GetEntityQuery(typeof(LocalToGalaxy)).ToComponentDataArray<LocalToGalaxy>(Allocator.Temp);
        var playerLtg = SystemAPI.GetComponent<LocalToGalaxy>(SystemAPI.GetSingletonEntity<LocalPlayer>());
        var toRemoveList = new NativeList<Entity>(Allocator.Temp);

        var shouldRecalculateSectors = math.any(playerLtg.StarSector != lastPlayerLtg.StarSector);
        var shouldRecalculateSubSectors = shouldRecalculateSectors || math.any(playerLtg.PlanetSector != lastPlayerLtg.PlanetSector);
        lastPlayerLtg = playerLtg;
        var sectorLoadSettings = SystemAPI.GetSingleton<LoadSectorSystem.SectorLoadSettings>();
        var pendingSectors = SystemAPI.GetSingletonRW<LoadSectorSystem.PendingSectors>();
        if (shouldRecalculateSectors)
        {
            Entities
                .WithNone<UnloadCandidate>()
                .WithNone<ToGenerate>()
                .WithAll<SectorLoadingDistance.CelestialBody>()
                .ForEach((Entity e, in LocalToGalaxy ltg) =>
                {
                    var unloadDistance = SystemAPI.HasComponent<SectorLoadingDistance.Planetoid>(e)
                        ? sectorLoadSettings.MaxLoadedPlanetSectorDistance 
                        : sectorLoadSettings.MaxLoadedStarSectorDistance;
                    if (GalaxyTools.ShouldUnloadSector(ltg - playerLtg,
                            unloadDistance))
                        ecb.AddComponent<UnloadCandidate>(e);
                }).Schedule();

            Entities
                .WithAll<UnloadCandidate, SectorLoadingDistance.CelestialBody>()
                .ForEach((Entity e, in LocalToGalaxy ltg) =>
                {
                    var unloadDistance = SystemAPI.HasComponent<SectorLoadingDistance.Planetoid>(e)
                        ? sectorLoadSettings.MaxLoadedPlanetSectorDistance 
                        : sectorLoadSettings.MaxLoadedStarSectorDistance;
                    if(!GalaxyTools.ShouldUnloadSector(ltg - playerLtg, unloadDistance))
                        ecb.RemoveComponent<UnloadCandidate>(e);
                }).Schedule();
        }

        if (shouldRecalculateSubSectors)
        {
            // Entities
            //     .WithNone<UnloadCandidate, SectorLoadingDistance.Planetoid, SectorLoadingDistance.CelestialBody>()
            //     .WithNone<ToGenerate>()
            //     .ForEach((Entity e, in LocalToGalaxy ltg) =>
            //     {
            //         if (GalaxyTools.ShouldUnloadSector(ltg - playerLtg,
            //                 sectorLoadSettings.MaxLoadedSectorDistanceDefault))
            //             ecb.AddComponent<UnloadCandidate>(e);
            //     }).Schedule();
            //
            // Entities
            //     .WithAll<UnloadCandidate>()
            //     .WithNone<SectorLoadingDistance.Planetoid, SectorLoadingDistance.CelestialBody>()
            //     .ForEach((Entity e, in LocalToGalaxy ltg) =>
            //     {
            //         if(!GalaxyTools.ShouldUnloadSector(ltg - playerLtg, sectorLoadSettings.MaxLoadedSectorDistanceDefault))
            //             ecb.RemoveComponent<UnloadCandidate>(e);
            //     }).Schedule();
        }


        foreach(var (candidate, ltg, e) in SystemAPI.Query<RefRW<UnloadCandidate>, LocalToGalaxy>().WithEntityAccess())
        {
            candidate.ValueRW.Timeout += deltaTime;
            if (candidate.ValueRW.Timeout <= KEEP_CANDIDATE_FOR_MAX_SECONDS)
                continue;
            if (ltg == playerLtg)
            {
                ecb.RemoveComponent<UnloadCandidate>(e);
                return;
            }

            for(int i = 0; i < localToGalaxyEntities.Length; i++)
                if (localToGalaxyBuffer[i] == ltg)
                {
                    toRemoveList.Add(localToGalaxyEntities[i]);
                }
            
            for(int i = 0; i < pendingSectors.ValueRW.sectorsLoaded.Length; i++)
                if(pendingSectors.ValueRW.sectorsLoaded[i] == ltg)
                    pendingSectors.ValueRW.sectorsLoaded.RemoveAt(i--);
        }

        EntityManager.DestroyEntity(toRemoveList);
    }
}
