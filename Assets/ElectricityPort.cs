using Unity.Entities;
using Unity.Transforms;

public struct ElectricityPort : IExchangeNetworkPort<float, ElectricityNetwork>
{
    public float value;
    public float Value => value;
}

public struct ElectricityNetwork : IExchangeNetwork
{
    public float Power;
}

public struct ElectricityNetworkConnection : IExchangeNetworkConnector<ElectricityNetwork>
{
    public Entity Target;
    public GridItem.Side ConnectsAt;

    public GridItem.Side Sides
    {
        get => ConnectsAt;
        set => ConnectsAt = value;
    }

    public Entity Network => Target;
}

public struct ElectricityNeedsRecalculation : IComponentData { }
[UpdateInGroup(typeof(InitializationSystemGroup))]
[UpdateAfter(typeof(GridRecalculationSystem))]
public partial class RecalculateElectricityNetworksSystem : SystemBase
{
    private EndInitializationEntityCommandBufferSystem bufferSystem;

    protected override void OnCreate()
    {
        bufferSystem = World.GetExistingSystem<EndInitializationEntityCommandBufferSystem>();
    }

    protected override void OnUpdate()
    {
        var buffer = bufferSystem.CreateCommandBuffer();
        Entities.WithAll<ElectricityNetworkConnection>().ForEach((Entity e, in DynamicBuffer<NeighbourBuffer> neighbours, in GridItem item) =>
        {
            var myNetwork = GetComponent<ElectricityNetworkConnection>(e);
            foreach (var neighbour in neighbours)
            {
                var theirNetwork = GetComponent<ElectricityNetworkConnection>(neighbour.Value);
                if (myNetwork.Network == theirNetwork.Network)
                    continue;

                var newnetwork = myNetwork.Network == Entity.Null ? theirNetwork.Network : myNetwork.Network;
                if (newnetwork == Entity.Null)
                {
                    newnetwork = buffer.CreateEntity();
                    buffer.AddComponent(newnetwork, new ElectricityNetwork());
                }

                myNetwork.Target = newnetwork;
                theirNetwork.Target = newnetwork;
                SetComponent(e, myNetwork);
                SetComponent(neighbour.Value, theirNetwork);
            }
        }).Schedule();
    }
}
