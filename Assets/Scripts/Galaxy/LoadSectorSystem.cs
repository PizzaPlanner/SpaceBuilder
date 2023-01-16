using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public struct ForceLoadSector : IComponentData
{
    public LocalToGalaxy Sector;
}

public partial class LoadSectorSystem : SystemBase
{
    private NativeQueue<LocalToGalaxy> sectorsToLoad;
    private EntityArchetype sectorArchetype;
    private LocalToGalaxy lastPlayerLtg;

    public struct HasPendingSectors : IComponentData
    {
        public int Count;
    }

    public struct SectorLoadSettings : IComponentData
    {
        public int MaxLoadedSectorDistanceCelestial;
        public int MaxLoadedSectorDistancePlanetoid;
        public int MaxLoadedSectorDistanceDefault;
        public int MaxSectorsLoadedPerFrame;
    }

    protected override void OnCreate()
    {
        RequireForUpdate(GetEntityQuery(new EntityQueryDesc{Any = new[]
        {
            ComponentType.ReadOnly<SectorChange>(),
            ComponentType.ReadOnly<ForceLoadSector>(),
            ComponentType.ReadOnly<HasPendingSectors>()
        }}));
        sectorsToLoad = new NativeQueue<LocalToGalaxy>(Allocator.Persistent);
        sectorArchetype = EntityManager.CreateArchetype(typeof(SubSector), typeof(LocalToGalaxy), typeof(TravellerInSector),
            typeof(ToGenerate));

        EntityManager.CreateEntity(typeof(SectorLoadSettings));
        SetSingleton(new SectorLoadSettings
        {
            MaxLoadedSectorDistanceCelestial = 40 * (int)Constants.MAX_SUBSECTOR,
            MaxLoadedSectorDistancePlanetoid = 3 * (int)Constants.MAX_SUBSECTOR,
            MaxLoadedSectorDistanceDefault = 3,
            MaxSectorsLoadedPerFrame = 45
        });
        lastPlayerLtg = new LocalToGalaxy {Sector = int.MaxValue, SubSector = int.MaxValue};
    }

    protected override void OnDestroy()
    {
        sectorsToLoad.Dispose();
    }

    protected override void OnUpdate()
    {
        var buffer = GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
        var loadedSectorsQuery = GetEntityQuery(new EntityQueryDesc() {
            Options = EntityQueryOptions.IncludeDisabledEntities,
            All = new ComponentType[]
            {
                typeof(SubSector), typeof(LocalToGalaxy)
            }});
        var sectors = loadedSectorsQuery.ToComponentDataArray<LocalToGalaxy>(Allocator.Temp);
        var sectorsToLoadLocal = sectorsToLoad;
        var sectorLoadSettings = GetSingleton<SectorLoadSettings>();

        Entities
            .WithName("ProcessSectorChanges")
            .ForEach((Entity e, in SectorChange sc) =>
            {
                buffer.DestroyEntity(e);

                if (sc.ChangeType != SectorChangeType.FTL)
                {
                    for(int x = -2; x <= 2; x++)
                    for(int y = -2; y <= 2; y++)
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
                        TryEnqueueCelestialSector(new int3(x, y, z),    sc, maxDistance, sectorsToLoadLocal);
                        TryEnqueueCelestialSector(new int3(-x, y, z),   sc, maxDistance, sectorsToLoadLocal);
                        TryEnqueueCelestialSector(new int3(x, -y, z),   sc, maxDistance, sectorsToLoadLocal);
                        TryEnqueueCelestialSector(new int3(-x, -y, z),  sc, maxDistance, sectorsToLoadLocal);
                        TryEnqueueCelestialSector(new int3(x, y, -z),   sc, maxDistance, sectorsToLoadLocal);
                        TryEnqueueCelestialSector(new int3(-x, y, -z),  sc, maxDistance, sectorsToLoadLocal);
                        TryEnqueueCelestialSector(new int3(x, -y, -z),  sc, maxDistance, sectorsToLoadLocal);
                        TryEnqueueCelestialSector(new int3(-x, -y, -z), sc, maxDistance, sectorsToLoadLocal);
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
                        TryEnqueueCelestialSector(new int3(x, y, z), sc, maxDistance, sectorsToLoadLocal);
                    }
                }
            }).Run();

