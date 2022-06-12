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

    public double3 AbsolutePosition =>
        new double3(TRSToLocalToGalaxySystem.MAX_SUBSECTOR_IN_SECTOR) * 2 * TRSToLocalToGalaxySystem.MAX_POSITION_IN_SECTOR * 2 * Sector +
        new double3(TRSToLocalToGalaxySystem.MAX_POSITION_IN_SECTOR)  * 2 * SubSector +
        LocalPosition;
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
    public const int MAX_POSITION_IN_SECTOR = 5000;
    public const int MAX_SUBSECTOR_IN_SECTOR = 0x7FFF_0000;

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
                var relativePos = t.Value % MAX_POSITION_IN_SECTOR;
                var ssDiff = (int3)(t.Value / MAX_POSITION_IN_SECTOR);
                var edge = MAX_POSITION_IN_SECTOR * -math.sign(ssDiff);

                var newPos = relativePos + edge;
                newPos = math.select(newPos, newPos + MAX_POSITION_IN_SECTOR * 0.000001f, newPos == MAX_POSITION_IN_SECTOR);
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
            .WithNone<CanTravelSectors>()
            .WithNone<Parent>()
            .ForEach((Entity e, ref Translation t, in LocalToGalaxy ltg) =>
            {

                t.Value = (float3)(ltg.AbsolutePosition - playerPosition.AbsolutePosition);
            }).ScheduleParallel();

        bufferSystem.AddJobHandleForProducer(Dependency);
    }
}

public partial class LoadSectorSystem : SystemBase
{
    private EndSimulationEntityCommandBufferSystem bufferSystem;

    protected override void OnCreate()
    {
        bufferSystem = World.GetExistingSystem<EndSimulationEntityCommandBufferSystem>();
        RequireForUpdate(GetEntityQuery(typeof(SectorChange)));
    }

    protected override void OnUpdate()
    {
        var buffer = bufferSystem.CreateCommandBuffer();
        var sectorsToLoad = new NativeList<LocalToGalaxy>(Allocator.Temp);
        var loadedSectorsQuery = GetEntityQuery(new EntityQueryDesc() {
            Options = EntityQueryOptions.IncludeDisabled,
            All = new ComponentType[]
            {
                typeof(SubSector), typeof(LocalToGalaxy), typeof(TravellerInSector)
            }});
        var sectorEntities = loadedSectorsQuery.ToEntityArray(Allocator.Temp);
        var sectors = loadedSectorsQuery.ToComponentDataArray<LocalToGalaxy>(Allocator.Temp);

        var localPlayer = GetSingletonEntity<LocalPlayer>();

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

                if (sc.Entity == localPlayer)
                {
                    if (newSector == Entity.Null)
                    {
                        sectorsToLoad.Add(sc.New);
                        if (math.any(sc.New.Sector != sc.Previous.Sector))
                        {
                            for (int x = -15; x <= 15; x++)
                            for (int y = -15; y <= 15; y++)
                            for (int z = -15; z <= 15; z++)
                            {
                                var potLtg = new LocalToGalaxy {Sector = sc.New.Sector + new int3(x, y, z)};
                                if(GalaxyTools.GetSectorData(potLtg).Type != SectorType.Empty)
                                    sectorsToLoad.Add(potLtg);
                            }
                        }
                        return;
                    }

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
                    buffer.DestroyEntity(e);
                }
            }).Run();

        List<LocalToGalaxy> added = new List<LocalToGalaxy>();
        foreach (var newSector in sectorsToLoad)
        {
            if (added.Contains(newSector)) continue;
            var e = EntityManager.CreateEntity(typeof(SubSector), typeof(LocalToGalaxy), typeof(TravellerInSector), typeof(Disabled), typeof(ToGenerate));
            added.Add(newSector);
            SetComponent(e, newSector);
        }

        bufferSystem.AddJobHandleForProducer(Dependency);
    }
}
