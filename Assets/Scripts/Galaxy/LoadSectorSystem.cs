using Galaxy;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public struct ForceLoadSector : IComponentData
{
    public LocalToGalaxy Sector;
}

public partial struct SectorLoadJob : IJob
{
    public LoadSectorSystem.PendingSectors sectorsToLoad;
    public LoadSectorSystem.SectorLoadSettings sectorLoadSettings;
    public EntityArchetype sectorArchetype;
    public EntityCommandBuffer ecb;
    public LocalToGalaxy playerLtg;
    
    public void Execute()
    {
        var sectorsLoadedThisFrame = 0;
        while (sectorsLoadedThisFrame++ < sectorLoadSettings.MaxSectorsLoadedPerFrame && sectorsToLoad.sectorsToLoad.TryDequeue(out var newSector))
        {
            newSector.LocalPosition = 0;
            var sectorType = GalaxyTools.GetSubSectorData(newSector).Type;
            var skipLoading = sectorType == SubSectorType.Empty || GalaxyTools.ShouldUnloadSector(playerLtg, newSector,
                sectorType.IsCelestialScale() ? sectorLoadSettings.MaxLoadedSectorDistanceCelestial :
                sectorType == SubSectorType.Planet ? sectorLoadSettings.MaxLoadedSectorDistancePlanetoid :
                sectorLoadSettings.MaxLoadedSectorDistanceDefault
            );

            for (int i = 0; i < sectorsToLoad.sectorsLoaded.Length && !skipLoading; i++)
            {
                skipLoading |= sectorsToLoad.sectorsLoaded[i] == newSector;
            }

            if (skipLoading)
            {
                sectorsLoadedThisFrame--;
                continue;
            }

            sectorsToLoad.sectorsLoaded.Add(newSector);
            var e = ecb.CreateEntity(sectorArchetype);
            ecb.SetComponent(e, newSector);
        }
    }
}

public partial struct ProcessSectorChangesJob : IJobEntity
{
    public LoadSectorSystem.PendingSectors sectorsToLoad;
    public LoadSectorSystem.SectorLoadSettings sectorLoadSettings;
    void Execute(Entity e, in SectorChange sc)
    {
        var sectorsToLoadLocal = sectorsToLoad.sectorsToLoad;
        if (sc.ChangeType != SectorChangeType.FTL)
        {
            for (int x = -2; x <= 2; x++)
            for (int y = -2; y <= 2; y++)
            for (int z = -2; z <= 2; z++)
            {
                var newLtg = sc.New + new LocalToGalaxy {SubSector = new int3(x, y, z)};
                sectorsToLoadLocal.Enqueue(newLtg);
            }
        }
        if (sc.ChangeType is SectorChangeType.Teleport or SectorChangeType.Spawn)
        {
            var maxDistance = sectorLoadSettings.MaxLoadedSectorDistanceCelestial / (int)Constants.MAX_SUBSECTOR;
            for (int x = 0; x < maxDistance; x++)
            for (int y = 0; y < maxDistance; y++)
            for (int z = 0; z < maxDistance; z++)
            {
                if(x + y + z > maxDistance)
                    continue;
                
                TryEnqueueCelestialSector(new int3(x, y, z),    sc, sectorsToLoadLocal);
                TryEnqueueCelestialSector(new int3(-x, y, z),   sc, sectorsToLoadLocal);
                TryEnqueueCelestialSector(new int3(x, -y, z),   sc, sectorsToLoadLocal);
                TryEnqueueCelestialSector(new int3(-x, -y, z),  sc, sectorsToLoadLocal);
                TryEnqueueCelestialSector(new int3(x, y, -z),   sc, sectorsToLoadLocal);
                TryEnqueueCelestialSector(new int3(-x, y, -z),  sc, sectorsToLoadLocal);
                TryEnqueueCelestialSector(new int3(x, -y, -z),  sc, sectorsToLoadLocal);
                TryEnqueueCelestialSector(new int3(-x, -y, -z), sc, sectorsToLoadLocal);
            } 
        }
        else if(math.any(sc.New.Sector != sc.Previous.Sector))
        {
            var maxDistance = sectorLoadSettings.MaxLoadedSectorDistanceCelestial / (int)Constants.MAX_SUBSECTOR;
            var diff = sc.New.Sector - sc.Previous.Sector;
            diff = math.max(math.min(diff, -2 * maxDistance), 2 * maxDistance);
            var diffsign = (int3)math.round(math.sign(diff));
            for (int x = diffsign.x * maxDistance; x != diffsign.x * (maxDistance - diff.x); x-= diffsign.x)
            for (int y = diffsign.y * maxDistance; y != diffsign.y * (maxDistance - diff.y); y-= diffsign.y)
            for (int z = diffsign.z * maxDistance; z != diffsign.z * (maxDistance - diff.z); z-= diffsign.z)
            {
                TryEnqueueCelestialSector(new int3(x, y, z), sc, sectorsToLoadLocal);
            }
        }
    }
    
    static void TryEnqueueCelestialSector(int3 offset, SectorChange sc,
                                          NativeQueue<LocalToGalaxy> sectorsToLoadLocal)
    {
        var pos = offset + sc.New.Sector;
        if (GalaxyTools.GetSectorType(pos).IsCelestialScale())
            sectorsToLoadLocal.Enqueue(new LocalToGalaxy
            {
                Sector = pos,
                SubSector = GalaxyTools.GetCelestialSectorCenter(pos, out _)
            });
    }
}

