using Unity.Entities;
using Unity.Transforms;


public struct SpawnPoint : IComponentData
{
    public Entity Prefab;
}

public partial class PlayerSpawnSystem : SystemBase
{

    protected override void OnUpdate()
    {
        if (HasSingleton<LocalPlayer>())
        {
            Enabled = false;
            return;
        }
        var buffer = GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);

        Entities.ForEach((in SpawnPoint sp, in LocalToWorld ltw, in LocalToGalaxy ltg) =>
        {
            var player = buffer.Instantiate(sp.Prefab);
            buffer.AddBuffer<CloseGrids>(player);
            buffer.SetComponent(player, new WorldTransform { Position = ltw.Position });
            buffer.AddComponent(player, ltg);
            buffer.AddComponent(player, new CanTravelSectors());

            var ctrl = buffer.CreateEntity();
            buffer.AddComponent<InputSystem.ControlPlayer>(ctrl);

            var update = buffer.CreateEntity();
            buffer.AddComponent(update, new SectorChange
            {
                Entity = player,
                Previous = new LocalToGalaxy(),
                New = ltg,
                ChangeType = SectorChangeType.Teleport
            });
        }).Schedule();
    }
}
