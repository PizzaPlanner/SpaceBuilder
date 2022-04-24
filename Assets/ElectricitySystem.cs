using Unity.Entities;
using Unity.Transforms;

[UpdateInGroup(typeof(InitializationSystemGroup))]
[UpdateAfter(typeof(GridRecalculationSystem))]
[UpdateAfter(typeof(RecalculateElectricityNetworksSystem))]
public partial class ElectricitySystem : SystemBase
{
    private EndInitializationEntityCommandBufferSystem bufferSystem;
    protected override void OnCreate()
    {
        bufferSystem = World.GetExistingSystem<EndInitializationEntityCommandBufferSystem>();
    }

    public struct NetworkPowerStatusChanged : IComponentData { }

    protected override void OnUpdate()
    {
        var buffer = bufferSystem.CreateCommandBuffer();

        Entities
            .WithName("CalculateNetworkPower")
            .WithNone<NetworkPowerStatusChanged>()
            .ForEach((Entity e, DynamicBuffer<ConnectionBuffer> connections, ref ElectricityNetwork network) =>
            {
                var hadPower = network.Power > 0;
                network.Power = 0;
                for (int i = 0; i < connections.Length; i++)
                    network.Power += GetComponent<ElectricityPort>(connections[i].Value).Value;
                var hasPower = network.Power > 0;
                if(hadPower != hasPower)
                    buffer.AddComponent<NetworkPowerStatusChanged>(e);
            }).Schedule();

        var ports = GetComponentDataFromEntity<ElectricityPort>(false);
        Entities
            .WithName("AssignOperationalValues")
            .WithAll<NetworkPowerStatusChanged>()
            .ForEach((Entity e, DynamicBuffer<ConnectionBuffer> connections, in ElectricityNetwork network) =>
            {
                for (int i = 0; i < connections.Length; i++)
                {
                    var entity = connections[i].Value;
                    if (!ports.HasComponent(entity))
                        continue;

                    var component = ports[entity];
                    component.NetworkOperational = network.Power > 0;
                    ports[entity] = component;
                }
                buffer.RemoveComponent<NetworkPowerStatusChanged>(e);
            }).Schedule();

        bufferSystem.AddJobHandleForProducer(Dependency);
    }
}
