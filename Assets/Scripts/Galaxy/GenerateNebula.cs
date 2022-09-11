using Unity.Entities;

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
                ecb.AddComponent<ToGenerate>(star);
                ecb.RemoveComponent<GenerateNebula>(e);
            }).Schedule();

        bufferSystem.AddJobHandleForProducer(Dependency);
    }
}
