using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
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
    public long XInTenKm;
    public long YInTenKm;
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

public partial class LoadSectorSystem : SystemBase
{
    private EndSimulationEntityCommandBufferSystem bufferSystem;

    public const float MAX_POSITION_IN_SECTOR = 10000f;

    protected override void OnCreate()
    {
        bufferSystem = World.GetExistingSystem<EndSimulationEntityCommandBufferSystem>();
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

        Entities
            .ForEach((Entity e, in SectorChange sc) =>
            {
                Entity oldSector = Entity.Null;
                Entity newSector = Entity.Null;
                for (int i = 0; i < sectors.Length; i++)
                {
                    if (sectors[i].XInTenKm == sc.New.XInTenKm && sectors[i].YInTenKm == sc.New.YInTenKm)
                        newSector = sectorEntities[i];
                    if (sectors[i].XInTenKm == sc.Previous.XInTenKm && sectors[i].YInTenKm == sc.Previous.YInTenKm)
                        oldSector = sectorEntities[i];
                }

                if (newSector == Entity.Null)
                {
                    sectorsToLoad.Add(sc.New);
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

                Debug.Log($"Entering {sc.New.XInTenKm}|{sc.New.YInTenKm}");
                SetComponent(sc.Entity, sc.New);

                var newPlayerBuffer = GetBuffer<TravellerInSector>(newSector);
                if (newPlayerBuffer.Length == 0 && HasComponent<Disabled>(newSector))
                {
                    buffer.RemoveComponent<Disabled>(newSector);
                }
                newPlayerBuffer.Add(new TravellerInSector {Player = sc.Entity});
                buffer.DestroyEntity(e);
        }).Run();

        Entities
            .WithAll<CanTravelSectors>()
            .WithNone<Parent>()
            .ForEach((Entity e, ref LocalToGalaxy ltg, ref Translation t) =>
            {
                var secToLoad = ltg;
                var offset = float3.zero;
                var planePosition = t.Value.xz;
                if (planePosition.x < -MAX_POSITION_IN_SECTOR)
                {
                    secToLoad.XInTenKm--;
                    offset.x = 2 * MAX_POSITION_IN_SECTOR;
                }
                else if (planePosition.x > MAX_POSITION_IN_SECTOR)
                {
                    secToLoad.XInTenKm++;
                    offset.x = -2 * MAX_POSITION_IN_SECTOR;
                }
                if (planePosition.y < -MAX_POSITION_IN_SECTOR)
                {
                    secToLoad.YInTenKm--;
                    offset.z = 2 * MAX_POSITION_IN_SECTOR;
                }
                else if (planePosition.y > MAX_POSITION_IN_SECTOR)
                {
                    secToLoad.YInTenKm++;
                    offset.z = -2 * MAX_POSITION_IN_SECTOR;
                }

                if (ltg.XInTenKm == secToLoad.XInTenKm && ltg.YInTenKm == secToLoad.YInTenKm)
                    return;

                var update = buffer.CreateEntity();
                buffer.AddComponent(update, new SectorChange{Entity = e, Previous = ltg, New = secToLoad});
                t.Value += offset;
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
