using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

public struct ForceLoadSector : IComponentData
{
    public LocalToGalaxy Sector;
}

public struct UnloadCandidate : IComponentData
{

}

public partial class LoadSectorSystem : SystemBase
{
    private EndSimulationEntityCommandBufferSystem bufferSystem;
    private NativeQueue<LocalToGalaxy> sectorsToLoad;
    private EntityArchetype sectorArchetype;
    private int3 lastPlayerSector;
    private const int MAX_KEEP_SECTOR_DISTANCE = 35;
    private const int MAX_SECTORS_PER_FRAME = 20;

    public struct HasPendingSectors : IComponentData
    {
        public int Count;
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

        var localPlayer = GetSingletonEntity<LocalPlayer>();
        var localPlayerSector = GetComponent<LocalToGalaxy>(localPlayer).Sector;
        var newPlayerSector = lastPlayerSector;

        var sectorsToLoadLocal = sectorsToLoad;

        Entities
            .ForEach((Entity e, in SectorChange sc) =>
            {
                Entity oldSector = Entity.Null;
                Entity newSector = Entity.Null;
                for (int i = 0; i < sectors.Length; i++)
                {
                    if (math.all(sectors[i].Sector == sc.New.Sector) && math.all(sectors[i].SubSector == sc.New.SubSector))
                        newSector = sectorEntities[i];
                    if (math.all(sectors[i].Sector == sc.Previous.Sector) && math.all(sectors[i].SubSector == sc.Previous.SubSector))
                        oldSector = sectorEntities[i];
                }

                if (newSector == Entity.Null)
                {
                    sectorsToLoadLocal.Enqueue(sc.New);
                    return;
                }

                if (sc.Entity == localPlayer)
                {
                    if (oldSector != Entity.Null && !HasComponent<Disabled>(oldSector))
                    {
                        var oldPlayerBuffer = GetBuffer<TravellerInSector>(oldSector);
                        for (int i = 0; i < oldPlayerBuffer.Length; i++)
                            if (oldPlayerBuffer[i].Player == sc.Entity)
                            {
                                oldPlayerBuffer.RemoveAt(i);
                                i--;
                            }

                        if (oldPlayerBuffer.Length == 0)
                            buffer.AddComponent<Disabled>(oldSector);
                    }

                    var newPlayerBuffer = GetBuffer<TravellerInSector>(newSector);
                    if (newPlayerBuffer.Length == 0 && HasComponent<Disabled>(newSector))
                    {
                        buffer.RemoveComponent<Disabled>(newSector);
                    }
                    newPlayerBuffer.Add(new TravellerInSector {Player = sc.Entity});
                    newPlayerSector = sc.New.Sector;
                }
                buffer.DestroyEntity(e);
            }).Run();

        Entities.ForEach((Entity e, in ForceLoadSector fls) =>
        {
            sectorsToLoadLocal.Enqueue(fls.Sector);
            buffer.DestroyEntity(e);
        }).Run();

        if (math.any(newPlayerSector != lastPlayerSector))
        {
            lastPlayerSector = newPlayerSector;
            Entities
                .WithNone<UnloadCandidate>()
                .ForEach((Entity e, in LocalToGalaxy ltg, in SectorData sector) =>
                {
                    if (math.csum(math.abs(ltg.Sector - newPlayerSector)) > MAX_KEEP_SECTOR_DISTANCE)
                        buffer.AddComponent<UnloadCandidate>(e);
                }).Run();

            Job
                .WithName("LoadNearbyStarSectors")
                .WithCode(() =>
                {
                    sectorsToLoadLocal.Enqueue(new LocalToGalaxy{Sector = localPlayerSector});

                    for (int x = -MAX_KEEP_SECTOR_DISTANCE; x < MAX_KEEP_SECTOR_DISTANCE; x++)
                    for (int y = -MAX_KEEP_SECTOR_DISTANCE; y < MAX_KEEP_SECTOR_DISTANCE; y++)
                    for (int z = -MAX_KEEP_SECTOR_DISTANCE; z < MAX_KEEP_SECTOR_DISTANCE; z++)
                    {
                        var pos = new int3(x, y, z) + newPlayerSector;
                        if (math.csum(math.abs(new int3(x, y, z))) > MAX_KEEP_SECTOR_DISTANCE)
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
        Job
            .WithName("LoadSectors")
            .WithCode(() =>
            {
                var sectorsLoadedThisFrame = 0;
                while (sectorsLoadedThisFrame++ < MAX_SECTORS_PER_FRAME && sectorsToLoadLocal.TryDequeue(out var newSector))
                {
                    newSector.LocalPosition = 0;
                    var hasSector = false;

                    foreach (var sector in added)
                    {
                        if (math.all(sector.SubSector == newSector.SubSector) &&
                            math.all(sector.Sector == newSector.Sector))
                        {
                            hasSector = true;
                            break;
                        }
                    }

                    for (int i = 0; i < sectors.Length && !hasSector; i++)
                    {
                        if (math.all(sectors[i].SubSector == newSector.SubSector) &&
                            math.all(sectors[i].Sector == newSector.Sector))
                        {
                            hasSector = true;
                            break;
                        }
                    }

                    if (hasSector)
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


        bufferSystem.AddJobHandleForProducer(Dependency);
    }
}