        var playerLtg = GetComponent<LocalToGalaxy>(GetSingletonEntity<LocalPlayer>());
        var shouldRecalculateSectors = playerLtg != lastPlayerLtg;
        lastPlayerLtg = playerLtg;
        if (shouldRecalculateSectors)
        {
            var css = GetSingleton<Galaxy.CalculateClosestStarSystem.ClosestStarSector>();
            var starsec = GalaxyTools.GetStarSectorData(css.StarLTG.Sector);
            for (byte i = 0; i < starsec.NumberOfPlanets; i++)
            {
                var planetsec = css.StarLTG;
                planetsec.SubSector = GalaxyTools.GetPlanetData(starsec, i).SubSector;
                if(GalaxyTools.DistanceInSubsectors(playerLtg, planetsec) < sectorLoadSettings.MaxLoadedSectorDistancePlanetoid)
                    sectorsToLoadLocal.Enqueue(planetsec);
            }
        }

        Entities.ForEach((Entity e, in ForceLoadSector fls) =>
        {
            sectorsToLoadLocal.Enqueue(fls.Sector);
            buffer.DestroyEntity(e);
        }).Run();

        var added = new NativeList<LocalToGalaxy>(Allocator.Temp);
        var sectorArchetypeLocal = sectorArchetype;
        var hasPendingSectors = HasSingleton<HasPendingSectors>() ? GetSingletonEntity<HasPendingSectors>() : Entity.Null;

        Job
            .WithName("LoadSectors")
            .WithCode(() =>
            {
                var sectorsLoadedThisFrame = 0;
                while (sectorsLoadedThisFrame++ < sectorLoadSettings.MaxSectorsLoadedPerFrame && sectorsToLoadLocal.TryDequeue(out var newSector))
                {
                    newSector.LocalPosition = 0;
                    var sectorType = GalaxyTools.GetSubSectorData(newSector).Type;
                    var skipLoading = sectorType == SubSectorType.Empty || GalaxyTools.ShouldUnloadSector(playerLtg, newSector,
                        sectorType.IsCelestialScale() ? sectorLoadSettings.MaxLoadedSectorDistanceCelestial :
                        sectorType == SubSectorType.Planet ? sectorLoadSettings.MaxLoadedSectorDistancePlanetoid :
                        sectorLoadSettings.MaxLoadedSectorDistanceDefault
                    );

                    foreach (var sector in added)
                    {
                        skipLoading |= sector == newSector;
                    }

                    for (int i = 0; i < sectors.Length && !skipLoading; i++)
                    {
                        skipLoading |= sectors[i] == newSector;
                    }

                    if (skipLoading)
                    {
                        sectorsLoadedThisFrame--;
                        continue;
                    }

                    added.Add(newSector);
                    var e = buffer.CreateEntity(sectorArchetypeLocal);
                    buffer.SetComponent(e, newSector);
                }

                if(sectorsToLoadLocal.IsEmpty() && hasPendingSectors != Entity.Null)
                    buffer.DestroyEntity(hasPendingSectors);
                else if (!sectorsToLoadLocal.IsEmpty())
                {
                    if (hasPendingSectors == Entity.Null)
                    {
                        hasPendingSectors = buffer.CreateEntity();
                        buffer.AddComponent(hasPendingSectors, new HasPendingSectors{Count = sectorsToLoadLocal.Count});
                    }
                    else
                        buffer.SetComponent(hasPendingSectors, new HasPendingSectors{Count = sectorsToLoadLocal.Count});
                }
            }).Run();

        sectorsToLoad = sectorsToLoadLocal;
    }

    static void TryEnqueueCelestialSector(int3 offset, SectorChange sc, int maxDistance,
                                          NativeQueue<LocalToGalaxy> sectorsToLoadLocal)
    {
        var pos = offset + sc.New.Sector;
        if (GalaxyTools.GetSectorType(pos).IsCelestialScale() &&
            math.csum(math.abs(offset)) < maxDistance)
            sectorsToLoadLocal.Enqueue(new LocalToGalaxy
            {
                Sector = pos,
                SubSector = GalaxyTools.GetCelestialSectorCenter(pos, out _)
            });
    }
}
