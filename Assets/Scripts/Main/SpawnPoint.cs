using Galaxy.SectorLoading;
using Unity.Entities;
using Unity.Transforms;


public struct SpawnPoint : IComponentData
{
    public Entity Prefab;
}

public partial class PlayerSpawnSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<SpawnPoint>();
    }

    protected override void OnUpdate()
    {
        if (SystemAPI.HasSingleton<LocalPlayer>())
        {
            Enabled = false;
            return;
        }
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                              .CreateCommandBuffer(World.Unmanaged);

        var spawnEntity = SystemAPI.GetSingletonEntity<SpawnPoint>();
        var sp = SystemAPI.GetComponent<SpawnPoint>(spawnEntity);
        var transform = SystemAPI.GetComponent<LocalTransform>(spawnEntity);
        var ltg = SystemAPI.GetComponent<LocalToGalaxy>(spawnEntity);

        var player = ecb.Instantiate(sp.Prefab);
        ecb.SetComponent(player, transform);
        ecb.SetComponent(player, ltg);
        ecb.AddComponent<PromoteToReferenceFrame>(player);
    }
}
