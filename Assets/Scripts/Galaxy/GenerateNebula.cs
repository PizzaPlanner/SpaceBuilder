using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.VFX;
using Random = Unity.Mathematics.Random;

public struct GenerateNebula : IComponentData { }

public partial class GenerateNebulaSystem : SystemBase
{
    private EndSimulationEntityCommandBufferSystem bufferSystem;
    private Entity prefab;

    protected override void OnCreate()
    {
        bufferSystem = World.GetExistingSystem<EndSimulationEntityCommandBufferSystem>();
    }

    protected override void OnUpdate()
    {
        if (prefab == Entity.Null)
        {
            prefab = GetEntityQuery(typeof(Prefab), typeof(Nebula)).GetSingletonEntity();
        }

        var ecb = bufferSystem.CreateCommandBuffer();
        var prefabLocal = prefab;
        Entities
            .WithAll<GenerateNebula>()
            .ForEach((Entity e, in LocalToGalaxy ltg, in SectorData data) =>
            {
                var star = ecb.Instantiate(prefabLocal);
                ecb.SetComponent(star, ltg);
                ecb.RemoveComponent<GenerateNebula>(e);
            }).Schedule();

        bufferSystem.AddJobHandleForProducer(Dependency);
    }
}