public partial struct LoadSectorSystem : ISystem
{
    private EntityArchetype sectorArchetype;
    private LocalToGalaxy lastPlayerLtg;
    EntityQuery sectorChangeQuery;

    public struct PendingSectors : IComponentData
    {
        public NativeQueue<LocalToGalaxy> sectorsToLoad;
        public NativeQueue<LocalToGalaxy> sectorsToUnload;
        public NativeList<LocalToGalaxy> sectorsLoaded;
    }

    public struct SectorLoadSettings : IComponentData
    {
        public int MaxLoadedSectorDistanceCelestial;
        public int MaxLoadedSectorDistancePlanetoid;
        public int MaxLoadedSectorDistanceDefault;
        public int MaxSectorsLoadedPerFrame;
    }

    public void OnCreate(ref SystemState state)
    {
        sectorChangeQuery = state.GetEntityQuery(new EntityQueryDesc
        {
            Any = new[]
            {
                ComponentType.ReadOnly<SectorChange>(),
                ComponentType.ReadOnly<ForceLoadSector>(),
            }
        });

        state.EntityManager.CreateEntity(typeof(PendingSectors));
        SystemAPI.SetSingleton(new PendingSectors
        {
            sectorsToLoad = new NativeQueue<LocalToGalaxy>(Allocator.Persistent),
            sectorsToUnload = new NativeQueue<LocalToGalaxy>(Allocator.Persistent), 
            sectorsLoaded = new NativeList<LocalToGalaxy>(Allocator.Persistent) 
        });
        
        sectorArchetype = state.EntityManager.CreateArchetype(
            typeof(SubSector), 
            typeof(LocalToGalaxy), 
            typeof(TravellerInSector),
            typeof(ToGenerate));

        state.EntityManager.CreateEntity(typeof(SectorLoadSettings));
        SystemAPI.SetSingleton(new SectorLoadSettings
        {
            MaxLoadedSectorDistanceCelestial = 40 * (int)Constants.MAX_SUBSECTOR,
            MaxLoadedSectorDistancePlanetoid = 3 * (int)Constants.MAX_SUBSECTOR,
            MaxLoadedSectorDistanceDefault = 3,
            MaxSectorsLoadedPerFrame = 45
        });
        lastPlayerLtg = new LocalToGalaxy {Sector = int.MaxValue, SubSector = int.MaxValue};
        
        state.RequireForUpdate<PendingSectors>();
        state.RequireForUpdate<LocalPlayer>();
        state.RequireForUpdate<CalculateClosestStarSystem.ClosestStarSector>();
        state.RequireForUpdate<LocalPlayer>();
        state.RequireForUpdate<SectorLoadSettings>();
    }

    public void OnDestroy(ref SystemState state)
    {
        var pendingSectors = SystemAPI.GetSingleton<PendingSectors>();
        pendingSectors.sectorsToLoad.Dispose();
        pendingSectors.sectorsLoaded.Dispose();
        pendingSectors.sectorsToUnload.Dispose();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                           .CreateCommandBuffer(state.WorldUnmanaged);
        var sectorLoadSettings = SystemAPI.GetSingleton<SectorLoadSettings>();
        var sectorsToLoad = SystemAPI.GetSingletonRW<PendingSectors>();

        var playerLtg = SystemAPI.GetComponent<LocalToGalaxy>(SystemAPI.GetSingletonEntity<LocalPlayer>());
        var shouldRecalculateSectors = playerLtg != lastPlayerLtg;
        lastPlayerLtg = playerLtg;
        if (shouldRecalculateSectors)
        {
            var css = SystemAPI.GetSingleton<Galaxy.CalculateClosestStarSystem.ClosestStarSector>();
            var starsec = GalaxyTools.GetStarSectorData(css.StarLTG.Sector);
            for (byte i = 0; i < starsec.NumberOfPlanets; i++)
            {
                var planetsec = css.StarLTG;
                planetsec.SubSector = GalaxyTools.GetPlanetData(starsec, i).SubSector;
                if(GalaxyTools.DistanceInSubsectors(playerLtg, planetsec) < sectorLoadSettings.MaxLoadedSectorDistancePlanetoid)
                    sectorsToLoad.ValueRW.sectorsToLoad.Enqueue(planetsec);
            }
        }
        
        foreach(var fls in SystemAPI.Query<ForceLoadSector>())
        {
            sectorsToLoad.ValueRW.sectorsToLoad.Enqueue(fls.Sector);
        }
        ecb.DestroyEntity(sectorChangeQuery, EntityQueryCaptureMode.AtRecord);
        
        state.Dependency = new ProcessSectorChangesJob
        {
            sectorsToLoad = sectorsToLoad.ValueRW,
            sectorLoadSettings = sectorLoadSettings,
        }.Schedule(state.Dependency);

        state.Dependency = new SectorLoadJob
        {
            sectorsToLoad = sectorsToLoad.ValueRW,
            sectorLoadSettings = sectorLoadSettings,
            ecb = ecb,
            playerLtg = playerLtg,
            sectorArchetype = sectorArchetype
        }.Schedule(state.Dependency);
    }
}
