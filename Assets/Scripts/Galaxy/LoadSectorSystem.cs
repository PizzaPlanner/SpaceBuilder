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
    private EndSimulationEntityCommandBufferSystem bufferSystem;
    private NativeQueue<LocalToGalaxy> sectorsToLoad;
    private EntityArchetype sectorArchetype;
    private int3 lastPlayerSector;
    private SectorLoadSettings lastSectorLoadSettings;

    public struct HasPendingSectors : IComponentData
    {
        public int Count;
    }

    public struct SectorLoadSettings : IComponentData
    {
        public int MaxLoadedSectorDistance;
        public int MaxSectorsLoadedPerFrame;
    }

    protected override void OnCreate()
    {
        bufferSystem = World.GetExistingSystem<EndSimulationEntityCommandBufferSystem>();
        RequireForUpdate(GetEntityQuery(new EntityQueryDesc{Any = new[]
        {
            ComponentType.ReadOnly<SectorChange>(),
            ComponentType.ReadOnly<ForceLoadSector>(),
            ComponentType.ReadOnly<HasPendingSectors>()
        }}));
        sectorsToLoad = new NativeQueue<LocalToGalaxy>(Allocator.Persistent);
        sectorArchetype = EntityManager.CreateArchetype(typeof(SubSector), typeof(LocalToGalaxy), typeof(TravellerInSector),
            typeof(ToGenerate));
        lastPlayerSector = int.MaxValue;

        EntityManager.CreateEntity(typeof(SectorLoadSettings));
        SetSingleton(new SectorLoadSettings{MaxLoadedSectorDistance = 50, MaxSectorsLoadedPerFrame = 75});
    }

    protected override void OnDestroy()
    {
        sectorsToLoad.Dispose();
    }

    protected override void OnUpdate()
    {
        var buffer = bufferSystem.CreateCommandBuffer();
        var loadedSectorsQuery = GetEntityQuery(new EntityQueryDesc() {
            Options = EntityQueryOptions.IncludeDisabled,
            All = new ComponentType[]
            {
                typeof(SubSector), typeof(LocalToGalaxy)
            }});
        var sectorEntities = loadedSectorsQuery.ToEntityArray(Allocator.Temp);
        var sectors = loadedSectorsQuery.ToComponentDataArray<LocalToGalaxy>(Allocator.Temp);

        var unloadCandidates = GetEntityQuery(typeof(UnloadCandidate), typeof(LocalToGalaxy))
            .ToComponentDataArray<LocalToGalaxy>(Allocator.Temp);

        var localPlayer = GetSingletonEntity<LocalPlayer>();
        var localPlayerSector = GetComponent<LocalToGalaxy>(localPlayer).Sector;

        var sectorsToLoadLocal = sectorsToLoad;

        Entities
            .ForEach((Entity e, in SectorChange sc) =>
            {
                sectorsToLoadLocal.Enqueue(sc.New);
                buffer.DestroyEntity(e);
            }).Run();

        Entities.ForEach((Entity e, in ForceLoadSector fls) =>
        {
            sectorsToLoadLocal.Enqueue(fls.Sector);
            buffer.DestroyEntity(e);
        }).Run();

        var shouldRecalculateSectors = math.any(localPlayerSector != lastPlayerSector) ||
                                       lastSectorLoadSettings.MaxLoadedSectorDistance != GetSingleton<SectorLoadSettings>().MaxLoadedSectorDistance;

        lastSectorLoadSettings = GetSingleton<SectorLoadSettings>();
        int maxLoadedSectorDistance = lastSectorLoadSettings.MaxLoadedSectorDistance;
        int maxSectorsLoadedPerFrame = lastSectorLoadSettings.MaxSectorsLoadedPerFrame;

        if (shouldRecalculateSectors)
        {
            lastPlayerSector = localPlayerSector;
            Entities
                .WithNone<UnloadCandidate>()
                .ForEach((Entity e, in LocalToGalaxy ltg, in SectorData sector) =>
                {
                    if (math.csum(math.abs(ltg.Sector - localPlayerSector)) > maxLoadedSectorDistance)
                        buffer.AddComponent<UnloadCandidate>(e);
                }).Schedule();

            Entities
                .WithAll<UnloadCandidate>()
                .ForEach((Entity e, in LocalToGalaxy ltg) =>
                {
                    if (math.csum(math.abs(ltg.Sector - localPlayerSector)) < maxLoadedSectorDistance)
                        buffer.RemoveComponent<UnloadCandidate>(e);
                }).Schedule();

            Job
                .WithName("LoadNearbyStarSectors")
                .WithCode(() =>
                {
                    sectorsToLoadLocal.Enqueue(new LocalToGalaxy{Sector = localPlayerSector});

                    for (int x = -maxLoadedSectorDistance; x < maxLoadedSectorDistance; x++)
                    for (int y = -maxLoadedSectorDistance; y < maxLoadedSectorDistance; y++)
                    for (int z = -maxLoadedSectorDistance; z < maxLoadedSectorDistance; z++)
                    {
                        var pos = new int3(x, y, z) + localPlayerSector;
                        if (math.csum(math.abs(new int3(x, y, z))) > maxLoadedSectorDistance)
                            continue;
                        if(GalaxyTools.GetSectorType(pos) == SectorType.Star)
                            sectorsToLoadLocal.Enqueue(new LocalToGalaxy
                            {
                                Sector = pos,
                                SubSector = GalaxyTools.GetStarSectorData(pos).Center
                            });
                    }
                }).Run();
        }

        var added = new NativeList<LocalToGalaxy>(Allocator.Temp);
        var sectorArchetypeLocal = sectorArchetype;
        var hasPendingSectors = HasSingleton<HasPendingSectors>() ? GetSingletonEntity<HasPendingSectors>() : Entity.Null;

        // var camForward = (float3)Camera.main.transform.forward;
        // Job
        //     .WithName("SortQueue")
        //     .WithCode(() =>
        //     {
        //         var array = sectorsToLoadLocal.ToArray(Allocator.Temp);
        //         array.Sort(new LocalToGalaxyComparer{forward = camForward});
        //         sectorsToLoadLocal.Clear();
        //         foreach(var item in array)
        //             sectorsToLoadLocal.Enqueue(item);
        //     }).Run();

        Job
            .WithName("LoadSectors")
            .WithCode(() =>
            {
                var sectorsLoadedThisFrame = 0;
                while (sectorsLoadedThisFrame++ < maxSectorsLoadedPerFrame && sectorsToLoadLocal.TryDequeue(out var newSector))
                {
                    newSector.LocalPosition = 0;
                    var skipLoading = math.csum(math.abs(newSector.Sector - localPlayerSector)) > maxLoadedSectorDistance;

                    foreach (var sector in added)
                    {
                        if (sector == newSector || skipLoading)
                        {
                            skipLoading = true;
                            break;
                        }
                    }

                    for (int i = 0; i < sectors.Length && !skipLoading; i++)
                    {
                        if (sectors[i] == newSector)
                        {
                            skipLoading = true;
                            break;
                        }
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

        bufferSystem.AddJobHandleForProducer(Dependency);
    }
}
