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
        ecb.AddBuffer<CloseGrids>(player);
        ecb.SetComponent(player, transform);
        ecb.AddComponent(player, ltg);
        ecb.AddComponent(player, new CanTravelSectors());

        var ctrl = ecb.CreateEntity();
        ecb.AddComponent<InputSystem.ControlPlayer>(ctrl);

        var update = ecb.CreateEntity();
        ecb.AddComponent(update, new SectorChange
        {
            Entity = player,
            Previous = new LocalToGalaxy(),
            New = ltg,
            ChangeType = SectorChangeType.Spawn
        });
    }
}
