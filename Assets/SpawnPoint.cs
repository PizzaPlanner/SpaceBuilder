using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

[GenerateAuthoringComponent]
public struct SpawnPoint : IComponentData
{
    public Entity Prefab;
}

public class PlayerSpawnSystem : SystemBase
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

        Entities.ForEach((in SpawnPoint sp) =>
        {
            var player = buffer.Instantiate(sp.Prefab);
            buffer.AddBuffer<CloseGrids>(player);
            buffer.AddComponent<LocalToParent>(player);
            buffer.AddComponent<CanMove>(player);
            buffer.AddComponent(player, new Parent {Value = origin});
        }).Schedule();
        bufferSystem.AddJobHandleForProducer(Dependency);
    }
}
