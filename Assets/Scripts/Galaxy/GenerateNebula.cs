using Galaxy;
using Unity.Collections;
using Unity.Entities;

public struct GenerateNebula : IComponentData { }

public partial class GenerateNebulaSystem : SystemBase
{
    private Entity prefab;

    protected override void OnUpdate()
    {
        if (prefab == Entity.Null)
        {
            if (!GetEntityQuery(new EntityQueryBuilder(Allocator.Temp)
                                .WithOptions(EntityQueryOptions.IncludePrefab)
                                .WithAll<Nebula>())
                    .TryGetSingletonEntity<Nebula>(out prefab))
                return;
        }

        var ecb = GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
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
    }
}
