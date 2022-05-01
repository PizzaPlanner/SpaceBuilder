using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[GenerateAuthoringComponent]
public struct SpawnPoint : IComponentData
{
    public Entity Prefab;
}

public partial class PlayerSpawnSystem : SystemBase
{
    private EndSimulationEntityCommandBufferSystem bufferSystem;

    protected override void OnCreate()
    {
        bufferSystem = World.GetExistingSystem<EndSimulationEntityCommandBufferSystem>();
    }

    protected override void OnUpdate()
    {
        if (HasSingleton<LocalPlayer>())
        {
            Enabled = false;
            return;
        }
        var buffer = bufferSystem.CreateCommandBuffer();
        var origin = World.GetExistingSystem<DockingSystem>().Origin;

        Entities.ForEach((in SpawnPoint sp, in LocalToWorld ltw, in LocalToGalaxy ltg) =>
        {
            var player = buffer.Instantiate(sp.Prefab);
            buffer.AddBuffer<CloseGrids>(player);
            buffer.AddComponent<CanMove>(player);
            buffer.SetComponent(player, new Translation{ Value = ltw.Position });
            buffer.AddComponent(player, ltg);
            buffer.AddComponent(player, new CanTravelSectors());

            var update = buffer.CreateEntity();
            buffer.AddComponent(update, new SectorChange
            {
                Entity = player,
                Previous = new LocalToGalaxy{XInTenKm = int.MinValue, YInTenKm = int.MinValue},
                New = ltg
            });
        }).Schedule();
        bufferSystem.AddJobHandleForProducer(Dependency);
    }
}
