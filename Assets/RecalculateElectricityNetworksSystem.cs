using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEditor.MemoryProfiler;
using UnityEngine;

[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial class GridRecalculationSystemGroup : ComponentSystemGroup
{
    protected override void OnCreate()
    {
        RequireForUpdate(GetEntityQuery(typeof(GridsNeedUpdate)));
        base.OnCreate();
    }
}

public struct NeedsUpdate : IComponentData { }

[UpdateInGroup(typeof(GridRecalculationSystemGroup))]
public partial class RecalculateElectricityNetworksSystem : SystemBase
{
    private EndInitializationEntityCommandBufferSystem bufferSystem;

    public static uint NextId;

    protected override void OnCreate()
    {
        bufferSystem = World.GetExistingSystem<EndInitializationEntityCommandBufferSystem>();
    }

    protected override void OnUpdate()
    {
        var buffer = bufferSystem.CreateCommandBuffer();

        Entities
            .WithName("ApplyElectricityConnectionRotation")
            .WithAll<ElectricityNetworkConnection>()
            .ForEach((Entity e, ref Rotation r, in Identifieable item, in UpdateRotation ur) =>
            {
                var prefabConnection = GetComponent<ElectricityNetworkConnection>(ur.ReferencePrefab);
                var myConnection = GetComponent<ElectricityNetworkConnection>(e);
                myConnection.ConnectsAt = prefabConnection.ConnectsAt.Rotate(ur.Value);
                SetComponent(e, myConnection);
                r.Value = ur.Value;
                buffer.RemoveComponent<UpdateRotation>(e);
                if(!HasComponent<NeedsUpdate>(e))
                    buffer.AddComponent<NeedsUpdate>(e);
            }).Run();

        Entities
            .WithAll<ElectricityNetworkConnection, NeedsUpdate>()
            .ForEach((Entity e, ref DynamicBuffer<ConnectionBuffer> connections) =>
        {
            var myNetwork = GetComponent<ElectricityNetworkConnection>(e);
            for (int i = 0; i < connections.Length; i++)
            {
                var neighbour = connections[i];
                if (!HasComponent<ElectricityNetworkConnection>(neighbour.Value))
                    continue;
                var theirNetwork = GetComponent<ElectricityNetworkConnection>(neighbour.Value);
                if (ConnectionBuffer.Connects(neighbour, myNetwork.ConnectsAt, theirNetwork.ConnectsAt))
                    neighbour.Type |= ConnectionType.Power;
                else
                    neighbour.Type &= ~ConnectionType.Power;
                connections[i] = neighbour;
            }
        }).Run();

        var newNetworks = new NativeList<uint>(Allocator.Temp);
        var nextId = NextId;
        Entities
            .WithName("CalculateElectricNetworks")
            .WithAll<ElectricityNetworkConnection, NeedsUpdate>()
            .ForEach((Entity e, in DynamicBuffer<ConnectionBuffer> connections, in GridItem item) =>
        {
            var myNetwork = GetComponent<ElectricityNetworkConnection>(e);
            for(int i = 0; i < connections.Length; i++)
            {
                var neighbour = connections[i];
                if ((neighbour.Type & ConnectionType.Power) == 0)
                    continue;

                var theirNetwork = GetComponent<ElectricityNetworkConnection>(neighbour.Value);
                if (myNetwork.Id != 0 && myNetwork.Id == theirNetwork.Id && neighbour.Type == ConnectionType.Power)
                {
                    buffer.RemoveComponent<NeedsUpdate>(e);
                    continue;
                }

                uint newNetwork = 0;
                if (myNetwork.Id != 0) newNetwork = myNetwork.Id;
                else if (theirNetwork.Id != 0) newNetwork = theirNetwork.Id;
                else
                {
                    newNetwork = ++nextId;
                    newNetworks.Add(newNetwork);
                }

                myNetwork.Id = newNetwork;
                theirNetwork.Id = newNetwork;

                SetComponent(e, myNetwork);
                SetComponent(neighbour.Value, theirNetwork);
                if(!HasComponent<NeedsUpdate>(neighbour.Value))
                    buffer.AddComponent<NeedsUpdate>(neighbour.Value);
            }
        }).Run();

        NextId = nextId;
        foreach (var newItem in newNetworks)
        {
            var e = EntityManager.CreateEntity(typeof(ElectricityNetwork),
                typeof(ElectricitySystem.NetworkPowerStatusChanged));
            EntityManager.AddBuffer<ConnectionBuffer>(e);
            EntityManager.SetComponentData(e, new ElectricityNetwork{Id = newItem, Power = 0});
        }

        var portQuery = GetEntityQuery(ComponentType.ReadOnly<ElectricityPort>(),
            ComponentType.ReadOnly<ElectricityNetworkConnection>());
        var entities = portQuery.ToEntityArray(Allocator.TempJob);
        var portConnections = portQuery.ToComponentDataArray<ElectricityNetworkConnection>(Allocator.TempJob);
        Entities
            .WithName("ConnectPortsToNetworks")
            .WithDisposeOnCompletion(entities)
            .WithDisposeOnCompletion(portConnections)
            .ForEach((DynamicBuffer<ConnectionBuffer> connections, in ElectricityNetwork network) =>
        {
            connections.Clear();
            for (int i = 0; i < entities.Length; i++)
            {
                if (portConnections[i].Id == network.Id)
                    connections.Add(new ConnectionBuffer {Value = entities[i], Type = ConnectionType.Related});
            }
        }).Schedule();

        bufferSystem.AddJobHandleForProducer(Dependency);
    }
}
