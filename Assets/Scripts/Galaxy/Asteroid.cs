using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;


[GenerateAuthoringComponent]
public struct AsteroidData : IComponentData
{
    public int Diameter;
    public float3 Seed;
}

public partial class AsteroidSystem : SystemBase
{
    private EndSimulationEntityCommandBufferSystem bufferSystem;
    protected override void OnCreate()
    {
        bufferSystem = World.GetExistingSystem<EndSimulationEntityCommandBufferSystem>();
    }

    protected override void OnUpdate()
    {
        var buffer = bufferSystem.CreateCommandBuffer();
        var maxPerFrame = 1;
        Entities
            .WithAll<ToGenerate>()
            .ForEach((Entity e, RenderMesh emesh, in AsteroidData data) =>
            {
                if (--maxPerFrame <= 0) return;
                var rm = emesh;
                var generator = new MarchingCubesSphericalGenerator(data.Diameter, data.Seed, true);
                generator.Generate();
                rm.mesh = generator.Bake(true);
                buffer.SetSharedComponent(e, rm);
                buffer.SetComponent(e, new RenderBounds{Value = new AABB{Center = rm.mesh.bounds.center, Extents = rm.mesh.bounds.extents }});
                buffer.RemoveComponent<ToGenerate>(e);
        }).WithoutBurst().Run();

        bufferSystem.AddJobHandleForProducer(Dependency);
    }
}
