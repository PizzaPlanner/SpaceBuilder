using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;

[WriteGroup(typeof(LocalToWorld))]
public struct AsteroidData : IComponentData
{
    public uint Seed;
}

public partial class GenerateAsteroidSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var buffer = GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(World.Unmanaged);
        var prefabs =
            GetEntityQuery(new EntityQueryBuilder(Allocator.Temp).WithOptions(EntityQueryOptions.IncludePrefab)
                                                   .WithAll<Prefab, AsteroidData, RenderMesh>())
                .ToEntityArray(Allocator.Temp);
        Entities
            .WithAll<ToGenerate>()
            .ForEach((Entity e, in AsteroidData data) =>
            {
                var random = new Unity.Mathematics.Random(data.Seed);
                var rm = EntityManager.GetSharedComponentManaged<RenderMesh>(
                    prefabs[random.NextInt(0, prefabs.Length - 1)]);
                EntityManager.SetSharedComponentManaged(e, rm);
                buffer.RemoveComponent<ToGenerate>(e);
            }).WithStructuralChanges().WithoutBurst().Run();
    }
}
