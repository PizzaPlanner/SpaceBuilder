using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;

[GenerateAuthoringComponent]
public struct AsteroidData : IComponentData
{
    public uint Seed;
}

public partial class GenerateAsteroidSystem : SystemBase
{
    private EndSimulationEntityCommandBufferSystem bufferSystem;
    protected override void OnCreate()
    {
        bufferSystem = World.GetExistingSystem<EndSimulationEntityCommandBufferSystem>();
    }

    protected override void OnUpdate()
    {
        var buffer = bufferSystem.CreateCommandBuffer();
        var prefabs = GetEntityQuery(typeof(Prefab), typeof(AsteroidData), typeof(RenderMesh)).ToEntityArray(Allocator.Temp);
        Entities
            .WithAll<ToGenerate>()
            .ForEach((Entity e, in AsteroidData data) =>
            {
                var random = new Unity.Mathematics.Random(data.Seed);
                var rm = EntityManager.GetSharedComponentData<RenderMesh>(
                    prefabs[random.NextInt(0, prefabs.Length - 1)]);
                EntityManager.SetSharedComponentData(e, rm);
                buffer.RemoveComponent<ToGenerate>(e);
            }).WithStructuralChanges().WithoutBurst().Run();

        bufferSystem.AddJobHandleForProducer(Dependency);
    }
}
