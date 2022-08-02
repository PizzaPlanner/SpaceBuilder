using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public struct TravellerInSector : IBufferElementData
{
    public Entity Player;
}

[GenerateAuthoringComponent]
public struct LocalToGalaxy : IComponentData
{
    public int3 Sector;
    public int3 SubSector;
    public float3 LocalPosition;

    public static double3 RelativePosition(LocalToGalaxy origin, LocalToGalaxy target)
    {
        var sector = target.Sector - origin.Sector;
        var subsector = target.SubSector - origin.SubSector;
        var relativePosition = target.LocalPosition - origin.LocalPosition;
        return new double3(TRSToLocalToGalaxySystem.MAX_SUBSECTOR_IN_SECTOR) * 2 *
               TRSToLocalToGalaxySystem.MAX_POSITION_IN_SUBSECTOR * 2 * sector +
               new double3(TRSToLocalToGalaxySystem.MAX_POSITION_IN_SUBSECTOR) * 2 * subsector + relativePosition;
    }
}

public struct SectorChange : IComponentData
{
    public Entity Entity;
    public LocalToGalaxy Previous;
    public LocalToGalaxy New;
}

public struct CanTravelSectors : IComponentData { }

[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial class MakeTravellerSystem : SystemBase
{
    protected override void OnUpdate()
    {
        Entities
            .WithNone<CanTravelSectors>()
            .WithAny<Player, Flyable>()
            .WithAll<LocalToGalaxy>()
            .ForEach((Entity e) =>
            {
                EntityManager.AddComponent<CanTravelSectors>(e);
            }).WithoutBurst().WithStructuralChanges().Run();
    }
}

[UpdateInGroup(typeof(TransformSystemGroup))]
[UpdateBefore(typeof(TRSToLocalToWorldSystem))]
[UpdateBefore(typeof(TRSToLocalToParentSystem))]
public partial class TRSToLocalToGalaxySystem : SystemBase
{
    private EndSimulationEntityCommandBufferSystem bufferSystem;
    public const int MAX_POSITION_IN_SUBSECTOR = 0x0FFF;
    public const int MAX_SUBSECTOR_IN_SECTOR = 0x7FFF_FFFF;

    protected override void OnCreate()
    {
        bufferSystem = World.GetExistingSystem<EndSimulationEntityCommandBufferSystem>();
        RequireSingletonForUpdate<LocalPlayer>();
    }

    protected override void OnUpdate()
    {
        var ecb = bufferSystem.CreateCommandBuffer();

        Entities
            .WithAll<CanTravelSectors>()
            .WithNone<Parent>()
            .ForEach((Entity e, ref LocalToGalaxy ltg, ref Translation t) =>
            {
                var secToLoad = ltg;
                var relativePos = t.Value % MAX_POSITION_IN_SUBSECTOR;
                var ssDiff = (int3)(t.Value / MAX_POSITION_IN_SUBSECTOR);
                var edge = MAX_POSITION_IN_SUBSECTOR * -math.sign(ssDiff);

                var newPos = relativePos + edge;
                newPos = math.select(newPos, newPos + MAX_POSITION_IN_SUBSECTOR * 0.000001f, newPos == MAX_POSITION_IN_SUBSECTOR);
                ltg.LocalPosition = newPos;
                t.Value = newPos;

                if (math.all(ssDiff == int3.zero))
                    return;

                var sX = (long)ltg.SubSector.x + ssDiff.x;
                var sY = (long)ltg.SubSector.y + ssDiff.y;
                var sZ = (long)ltg.SubSector.z + ssDiff.z;

                ltg.SubSector += ssDiff;
                ltg.Sector += new int3(
                    math.abs(sX) > MAX_SUBSECTOR_IN_SECTOR ? (int)math.sign(sX) : 0,
                    math.abs(sY) > MAX_SUBSECTOR_IN_SECTOR ? (int)math.sign(sY) : 0,
                    math.abs(sZ) > MAX_SUBSECTOR_IN_SECTOR ? (int)math.sign(sZ) : 0);

                var update = ecb.CreateEntity();
                ecb.AddComponent(update, new SectorChange{Entity = e, Previous = ltg, New = secToLoad});
            }).Run();

        var playerPosition = GetComponent<LocalToGalaxy>(GetSingletonEntity<LocalPlayer>());
        Entities
            .WithNone<LocalPlayer>()
            .WithNone<Parent>()
            .ForEach((Entity e, ref Translation t, in LocalToGalaxy ltg) =>
            {
                t.Value = (float3)LocalToGalaxy.RelativePosition(playerPosition, ltg);
            }).ScheduleParallel();

        bufferSystem.AddJobHandleForProducer(Dependency);
    }
}

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

    struct HasPendingSectors : IComponentData
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
