using Galaxy;
using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Random = Unity.Mathematics.Random;

public struct ForceLoadSector : IComponentData
{
    public LocalToGalaxy Sector;
}

public partial struct SectorLoadJob : IJob
{
    public LoadSectorSystem.PendingSectors pendingSectors;
    public LoadSectorSystem.SectorLoadSettings sectorLoadSettings;
    public EntityArchetype sectorArchetype;
    public EntityCommandBuffer ecb;
    public LocalToGalaxy playerLtg;
    
    public void Execute()
    {
        var sectorsLoadedThisFrame = 0;
        while (sectorsLoadedThisFrame++ < sectorLoadSettings.MaxSectorsLoadedPerFrame && pendingSectors.sectorsToLoad.TryDequeue(out var newSector))
        {
            newSector.LocalPosition = 0;
            var sectorDelta = newSector - playerLtg;
            var skipLoading = GalaxyTools.GetLightSectorType(newSector, out var _) == LightSectorType.Empty;

            for (int i = 0; i < pendingSectors.sectorsLoaded.Length && !skipLoading; i++)
            {
                skipLoading |= pendingSectors.sectorsLoaded[i] == newSector;
            }

            if (skipLoading)
            {
                sectorsLoadedThisFrame--;
                continue;
            }

            var sectorData = GalaxyTools.GetLightSectorData(newSector);
            LoadLightSector(newSector, sectorData);
        }
    }

    void LoadStarSector(in LocalToGalaxy ltg, StarSectorData data)
    {
        pendingSectors.sectorsLoaded.Add(ltg);
        var e = ecb.CreateEntity(sectorArchetype);
        ecb.SetComponent(e, ltg);
        ecb.AddComponent(e, data);
        ecb.AddComponent<GenerateStar>(e);
    }

    void LoadLightSector(in LocalToGalaxy ltg, LightSectorData data)
    {
        pendingSectors.sectorsLoaded.Add(ltg);
        var e = ecb.CreateEntity(sectorArchetype);
        ecb.SetComponent(e, ltg);
        ecb.AddComponent(e, data);

        for (byte i = 0; i < data.NumberOfStars; i++)
        {
            var ltg2 = ltg;
            ltg2.StarSector = data.GetStarSector(i);
            LoadStarSector(ltg2, GalaxyTools.GetStarSectorData(ltg2));
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
                var newLtg = sc.New + new LocalToGalaxy { PlanetSector = new int3(x, y, z) };
                sectorsToLoadLocal.Enqueue(newLtg);
            }
        }
        if (sc.ChangeType is SectorChangeType.Teleport or SectorChangeType.Spawn)
        {
            var maxDistance = sectorLoadSettings.MaxLoadedLightSectorDistance;
            for (int x = 0; x < maxDistance; x++)
            for (int y = 0; y < maxDistance; y++)
            for (int z = 0; z < maxDistance; z++)
            {
                if(x + y + z > maxDistance)
                    continue;
                
                TryEnqueueLightSector(new int3(x, y, z),    sc, sectorsToLoadLocal);
                TryEnqueueLightSector(new int3(-x, y, z),   sc, sectorsToLoadLocal);
                TryEnqueueLightSector(new int3(x, -y, z),   sc, sectorsToLoadLocal);
                TryEnqueueLightSector(new int3(-x, -y, z),  sc, sectorsToLoadLocal);
                TryEnqueueLightSector(new int3(x, y, -z),   sc, sectorsToLoadLocal);
                TryEnqueueLightSector(new int3(-x, y, -z),  sc, sectorsToLoadLocal);
                TryEnqueueLightSector(new int3(x, -y, -z),  sc, sectorsToLoadLocal);
                TryEnqueueLightSector(new int3(-x, -y, -z), sc, sectorsToLoadLocal);
            } 
        }
        else if(math.any(sc.New.StarSector != sc.Previous.StarSector))
        {
            var maxDistance = sectorLoadSettings.MaxLoadedStarSectorDistance;
            var diff = sc.New.StarSector - sc.Previous.StarSector;
            diff = math.max(math.min(diff, -2 * maxDistance), 2 * maxDistance);
            var diffsign = (int3)math.round(math.sign(diff));
            for (int x = diffsign.x * maxDistance; x != diffsign.x * (maxDistance - diff.x); x-= diffsign.x)
            for (int y = diffsign.y * maxDistance; y != diffsign.y * (maxDistance - diff.y); y-= diffsign.y)
            for (int z = diffsign.z * maxDistance; z != diffsign.z * (maxDistance - diff.z); z-= diffsign.z)
            {
                TryEnqueueStarSector(new int3(x, y, z), sc, sectorsToLoadLocal);
            }
        }
    }
    
    static void TryEnqueueLightSector(int3 offset, SectorChange sc,
                                      NativeQueue<LocalToGalaxy> sectorsToLoadLocal)
    {
        var pos = offset + sc.New.LightSector;
        var toLoad = sc.New;
        toLoad.LightSector = pos;
        if (GalaxyTools.GetLightSectorType(toLoad, out _) != LightSectorType.Empty)
            sectorsToLoadLocal.Enqueue(new LocalToGalaxy
            {
                LightSector = pos,
            });
    }
    
    static void TryEnqueueStarSector(int3 offset, SectorChange sc,
                                      NativeQueue<LocalToGalaxy> sectorsToLoadLocal)
    {
        var pos = offset + sc.New.StarSector;
        var toLoad = sc.New;
        toLoad.StarSector = pos;
        if (GalaxyTools.GetStarSectorType(toLoad) != StarSectorType.Empty)
            sectorsToLoadLocal.Enqueue(new LocalToGalaxy
            {
                LightSector = toLoad.LightSector,
                StarSector = toLoad.StarSector,
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
        public int MaxLoadedLightSectorDistance;
        public int MaxLoadedStarSectorDistance;
        public int MaxLoadedPlanetSectorDistance;
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
            typeof(LocalToGalaxy), 
            typeof(TravellerInSector),
            typeof(ToGenerate));

        state.EntityManager.CreateEntity(typeof(SectorLoadSettings));
        SystemAPI.SetSingleton(new SectorLoadSettings
        {
            MaxLoadedLightSectorDistance = 5,
            MaxLoadedStarSectorDistance = 2,
            MaxLoadedSectorDistanceDefault = 3,
            MaxSectorsLoadedPerFrame = 45
        });
        lastPlayerLtg = new LocalToGalaxy {StarSector = int.MaxValue, PlanetSector = int.MaxValue};
        
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
            pendingSectors = sectorsToLoad.ValueRW,
            sectorLoadSettings = sectorLoadSettings,
            ecb = ecb,
            playerLtg = playerLtg,
            sectorArchetype = sectorArchetype
        }.Schedule(state.Dependency);
    }
}
